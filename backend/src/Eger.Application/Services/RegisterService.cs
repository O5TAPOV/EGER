using System.Globalization;
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
    private readonly IJournalSheetRepository _sheets;
    private readonly IGradingSettingsRepository _settings;

    public RegisterService(
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IClassSessionRepository sessions,
        IJournalSheetRepository sheets,
        IGradingSettingsRepository settings)
    {
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
        _sessions = sessions;
        _sheets = sheets;
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
        var checkedGroup = actor.Role == Roles.Student ? students[0].Group : group;
        EnsureSubjectGroup(subject, checkedGroup);
        var sheetGroup = students.Count == 0
            ? group?.Trim() ?? ""
            : students[0].Group;
        if (actor.Role != Roles.Student)
            sheetGroup = group!.Trim();

        await AttachOrphansAsync(subject.Id, sheetGroup, ct);
        await EnsureSheetAsync(subject, sheetGroup, ct);
        var omitHidden = actor.Role != Roles.Student && string.IsNullOrWhiteSpace(studentId);
        return await BuildAsync(subject, sheetGroup, students, canEdit, omitHidden, ct);
    }

    public async Task<RegisterResponse> SetFinalizedAsync(Actor actor, UpdateSheetRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var group = request.Group.Trim();
        var sheet = await EnsureSheetAsync(subject, group, ct);
        sheet.Finalized = request.Finalized;
        await _sheets.UpdateAsync(sheet, ct);
        var students = (await _students.GetByGroupAsync(group, ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, group, students, canEdit: true, omitHidden: true, ct);
    }

    public async Task<IReadOnlyList<string>> GroupsForSubjectAsync(Actor actor, string subjectId, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var subject = await RequireSubjectAsync(subjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        if (subject.Groups is { Count: > 0 })
            return subject.Groups.OrderBy(group => group, StringComparer.CurrentCulture).ToList();
        var groups = await _sessions.GetGroupsBySubjectAsync(subject.Id, ct);
        return groups
            .OrderBy(group => group, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<RegisterResponse> SetControlFormAsync(Actor actor, UpdateControlFormRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var form = NormalizeControlForm(request.ControlForm);
        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var group = request.Group.Trim();
        subject.ControlForm = form;
        await _subjects.UpdateAsync(subject, ct);
        var sheet = await EnsureSheetAsync(subject, group, ct);
        sheet.ControlForm = form;
        await _sheets.UpdateAsync(sheet, ct);
        return await BuildGroupAsync(subject, group, ct);
    }

    public async Task<RegisterResponse> AddColumnAsync(Actor actor, AddColumnRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var kind = request.Kind.Trim();
        if (kind is not (JournalColumns.Lecture or JournalColumns.Laboratory or JournalColumns.Practical or JournalColumns.Control))
            throw new AppException(400, "Невідомий тип колонки");

        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var settings = await _settings.GetAsync(ct);
        var group = request.Group.Trim();
        EnsureSubjectGroup(subject, group);
        if (request.MaxPoints > settings.CurrentMax)
            throw new AppException(400, $"Увага. Максимум колонки {request.MaxPoints} перевищує допустимий максимум поточних балів {settings.CurrentMax}.");

        var sessions = await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct);
        var number = sessions.Where(session => ResolveKind(session) == kind).Select(session => session.Number).DefaultIfEmpty(0).Max() + 1;
        var code = kind == JournalColumns.Control ? NextControlCode(sessions) : "";
        var legend = kind == JournalColumns.Control
            ? (string.IsNullOrWhiteSpace(request.Legend) ? code : request.Legend.Trim())
            : "";
        var date = request.Date ?? (kind == JournalColumns.Control ? null : NextLessonDate(sessions, kind));
        await _sessions.CreateAsync(new ClassSession
        {
            SubjectId = subject.Id,
            Group = group,
            Date = Dates.NormalizeUtc(date),
            GradeType = JournalColumns.ToGradeType(kind),
            ColumnKind = kind,
            Number = number,
            Code = code,
            Legend = legend,
            MaxPoints = request.MaxPoints
        }, ct);
        if (kind == JournalColumns.Control)
        {
            var sheet = await EnsureSheetAsync(subject, group, ct);
            sheet.Legend ??= [];
            if (!sheet.Legend.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
                sheet.Legend.Add(new SheetLegend { Code = code, Text = legend });
            sheet.RemovedLegendCodes?.RemoveAll(item => string.Equals(item, code, StringComparison.OrdinalIgnoreCase));
            await _sheets.UpdateAsync(sheet, ct);
        }

        if (JournalColumns.IsWork(kind) && !sessions.Any(item => JournalColumns.IsWork(ResolveKind(item))))
        {
            var sheet = await EnsureSheetAsync(subject, group, ct);
            sheet.WorkTitle = kind == JournalColumns.Practical ? "Практичні" : "Лабораторні";
            await _sheets.UpdateAsync(sheet, ct);
        }

        var students = (await _students.GetByGroupAsync(group, ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, group, students, canEdit: true, omitHidden: true, ct);
    }

    public async Task<RegisterResponse> UpdateColumnDateAsync(Actor actor, string sessionId, UpdateColumnDateRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(sessionId);
        var session = await _sessions.GetByIdAsync(sessionId, ct)
            ?? throw new AppException(404, "Колонку не знайдено");
        var kind = ResolveKind(session);
        if (kind is not (JournalColumns.Lecture or JournalColumns.Laboratory or JournalColumns.Practical))
            throw new AppException(400, "Дату можна змінити лише для лекції, практичної або лабораторної.");
        var subject = await RequireSubjectAsync(session.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        session.Date = Dates.NormalizeUtc(request.Date);
        await _sessions.UpdateAsync(session, ct);
        var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
        foreach (var grade in grades.Where(grade => grade.SessionId == session.Id))
        {
            grade.Date = session.Date;
            await _grades.UpdateAsync(grade, ct);
        }

        return await BuildGroupAsync(subject, session.Group, ct);
    }

    public async Task<RegisterResponse> DeleteColumnAsync(Actor actor, string sessionId, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(sessionId);
        var session = await _sessions.GetByIdAsync(sessionId, ct)
            ?? throw new AppException(404, "Колонку не знайдено");
        var kind = ResolveKind(session);
        if (kind is not (JournalColumns.Lecture or JournalColumns.Laboratory or JournalColumns.Practical or JournalColumns.Control))
            throw new AppException(400, "Цю колонку не можна видалити.");
        var subject = await RequireSubjectAsync(session.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        await _grades.DeleteBySessionAsync(session.Id, ct);
        await _sessions.DeleteAsync(session.Id, ct);
        if (kind == JournalColumns.Control && !string.IsNullOrWhiteSpace(session.Code))
        {
            var sheet = await EnsureSheetAsync(subject, session.Group, ct);
            sheet.Legend ??= [];
            var removed = sheet.Legend.RemoveAll(item => string.Equals(item.Code, session.Code, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                await _sheets.UpdateAsync(sheet, ct);
        }

        return await BuildGroupAsync(subject, session.Group, ct);
    }

    public async Task<RegisterResponse> HideStudentAsync(Actor actor, string subjectId, string group, string studentId, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(studentId);
        var subject = await RequireSubjectAsync(subjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var student = await _students.GetByIdAsync(studentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        group = group.Trim();
        if (!string.Equals(student.Group, group, StringComparison.Ordinal))
            throw new AppException(400, "Студент не належить до цієї групи");
        var sheet = await EnsureSheetAsync(subject, group, ct);
        sheet.HiddenStudentIds ??= [];
        if (!sheet.HiddenStudentIds.Contains(student.Id))
        {
            sheet.HiddenStudentIds.Add(student.Id);
            await _sheets.UpdateAsync(sheet, ct);
        }

        return await BuildGroupAsync(subject, group, ct);
    }

    public async Task<RegisterResponse> UpdateLegendAsync(Actor actor, UpdateLegendRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var group = request.Group.Trim();
        var sheet = await EnsureSheetAsync(subject, group, ct);
        var sessions = (await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct))
            .Where(session => ResolveKind(session) == JournalColumns.Control)
            .ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stored = new List<SheetLegend>();
        foreach (var entry in request.Entries ?? [])
        {
            var code = entry.Code?.Trim() ?? "";
            var previous = entry.PreviousCode?.Trim() ?? "";
            var text = entry.Text?.Trim() ?? "";
            if (code.Length == 0)
                throw new AppException(400, "Вкажіть код позначення.");
            if (!seen.Add(code))
                throw new AppException(400, $"Код «{code}» повторено.");
            var matchCode = previous.Length > 0 ? previous : code;
            foreach (var session in sessions.Where(session => string.Equals(session.Code, matchCode, StringComparison.OrdinalIgnoreCase)))
            {
                session.Code = code;
                session.Legend = text;
                await _sessions.UpdateAsync(session, ct);
            }

            stored.Add(new SheetLegend { Code = code, Text = text });
        }

        var removed = new HashSet<string>(sheet.RemovedLegendCodes ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var code in seen)
            removed.Remove(code);
        foreach (var session in sessions)
        {
            var code = session.Code?.Trim() ?? "";
            if (code.Length == 0 || seen.Contains(code))
                continue;
            removed.Add(code);
        }

        sheet.Legend = stored;
        sheet.RemovedLegendCodes = removed.ToList();
        sheet.LegendCustomized = true;
        await _sheets.UpdateAsync(sheet, ct);
        return await BuildGroupAsync(subject, group, ct);
    }

    public async Task<RegisterResponse> SetCellAsync(Actor actor, SetCellRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(request.StudentId);

        if (request.FinalColumn || request.Retake)
        {
            Ids.Ensure(request.SubjectId);
            var subject = await RequireSubjectAsync(request.SubjectId!, ct);
            var professor = await EnsureTeachesAsync(actor, subject, ct);
            var student = await _students.GetByIdAsync(request.StudentId, ct)
                ?? throw new AppException(404, "Студента не знайдено");
            EnsureSubjectGroup(subject, student.Group);
            if (request.Retake)
                await SaveRetakeAsync(subject, student, professor.Id, request, ct);
            else
                await SaveFinalAsync(subject, student, professor.Id, request.Mark, ct);
            return await BuildGroupAsync(subject, student.Group, ct);
        }

        Ids.Ensure(request.SessionId);
        var session = await _sessions.GetByIdAsync(request.SessionId!, ct)
            ?? throw new AppException(404, "Колонку не знайдено");
        var lessonSubject = await RequireSubjectAsync(session.SubjectId, ct);
        var lessonProfessor = await EnsureTeachesAsync(actor, lessonSubject, ct);
        var lessonStudent = await _students.GetByIdAsync(request.StudentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        if (!string.Equals(lessonStudent.Group, session.Group, StringComparison.Ordinal))
            throw new AppException(400, "Студент не належить до цієї групи");

        if (GradeTypes.IsFinal(session.GradeType) || ResolveKind(session) == "Підсумок")
        {
            await SaveFinalAsync(lessonSubject, lessonStudent, lessonProfessor.Id, request.Mark, ct);
            return await BuildGroupAsync(lessonSubject, session.Group, ct);
        }

        var parsed = ParseMark(request.Mark, allowsAbsence: true);
        var own = (await _grades.GetByStudentAsync(lessonStudent.Id, ct))
            .Where(grade => grade.SubjectId == lessonSubject.Id)
            .ToList();
        var cellGrades = own.Where(grade => grade.SessionId == session.Id).ToList();

        if (parsed.Clear)
        {
            foreach (var grade in cellGrades)
                await _grades.DeleteAsync(grade.Id, ct);
        }
        else
        {
            var others = own.Where(grade => grade.SessionId != session.Id).ToList();
            var preview = new List<Grade>(others)
            {
                new()
                {
                    StudentId = lessonStudent.Id,
                    SubjectId = lessonSubject.Id,
                    GradeType = session.GradeType,
                    GradeValue = parsed.Absent ? 0 : parsed.Points,
                    Date = session.Date,
                    SessionId = session.Id,
                    Absent = parsed.Absent
                }
            };
            var settings = await _settings.GetAsync(ct);
            var standing = GradeBook.Evaluate(preview, settings);
            var messages = new List<string>();
            if (!parsed.Absent && parsed.Points > session.MaxPoints)
                messages.Add(PointGuard.Column(lessonStudent.FullName, parsed.Points, session.MaxPoints));
            messages.AddRange(PointGuard.Describe(lessonStudent.FullName, standing, settings));
            if (messages.Count > 0)
                throw new AppException(400, PointGuard.Join(messages));

            var keeper = cellGrades.FirstOrDefault();
            if (keeper is null)
            {
                await _grades.CreateAsync(new Grade
                {
                    StudentId = lessonStudent.Id,
                    SubjectId = lessonSubject.Id,
                    ProfessorId = lessonProfessor.Id,
                    GradeValue = parsed.Absent ? 0 : parsed.Points,
                    GradeType = session.GradeType,
                    Date = session.Date,
                    SessionId = session.Id,
                    Absent = parsed.Absent
                }, ct);
            }
            else
            {
                keeper.GradeValue = parsed.Absent ? 0 : parsed.Points;
                keeper.GradeType = session.GradeType;
                keeper.Date = session.Date;
                keeper.ProfessorId = lessonProfessor.Id;
                keeper.SessionId = session.Id;
                keeper.Absent = parsed.Absent;
                await _grades.UpdateAsync(keeper, ct);
                foreach (var extra in cellGrades.Skip(1))
                    await _grades.DeleteAsync(extra.Id, ct);
            }
        }

        return await BuildGroupAsync(lessonSubject, session.Group, ct);
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
                var kind = JournalColumns.FromGradeType(bucket.Key.GradeType);
                var number = sessions.Count(item => ResolveKind(item) == kind) + 1;
                var code = "";
                string? legend = null;
                if (kind == JournalColumns.Control)
                {
                    code = bucket.Key.GradeType switch
                    {
                        GradeTypes.Homework => number == 1 ? "ДЗ" : $"ДЗ{number}",
                        GradeTypes.Module => number == 1 ? "КР" : $"КР{number}",
                        _ => $"К{number}"
                    };
                    legend = bucket.Key.GradeType;
                }

                session = new ClassSession
                {
                    SubjectId = subjectId,
                    Group = group,
                    Date = DateTime.SpecifyKind(bucket.Key.Date, DateTimeKind.Utc),
                    GradeType = bucket.Key.GradeType,
                    ColumnKind = kind,
                    Number = number,
                    Code = code,
                    Legend = legend,
                    MaxPoints = Math.Max(1, bucket.Where(grade => !grade.Absent).Select(grade => grade.GradeValue).DefaultIfEmpty(1).Max())
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

    private async Task<RegisterResponse> BuildGroupAsync(Subject subject, string group, CancellationToken ct)
    {
        var students = (await _students.GetByGroupAsync(group, ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, group, students, canEdit: true, omitHidden: true, ct);
    }

    private async Task SaveFinalAsync(Subject subject, Student student, string professorId, string? mark, CancellationToken ct)
    {
        var parsed = ParseMark(mark, allowsAbsence: false);
        var own = (await _grades.GetByStudentAsync(student.Id, ct))
            .Where(grade => grade.SubjectId == subject.Id)
            .ToList();
        var finals = own.Where(grade => GradeTypes.IsFinal(grade.GradeType))
            .OrderBy(grade => grade.Date)
            .ThenBy(grade => grade.Id, StringComparer.Ordinal)
            .ToList();
        if (parsed.Clear)
        {
            foreach (var grade in finals)
                await _grades.DeleteAsync(grade.Id, ct);
            return;
        }

        var sheet = await EnsureSheetAsync(subject, student.Group, ct);
        var gradeType = EffectiveControlForm(subject, sheet) == "Залік" ? GradeTypes.Credit : GradeTypes.Exam;
        var settings = await _settings.GetAsync(ct);
        var retake = own.FirstOrDefault(grade => GradeTypes.IsRetake(grade.GradeType));
        var preview = own.Where(grade => !GradeTypes.IsFinal(grade.GradeType) && !GradeTypes.IsRetake(grade.GradeType)).ToList();
        preview.Add(retake ?? new Grade
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            GradeType = gradeType,
            GradeValue = parsed.Points,
            Date = DateTime.UtcNow
        });
        var standing = GradeBook.Evaluate(preview, settings);
        var messages = PointGuard.Describe(student.FullName, standing, settings);
        if (retake is not null && parsed.Points > settings.FinalMax)
            messages.Add(PointGuard.Final(student.FullName, parsed.Points, settings.FinalMax));
        if (messages.Count > 0)
            throw new AppException(400, PointGuard.Join(messages));

        var keeper = finals.FirstOrDefault();
        if (keeper is null)
        {
            await _grades.CreateAsync(new Grade
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                ProfessorId = professorId,
                GradeValue = parsed.Points,
                GradeType = gradeType,
                Date = DateTime.UtcNow
            }, ct);
        }
        else
        {
            keeper.GradeValue = parsed.Points;
            keeper.GradeType = gradeType;
            keeper.ProfessorId = professorId;
            keeper.Absent = false;
            await _grades.UpdateAsync(keeper, ct);
            foreach (var extra in finals.Skip(1))
                await _grades.DeleteAsync(extra.Id, ct);
        }
    }

    private async Task SaveRetakeAsync(Subject subject, Student student, string professorId, SetCellRequest request, CancellationToken ct)
    {
        var own = (await _grades.GetByStudentAsync(student.Id, ct))
            .Where(grade => grade.SubjectId == subject.Id)
            .ToList();
        if (!own.Any(grade => GradeTypes.IsFinal(grade.GradeType)))
            throw new AppException(400, "Спочатку поставте підсумок.");

        var existing = own.Where(grade => GradeTypes.IsRetake(grade.GradeType))
            .OrderByDescending(grade => grade.Date)
            .ThenBy(grade => grade.Id, StringComparer.Ordinal)
            .ToList();
        var parsed = ParseMark(request.Mark, allowsAbsence: false);
        if (existing.Count > 0 && !request.ConfirmReplace)
            throw new AppException(400, "Перескладання вже є. Щоб поставити інше, підтвердьте видалення наявного.");
        if (parsed.Clear)
        {
            if (existing.Count == 0)
                return;
            foreach (var grade in existing)
                await _grades.DeleteAsync(grade.Id, ct);
            return;
        }

        var date = ParseRetakeDate(request.RetakeDate);
        var settings = await _settings.GetAsync(ct);
        var preview = own.Where(grade => !GradeTypes.IsRetake(grade.GradeType)).ToList();
        preview.Add(new Grade
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            GradeType = GradeTypes.Retake,
            GradeValue = parsed.Points,
            Date = date
        });
        var standing = GradeBook.Evaluate(preview, settings);
        var messages = PointGuard.Describe(student.FullName, standing, settings);
        if (messages.Count > 0)
            throw new AppException(400, PointGuard.Join(messages));

        var keeper = existing.FirstOrDefault();
        if (keeper is null)
        {
            await _grades.CreateAsync(new Grade
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                ProfessorId = professorId,
                GradeValue = parsed.Points,
                GradeType = GradeTypes.Retake,
                Date = date
            }, ct);
            return;
        }

        keeper.GradeValue = parsed.Points;
        keeper.Date = date;
        keeper.ProfessorId = professorId;
        keeper.Absent = false;
        await _grades.UpdateAsync(keeper, ct);
        foreach (var extra in existing.Skip(1))
            await _grades.DeleteAsync(extra.Id, ct);
    }

    private static DateTime ParseRetakeDate(string? text)
    {
        var value = text?.Trim() ?? "";
        if (!DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new AppException(400, "Дата перескладання має бути у форматі дд.мм.рррр.");
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }

    private async Task<JournalSheet> EnsureSheetAsync(Subject subject, string group, CancellationToken ct)
    {
        var existing = await _sheets.GetBySubjectGroupAsync(subject.Id, group, ct);
        if (existing is not null)
            return existing;

        var sessions = await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct);
        var studentIds = (await _students.GetByGroupAsync(group, ct)).Select(student => student.Id).ToHashSet();
        var grades = (await _grades.GetBySubjectAsync(subject.Id, ct))
            .Where(grade => studentIds.Contains(grade.StudentId))
            .ToList();
        var hasPractical = sessions.Any(session => ResolveKind(session) == JournalColumns.Practical);
        var hasLaboratory = sessions.Any(session => ResolveKind(session) == JournalColumns.Laboratory);
        var sheet = new JournalSheet
        {
            SubjectId = subject.Id,
            Group = group,
            Hours = Math.Max(subject.Credits, 1) * 30,
            ControlForm = grades.Any(grade => grade.GradeType == GradeTypes.Credit) ? "Залік" : "Екзамен",
            WorkTitle = hasPractical && !hasLaboratory ? "Практичні" : "Лабораторні",
            CurrentProfessorId = subject.ProfessorIds.FirstOrDefault() ?? "",
            FinalProfessorId = subject.ProfessorIds.LastOrDefault() ?? subject.ProfessorIds.FirstOrDefault() ?? ""
        };
        await _sheets.CreateAsync(sheet, ct);
        return sheet;
    }

    private async Task<RegisterResponse> BuildAsync(
        Subject subject,
        string group,
        IReadOnlyList<Student> students,
        bool canEdit,
        bool omitHidden,
        CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var sheet = await EnsureSheetAsync(subject, group, ct);
        var professorIds = new[] { sheet.CurrentProfessorId, sheet.FinalProfessorId };
        var professors = await _professors.GetByIdsAsync(professorIds, ct);
        var sessions = (await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct)).ToList();
        var lessons = sessions
            .Where(session => !GradeTypes.IsFinal(session.GradeType) && ResolveKind(session) != "Підсумок")
            .ToList();
        var lectureSessions = Ordered(lessons.Where(session => ResolveKind(session) == JournalColumns.Lecture));
        var workSessions = Ordered(lessons.Where(session => JournalColumns.IsWork(ResolveKind(session))));
        var controlSessions = Ordered(lessons.Where(session => ResolveKind(session) == JournalColumns.Control));
        var lectures = MapColumns(lectureSessions, dated: true);
        var works = MapColumns(workSessions, dated: true);
        var controls = MapColumns(controlSessions, dated: false);
        var plannedSum = lectures.Sum(column => column.MaxPoints)
            + works.Sum(column => column.MaxPoints)
            + controls.Sum(column => column.MaxPoints);
        var plannedCurrent = lectures.Count + works.Count + controls.Count == 0
            ? settings.CurrentMax
            : Math.Min(plannedSum, settings.CurrentMax);

        var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
        var hidden = sheet.HiddenStudentIds ?? [];
        var rows = new List<RegisterRowResponse>();
        var number = 1;
        foreach (var student in students)
        {
            if (omitHidden && hidden.Contains(student.Id))
                continue;
            var own = grades.Where(grade => grade.StudentId == student.Id).ToList();
            var standing = GradeBook.Evaluate(own, settings);
            var warnings = PointGuard.Describe(student.FullName, standing, settings);
            foreach (var session in lectureSessions.Concat(workSessions).Concat(controlSessions))
            {
                var taken = own.Where(grade => grade.SessionId == session.Id && !grade.Absent).ToList();
                if (taken.Count == 0)
                    continue;
                var sum = taken.Sum(grade => grade.GradeValue);
                if (sum > session.MaxPoints)
                    warnings.Add(PointGuard.Column(student.FullName, sum, session.MaxPoints));
            }

            var lectureCells = MapCells(lectureSessions, own);
            var workCells = MapCells(workSessions, own);
            var controlCells = MapCells(controlSessions, own);
            var showScores = standing.HasFinal
                || lectureCells.Concat(workCells).Concat(controlCells).Any(cell => cell.Points is not null);
            rows.Add(new RegisterRowResponse
            {
                Number = number++,
                StudentId = student.Id,
                FullName = student.FullName,
                CurrentPoints = standing.CurrentPoints,
                FinalPoints = standing.HasAttempt ? standing.AttemptPoints : null,
                RetakePoints = standing.HasRetake ? standing.RetakePoints : null,
                RetakeDate = standing.RetakeDate is DateTime retakeDate ? retakeDate.ToString("dd.MM.yyyy") : "",
                HasRetake = standing.HasRetake,
                Total = standing.Total,
                ShowScores = showScores,
                HasFinal = standing.HasAttempt,
                Debt = standing.Debt,
                WithinLimits = standing.WithinLimits,
                Ects = standing.Ects,
                NationalLabel = standing.NationalLabel,
                Warnings = warnings,
                Lectures = lectureCells,
                Works = workCells,
                Controls = controlCells
            });
        }

        var scored = rows.Where(row => row.ShowScores).Select(row => row.Total).ToList();
        return new RegisterResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Group = group,
            Hours = sheet.Hours > 0 ? sheet.Hours : subject.Credits * 30,
            ControlForm = EffectiveControlForm(subject, sheet),
            WorkTitle = string.IsNullOrWhiteSpace(sheet.WorkTitle) ? "Лабораторні" : sheet.WorkTitle,
            CurrentProfessor = professors.FirstOrDefault(professor => professor.Id == sheet.CurrentProfessorId)?.FullName ?? "",
            FinalProfessor = professors.FirstOrDefault(professor => professor.Id == sheet.FinalProfessorId)?.FullName ?? "",
            Specialty = sheet.Specialty,
            Degree = sheet.Degree,
            Semester = sheet.Semester,
            Finalized = sheet.Finalized,
            CurrentMax = settings.CurrentMax,
            FinalMax = settings.FinalMax,
            PlannedCurrentMax = plannedCurrent,
            PassThreshold = settings.PassThreshold,
            CanEdit = canEdit,
            ClassAverage = scored.Count == 0 ? null : GradeBook.Average(scored),
            DebtCount = rows.Count(row => row.Debt),
            Lectures = lectures,
            Works = works,
            Controls = controls,
            Legend = MergeLegend(sheet, controls),
            Rows = rows
        };
    }

    private static string NormalizeControlForm(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text is not ("Залік" or "Екзамен"))
            throw new AppException(400, "Форма контролю має бути «Залік» або «Екзамен».");
        return text;
    }

    private static string EffectiveControlForm(Subject subject, JournalSheet sheet)
    {
        if (subject.ControlForm is "Залік" or "Екзамен")
            return subject.ControlForm;
        if (sheet.ControlForm is "Залік" or "Екзамен")
            return sheet.ControlForm;
        return "Екзамен";
    }

    private static DateTime NextLessonDate(IEnumerable<ClassSession> sessions, string kind)
    {
        DateTime? last = null;
        foreach (var session in sessions)
        {
            if (ResolveKind(session) != kind || session.Date == default)
                continue;
            var day = session.Date.Date;
            if (last is null || day > last.Value)
                last = day;
        }

        return (last ?? DateTime.UtcNow.Date).AddDays(last is null ? 0 : 7);
    }

    private static List<LegendEntryResponse> MergeLegend(JournalSheet sheet, IReadOnlyList<RegisterColumnResponse> controls)
    {
        var result = new List<LegendEntryResponse>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in sheet.Legend ?? [])
        {
            var code = item.Code?.Trim() ?? "";
            if (code.Length == 0 || !seen.Add(code))
                continue;
            var text = item.Text?.Trim() ?? "";
            if (text.Length == 0)
                text = controls.FirstOrDefault(column => string.Equals(column.Code, code, StringComparison.OrdinalIgnoreCase))?.Legend ?? code;
            result.Add(new LegendEntryResponse { Code = code, Text = text });
        }

        var removed = new HashSet<string>(sheet.RemovedLegendCodes ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var column in controls)
        {
            if (string.IsNullOrWhiteSpace(column.Code) || removed.Contains(column.Code) || !seen.Add(column.Code))
                continue;
            result.Add(new LegendEntryResponse
            {
                Code = column.Code,
                Text = string.IsNullOrWhiteSpace(column.Legend) ? column.Code : column.Legend
            });
        }

        return result;
    }

    private static string NextControlCode(IEnumerable<ClassSession> sessions)
    {
        var used = sessions
            .Where(session => ResolveKind(session) == JournalColumns.Control)
            .Select(session => session.Code?.Trim().ToUpperInvariant())
            .ToHashSet();
        for (var index = 1; index <= 6; index++)
        {
            var code = $"C{index}";
            if (!used.Contains(code))
                return code;
        }

        throw new AppException(400, "Усі коди контролю C1–C6 вже використано.");
    }

    private static string ResolveKind(ClassSession session) =>
        string.IsNullOrWhiteSpace(session.ColumnKind)
            ? JournalColumns.FromGradeType(session.GradeType)
            : session.ColumnKind;

    private static List<ClassSession> Ordered(IEnumerable<ClassSession> sessions) =>
        sessions
            .OrderBy(session => session.Number > 0 ? session.Number : int.MaxValue)
            .ThenBy(session => session.Date)
            .ThenBy(session => session.Id, StringComparer.Ordinal)
            .ToList();

    private static List<RegisterColumnResponse> MapColumns(IReadOnlyList<ClassSession> sessions, bool dated)
    {
        var columns = new List<RegisterColumnResponse>();
        var fallback = 1;
        foreach (var session in sessions)
        {
            var kind = ResolveKind(session);
            var number = session.Number > 0 ? session.Number : fallback;
            fallback++;
            var code = session.Code?.Trim() ?? "";
            if (kind == JournalColumns.Control && code.Length == 0)
            {
                code = session.GradeType switch
                {
                    GradeTypes.Homework => number == 1 ? "ДЗ" : $"ДЗ{number}",
                    GradeTypes.Module => number == 1 ? "КР" : $"КР{number}",
                    _ => $"К{number}"
                };
            }

            columns.Add(new RegisterColumnResponse
            {
                Id = session.Id,
                Kind = kind,
                Number = number,
                Code = code,
                DateLabel = dated ? $"{session.Date.Day:00}.{session.Date.Month:00}.{session.Date.Year}" : "",
                DateValue = dated ? session.Date.ToString("yyyy-MM-dd") : "",
                MaxPoints = session.MaxPoints,
                Legend = string.IsNullOrWhiteSpace(session.Legend)
                    ? kind == JournalColumns.Control ? session.GradeType : ""
                    : session.Legend.Trim(),
                AllowsAbsence = true
            });
        }

        return columns;
    }

    private static List<RegisterCellResponse> MapCells(IReadOnlyList<ClassSession> sessions, IReadOnlyList<Grade> own)
    {
        return sessions.Select(session =>
        {
            var cell = own.Where(grade => grade.SessionId == session.Id).ToList();
            if (cell.Count == 0)
            {
                return new RegisterCellResponse { SessionId = session.Id };
            }

            if (cell.All(grade => grade.Absent))
            {
                return new RegisterCellResponse
                {
                    SessionId = session.Id,
                    Absent = true,
                    Display = "н"
                };
            }

            var points = cell.Where(grade => !grade.Absent).Sum(grade => grade.GradeValue);
            return new RegisterCellResponse
            {
                SessionId = session.Id,
                Points = points,
                Display = points.ToString()
            };
        }).ToList();
    }

    private static ParsedMark ParseMark(string? mark, bool allowsAbsence)
    {
        if (string.IsNullOrWhiteSpace(mark))
            return new ParsedMark(true, false, 0);
        var text = mark.Trim();
        if (text.Equals("н", StringComparison.OrdinalIgnoreCase))
        {
            if (!allowsAbsence)
                throw new AppException(400, "Позначку «н» можна ставити в колонках занять.");
            return new ParsedMark(false, true, 0);
        }

        if (!int.TryParse(text, out var points) || points < 0)
        {
            throw new AppException(400, allowsAbsence
                ? "Вкажіть ціле число або «н»."
                : "Вкажіть ціле невід'ємне число.");
        }

        return new ParsedMark(false, false, points);
    }

    private readonly record struct ParsedMark(bool Clear, bool Absent, int Points);

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

    private static void EnsureSubjectGroup(Subject subject, string? group)
    {
        if (subject.Groups is not { Count: > 0 })
            return;
        var value = group?.Trim() ?? "";
        if (subject.Groups.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            return;
        throw new AppException(400, $"Дисципліна «{subject.Title}» не читається в групі {value}.");
    }
}
