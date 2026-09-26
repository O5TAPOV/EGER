using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class RegisterService
{
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly ISubjectRepository _subjects;
    private readonly IGradeRepository _grades;
    private readonly IClassSessionRepository _sessions;
    private readonly IGradingSettingsRepository _settings;

    public RegisterService(
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IClassSessionRepository sessions,
        IGradingSettingsRepository settings)
    {
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
        _sessions = sessions;
        _settings = settings;
    }

    public async Task<RegisterResponse> GetAsync(
        Actor actor,
        string subjectId,
        string? group,
        string? studentId,
        CancellationToken ct = default)
    {
        var subject = await RequireSubjectAsync(subjectId, ct);
        var (students, canEdit) = await ResolveAudienceAsync(actor, subject, group, studentId, ct);
        var sheetGroup = students.Count == 0
            ? group?.Trim() ?? ""
            : students[0].Group;
        if (actor.Role != Roles.Student)
            sheetGroup = group!.Trim();

        await AttachOrphansAsync(subject.Id, sheetGroup, ct);
        return await BuildAsync(subject, sheetGroup, students, canEdit, ct);
    }

    public async Task<RegisterResponse> AddColumnAsync(Actor actor, AddColumnRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        if (!GradeTypes.IsKnown(request.GradeType))
            throw new AppException(400, "Невідомий тип оцінювання");
        if (request.Date is null)
            throw new AppException(400, "Вкажіть дату колонки");

        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var settings = await _settings.GetAsync(ct);
        var group = request.Group.Trim();
        var sessions = await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct);
        var isFinal = GradeTypes.IsFinal(request.GradeType);
        if (isFinal && sessions.Any(session => GradeTypes.IsFinal(session.GradeType)))
            throw new AppException(400, "Для цієї відомості вже є колонка підсумкового контролю");

        var cap = isFinal ? settings.FinalMax : settings.CurrentMax;
        if (request.MaxPoints > cap)
        {
            var kind = isFinal ? "підсумкових" : "поточних";
            throw new AppException(400, $"Увага. Максимум колонки {request.MaxPoints} перевищує допустимий максимум {kind} балів {cap}.");
        }

        await _sessions.CreateAsync(new ClassSession
        {
            SubjectId = subject.Id,
            Group = group,
            Date = Dates.NormalizeUtc(request.Date),
            GradeType = request.GradeType,
            MaxPoints = request.MaxPoints
        }, ct);

        var students = (await _students.GetByGroupAsync(group, ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, group, students, canEdit: true, ct);
    }

    public async Task<RegisterResponse> SetCellAsync(Actor actor, SetCellRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(request.SessionId);
        Ids.Ensure(request.StudentId);
        if (request.Points is < 0)
            throw new AppException(400, "Бал не може бути від'ємним");

        var session = await _sessions.GetByIdAsync(request.SessionId, ct)
            ?? throw new AppException(404, "Колонку не знайдено");
        var subject = await RequireSubjectAsync(session.SubjectId, ct);
        var professor = await EnsureTeachesAsync(actor, subject, ct);
        var student = await _students.GetByIdAsync(request.StudentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        if (!string.Equals(student.Group, session.Group, StringComparison.Ordinal))
            throw new AppException(400, "Студент не належить до цієї групи");

        var own = (await _grades.GetByStudentAsync(student.Id, ct))
            .Where(grade => grade.SubjectId == subject.Id)
            .ToList();
        var cellGrades = own.Where(grade => grade.SessionId == session.Id).ToList();

        if (request.Points is null)
        {
            foreach (var grade in cellGrades)
                await _grades.DeleteAsync(grade.Id, ct);
        }
        else
        {
            var points = request.Points.Value;
            var others = own.Where(grade => grade.SessionId != session.Id).ToList();
            if (GradeTypes.IsFinal(session.GradeType) && others.Any(grade => GradeTypes.IsFinal(grade.GradeType)))
                throw new AppException(400, "Підсумок для цього студента з дисципліни можна виставити лише один раз");

            var preview = new List<Grade>(others)
            {
                new()
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    GradeType = session.GradeType,
                    GradeValue = points,
                    Date = session.Date,
                    SessionId = session.Id
                }
            };
            var settings = await _settings.GetAsync(ct);
            var standing = GradeBook.Evaluate(preview, settings);
            var messages = new List<string>();
            if (points > session.MaxPoints)
                messages.Add(PointGuard.Column(student.FullName, points, session.MaxPoints));
            messages.AddRange(PointGuard.Describe(student.FullName, standing, settings));
            if (messages.Count > 0)
                throw new AppException(400, PointGuard.Join(messages));

            var keeper = cellGrades.FirstOrDefault();
            if (keeper is null)
            {
                await _grades.CreateAsync(new Grade
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    ProfessorId = professor.Id,
                    GradeValue = points,
                    GradeType = session.GradeType,
                    Date = session.Date,
                    SessionId = session.Id
                }, ct);
            }
            else
            {
                keeper.GradeValue = points;
                keeper.GradeType = session.GradeType;
                keeper.Date = session.Date;
                keeper.ProfessorId = professor.Id;
                keeper.SessionId = session.Id;
                await _grades.UpdateAsync(keeper, ct);
                foreach (var extra in cellGrades.Skip(1))
                    await _grades.DeleteAsync(extra.Id, ct);
            }
        }

        var students = (await _students.GetByGroupAsync(session.Group, ct))
            .OrderBy(item => item.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, session.Group, students, canEdit: true, ct);
    }

    private async Task AttachOrphansAsync(string subjectId, string group, CancellationToken ct)
    {
        var studentIds = (await _students.GetByGroupAsync(group, ct)).Select(student => student.Id).ToHashSet();
        var sessions = (await _sessions.GetBySubjectGroupAsync(subjectId, group, ct)).ToList();
        var known = sessions.Select(session => session.Id).ToHashSet();
        var grades = (await _grades.GetBySubjectAsync(subjectId, ct))
            .Where(grade => studentIds.Contains(grade.StudentId))
            .Where(grade => string.IsNullOrEmpty(grade.SessionId) || !known.Contains(grade.SessionId))
            .ToList();
        if (grades.Count == 0)
            return;

        foreach (var bucket in grades.GroupBy(grade => (grade.Date.Date, grade.GradeType)))
        {
            var session = sessions.FirstOrDefault(item =>
                item.Date.Date == bucket.Key.Date && item.GradeType == bucket.Key.GradeType);
            if (session is null && GradeTypes.IsFinal(bucket.Key.GradeType))
                session = sessions.FirstOrDefault(item => GradeTypes.IsFinal(item.GradeType));
            if (session is null)
            {
                session = new ClassSession
                {
                    SubjectId = subjectId,
                    Group = group,
                    Date = DateTime.SpecifyKind(bucket.Key.Date, DateTimeKind.Utc),
                    GradeType = bucket.Key.GradeType,
                    MaxPoints = Math.Max(1, bucket.Max(grade => grade.GradeValue))
                };
                await _sessions.CreateAsync(session, ct);
                sessions.Add(session);
            }

            foreach (var grade in bucket)
            {
                grade.SessionId = session.Id;
                await _grades.UpdateAsync(grade, ct);
            }
        }
    }

    private async Task<RegisterResponse> BuildAsync(
        Subject subject,
        string group,
        IReadOnlyList<Student> students,
        bool canEdit,
        CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var sessions = (await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct))
            .OrderBy(session => session.Date)
            .ThenBy(session => GradeTypes.IsFinal(session.GradeType))
            .ThenBy(session => session.Id, StringComparer.Ordinal)
            .ToList();
        var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
        var rows = new List<RegisterRowResponse>();
        var number = 1;
        foreach (var student in students)
        {
            var own = grades.Where(grade => grade.StudentId == student.Id).ToList();
            var standing = GradeBook.Evaluate(own, settings);
            var warnings = PointGuard.Describe(student.FullName, standing, settings);
            foreach (var session in sessions)
            {
                var taken = own.Where(grade => grade.SessionId == session.Id).Sum(grade => grade.GradeValue);
                var filled = own.Any(grade => grade.SessionId == session.Id);
                if (filled && taken > session.MaxPoints)
                    warnings.Add(PointGuard.Column(student.FullName, taken, session.MaxPoints));
            }
            rows.Add(new RegisterRowResponse
            {
                Number = number++,
                StudentId = student.Id,
                FullName = student.FullName,
                CurrentPoints = standing.CurrentPoints,
                FinalPoints = standing.HasFinal ? standing.FinalPoints : null,
                Total = standing.Total,
                HasMarks = own.Count > 0,
                HasFinal = standing.HasFinal,
                Debt = standing.Debt,
                WithinLimits = standing.WithinLimits,
                Ects = standing.Ects,
                Status = own.Count == 0 ? "" : StatusOf(standing),
                Warnings = warnings,
                Cells = sessions.Select(session =>
                {
                    var cell = own.Where(grade => grade.SessionId == session.Id).ToList();
                    return new RegisterCellResponse
                    {
                        SessionId = session.Id,
                        Points = cell.Count == 0 ? null : cell.Sum(grade => grade.GradeValue)
                    };
                }).ToList()
            });
        }

        var scored = rows.Where(row => row.Cells.Any(cell => cell.Points is not null)).Select(row => row.Total).ToList();
        return new RegisterResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Group = group,
            Credits = subject.Credits,
            CurrentMax = settings.CurrentMax,
            FinalMax = settings.FinalMax,
            PassThreshold = settings.PassThreshold,
            CanEdit = canEdit,
            HasFinalColumn = sessions.Any(session => GradeTypes.IsFinal(session.GradeType)),
            ClassAverage = scored.Count == 0 ? null : GradeBook.Average(scored),
            DebtCount = rows.Count(row => row.Debt),
            Columns = sessions.Select(session => new RegisterColumnResponse
            {
                Id = session.Id,
                Date = session.Date,
                GradeType = session.GradeType,
                MaxPoints = session.MaxPoints,
                IsFinal = GradeTypes.IsFinal(session.GradeType)
            }).ToList(),
            Rows = rows
        };
    }

    private static string StatusOf(SubjectStanding standing)
    {
        if (!standing.WithinLimits)
            return "перевищення";
        if (standing.Debt)
            return "борг";
        if (!standing.HasFinal)
            return "набрано на зараз";
        return standing.Outcome;
    }

    private async Task<(List<Student> Students, bool CanEdit)> ResolveAudienceAsync(
        Actor actor,
        Subject subject,
        string? group,
        string? studentId,
        CancellationToken ct)
    {
        if (actor.Role == Roles.Student)
        {
            var own = await _students.GetByUserIdAsync(actor.UserId, ct)
                ?? throw new AppException(404, "Профіль студента не знайдено");
            return ([own], false);
        }

        if (string.IsNullOrWhiteSpace(group))
            throw new AppException(400, "Оберіть групу");
        await EnsureTeachesAsync(actor, subject, ct);
        var students = (await _students.GetByGroupAsync(group.Trim(), ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            Ids.Ensure(studentId);
            students = students.Where(student => student.Id == studentId).ToList();
            if (students.Count == 0)
                throw new AppException(404, "Студента не знайдено в цій групі");
        }

        return (students, true);
    }

    private async Task<Subject> RequireSubjectAsync(string subjectId, CancellationToken ct)
    {
        Ids.Ensure(subjectId);
        return await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
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

        var me = await _professors.GetByUserIdAsync(actor.UserId, ct)
            ?? throw new AppException(403, "Профіль викладача не знайдено");
        if (!subject.ProfessorIds.Contains(me.Id))
            throw new AppException(403, "Ви не викладаєте цю дисципліну");
        return me;
    }
}
