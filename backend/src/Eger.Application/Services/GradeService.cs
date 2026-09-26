using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class GradeService
{
    private readonly IGradeRepository _grades;
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly ISubjectRepository _subjects;
    private readonly IGradingSettingsRepository _settings;

    public GradeService(
        IGradeRepository grades,
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradingSettingsRepository settings)
    {
        _grades = grades;
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _settings = settings;
    }

    public async Task<IReadOnlyList<GradeResponse>> ListAsync(
        Actor actor,
        string? studentId,
        string? subjectId,
        CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");

        if (!string.IsNullOrWhiteSpace(studentId))
            Ids.Ensure(studentId);
        if (!string.IsNullOrWhiteSpace(subjectId))
            Ids.Ensure(subjectId);

        IReadOnlyList<Grade> grades;
        if (!string.IsNullOrWhiteSpace(subjectId))
            grades = await _grades.GetBySubjectAsync(subjectId, ct);
        else if (!string.IsNullOrWhiteSpace(studentId))
            grades = await _grades.GetByStudentAsync(studentId, ct);
        else
            grades = await _grades.GetAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(studentId))
            grades = grades.Where(g => g.StudentId == studentId).ToList();

        if (actor.Role == Roles.Professor)
        {
            var me = await RequireProfessorAsync(actor.UserId, ct);
            var mine = await _subjects.GetByProfessorAsync(me.Id, ct);
            var allowed = mine.Select(s => s.Id).ToHashSet();
            grades = grades.Where(g => allowed.Contains(g.SubjectId)).ToList();
        }

        return await MapManyAsync(grades, ct);
    }

    public async Task<IReadOnlyList<GradeResponse>> MineAsync(string userId, CancellationToken ct = default)
    {
        var student = await _students.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль студента не знайдено");
        var grades = await _grades.GetByStudentAsync(student.Id, ct);
        return await MapManyAsync(grades, ct);
    }

    public async Task<GradeGridResponse> GridAsync(Actor actor, string subjectId, string group, CancellationToken ct = default)
    {
        Ids.Ensure(subjectId);
        if (string.IsNullOrWhiteSpace(group))
            throw new AppException(400, "Оберіть групу");

        var subject = await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        await EnsureTeachesAsync(actor, subject, ct);

        var students = await _students.GetByGroupAsync(group.Trim(), ct);
        var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var relevant = grades.Where(g => studentIds.Contains(g.StudentId)).ToList();
        var mapped = await MapManyAsync(relevant, ct);
        var byStudent = mapped.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).ToList());

        return new GradeGridResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Credits = subject.Credits,
            Group = group.Trim(),
            Rows = students
                .OrderBy(s => s.FullName, StringComparer.CurrentCulture)
                .Select(s => new GradeGridRow
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    StudentCardNumber = s.StudentCardNumber,
                    Grades = byStudent.TryGetValue(s.Id, out var list) ? list : []
                })
                .ToList()
        };
    }

    public async Task<GradeResponse> CreateAsync(Actor actor, GradeRequest request, CancellationToken ct = default)
    {
        var grade = await BuildGradeAsync(actor, request, existing: null, ct);
        await _grades.CreateAsync(grade, ct);
        var mapped = await MapManyAsync([grade], ct);
        return mapped[0];
    }

    public async Task<GradeResponse> UpdateAsync(Actor actor, string id, GradeRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var existing = await _grades.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Оцінку не знайдено");
        var grade = await BuildGradeAsync(actor, request, existing, ct);
        grade.Id = existing.Id;
        await _grades.UpdateAsync(grade, ct);
        var mapped = await MapManyAsync([grade], ct);
        return mapped[0];
    }

    public async Task DeleteAsync(Actor actor, string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var grade = await _grades.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Оцінку не знайдено");
        var subject = await _subjects.GetByIdAsync(grade.SubjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        await EnsureTeachesAsync(actor, subject, ct);
        if (actor.Role == Roles.Professor)
        {
            var me = await RequireProfessorAsync(actor.UserId, ct);
            if (grade.ProfessorId != me.Id)
                throw new AppException(403, "Можна змінювати лише власні оцінки");
        }
        await _grades.DeleteAsync(grade.Id, ct);
    }

    public async Task<IReadOnlyList<GradeResponse>> BulkAsync(Actor actor, BulkGradeRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(request.SubjectId);
        var subject = await _subjects.GetByIdAsync(request.SubjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        var professor = await EnsureTeachesAsync(actor, subject, ct);

        var saved = new List<Grade>();
        foreach (var item in request.Items)
        {
            if (!GradeTypes.IsKnown(item.GradeType))
                throw new AppException(400, "Невідомий тип оцінювання");
            Ids.Ensure(item.StudentId);
            var student = await _students.GetByIdAsync(item.StudentId, ct)
                ?? throw new AppException(404, "Студента не знайдено");

            Grade grade;
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                Ids.Ensure(item.Id);
                var existing = await _grades.GetByIdAsync(item.Id, ct)
                    ?? throw new AppException(404, "Оцінку не знайдено");
                if (existing.SubjectId != subject.Id || existing.StudentId != student.Id)
                    throw new AppException(400, "Оцінка не належить до обраної дисципліни або студента");
                if (actor.Role == Roles.Professor && existing.ProfessorId != professor.Id)
                    throw new AppException(403, "Можна змінювати лише власні оцінки");

                existing.GradeValue = item.GradeValue;
                existing.GradeType = item.GradeType;
                existing.Date = Dates.NormalizeUtc(item.Date);
                existing.ProfessorId = professor.Id;
                await EnsureLimitsAsync(existing, existing.Id, ct);
                await _grades.UpdateAsync(existing, ct);
                grade = existing;
            }
            else
            {
                grade = new Grade
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    ProfessorId = professor.Id,
                    GradeValue = item.GradeValue,
                    GradeType = item.GradeType,
                    Date = Dates.NormalizeUtc(item.Date)
                };
                await EnsureLimitsAsync(grade, null, ct);
                await _grades.CreateAsync(grade, ct);
            }

            saved.Add(grade);
        }

        return await MapManyAsync(saved, ct);
    }

    private async Task<Grade> BuildGradeAsync(Actor actor, GradeRequest request, Grade? existing, CancellationToken ct)
    {
        if (!GradeTypes.IsKnown(request.GradeType))
            throw new AppException(400, "Невідомий тип оцінювання");
        Ids.Ensure(request.StudentId);
        Ids.Ensure(request.SubjectId);

        var subject = await _subjects.GetByIdAsync(request.SubjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        var student = await _students.GetByIdAsync(request.StudentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        var professor = await ResolveProfessorAsync(actor, subject, request.ProfessorId, ct);

        if (existing is not null && actor.Role == Roles.Professor && existing.ProfessorId != professor.Id)
            throw new AppException(403, "Можна змінювати лише власні оцінки");

        var grade = new Grade
        {
            Id = existing?.Id ?? "",
            StudentId = student.Id,
            SubjectId = subject.Id,
            ProfessorId = professor.Id,
            GradeValue = request.GradeValue,
            GradeType = request.GradeType,
            Date = Dates.NormalizeUtc(request.Date)
        };
        await EnsureLimitsAsync(grade, existing?.Id, ct);
        return grade;
    }

    private async Task EnsureLimitsAsync(Grade incoming, string? replacingId, CancellationToken ct)
    {
        if (incoming.GradeValue < 0)
            throw new AppException(400, "Бали не можуть бути від'ємними");

        var settings = await _settings.GetAsync(ct);
        var rows = (await _grades.GetByStudentAsync(incoming.StudentId, ct))
            .Where(row => row.SubjectId == incoming.SubjectId && row.Id != replacingId)
            .ToList();
        rows.Add(incoming);

        var finals = rows.Where(row => GradeTypes.IsFinal(row.GradeType)).ToList();
        if (finals.Count > 1)
            throw new AppException(400, "Підсумок для цього студента з дисципліни можна виставити лише один раз");

        var student = await _students.GetByIdAsync(incoming.StudentId, ct);
        var standing = GradeBook.Evaluate(rows, settings);
        var messages = PointGuard.Describe(student?.FullName ?? "студента", standing, settings);
        if (messages.Count > 0)
            throw new AppException(400, PointGuard.Join(messages));
    }

    private async Task<Professor> ResolveProfessorAsync(Actor actor, Subject subject, string? requestedProfessorId, CancellationToken ct)
    {
        if (actor.Role == Roles.Admin)
        {
            var professorId = string.IsNullOrWhiteSpace(requestedProfessorId)
                ? subject.ProfessorIds.FirstOrDefault()
                : requestedProfessorId.Trim();
            if (string.IsNullOrWhiteSpace(professorId))
                throw new AppException(400, "До дисципліни не призначено викладача");
            Ids.Ensure(professorId);
            if (!subject.ProfessorIds.Contains(professorId))
                throw new AppException(400, "Викладач не призначений на цю дисципліну");
            return await _professors.GetByIdAsync(professorId, ct)
                ?? throw new AppException(404, "Викладача не знайдено");
        }

        var me = await EnsureTeachesAsync(actor, subject, ct);
        if (!string.IsNullOrWhiteSpace(requestedProfessorId) && requestedProfessorId != me.Id)
            throw new AppException(403, "Можна виставляти оцінки лише від свого імені");
        return me;
    }

    private async Task<Professor> EnsureTeachesAsync(Actor actor, Subject subject, CancellationToken ct)
    {
        if (actor.Role == Roles.Admin)
        {
            var professorId = subject.ProfessorIds.FirstOrDefault()
                ?? throw new AppException(400, "До дисципліни не призначено викладача");
            return await _professors.GetByIdAsync(professorId, ct)
                ?? throw new AppException(404, "Викладача не знайдено");
        }

        if (actor.Role != Roles.Professor)
            throw new AppException(403, "Недостатньо прав");

        var me = await RequireProfessorAsync(actor.UserId, ct);
        if (!subject.ProfessorIds.Contains(me.Id))
            throw new AppException(403, "Ви не викладаєте цю дисципліну");
        return me;
    }

    private async Task<Professor> RequireProfessorAsync(string userId, CancellationToken ct) =>
        await _professors.GetByUserIdAsync(userId, ct)
        ?? throw new AppException(403, "Профіль викладача не знайдено");

    private async Task<List<GradeResponse>> MapManyAsync(IReadOnlyList<Grade> grades, CancellationToken ct)
    {
        var students = await _students.GetByIdsAsync(grades.Select(g => g.StudentId), ct);
        var subjects = await _subjects.GetAllAsync(ct);
        var professors = await _professors.GetByIdsAsync(grades.Select(g => g.ProfessorId), ct);
        var studentNames = students.ToDictionary(s => s.Id, s => s.FullName);
        var subjectMap = subjects.ToDictionary(s => s.Id);
        var professorNames = professors.ToDictionary(p => p.Id, p => p.FullName);

        return grades
            .OrderByDescending(g => g.Date)
            .Select(g =>
            {
                subjectMap.TryGetValue(g.SubjectId, out var subject);
                return new GradeResponse
                {
                    Id = g.Id,
                    StudentId = g.StudentId,
                    StudentName = studentNames.TryGetValue(g.StudentId, out var studentName) ? studentName : "—",
                    SubjectId = g.SubjectId,
                    SubjectTitle = subject?.Title ?? "—",
                    ProfessorId = g.ProfessorId,
                    ProfessorName = professorNames.TryGetValue(g.ProfessorId, out var professorName) ? professorName : "—",
                    GradeValue = g.GradeValue,
                    Date = g.Date,
                    GradeType = g.GradeType,
                    Credits = subject?.Credits ?? 0
                };
            })
            .ToList();
    }
}
