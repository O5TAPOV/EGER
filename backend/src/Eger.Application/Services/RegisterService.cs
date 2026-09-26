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
        var sheetGroup = students.Count == 0
            ? group?.Trim() ?? ""
            : students[0].Group;
        if (actor.Role != Roles.Student)
            sheetGroup = group!.Trim();

        await AttachOrphansAsync(subject.Id, sheetGroup, ct);
        await EnsureSheetAsync(subject, sheetGroup, ct);
        return await BuildAsync(subject, sheetGroup, students, canEdit, ct);
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
        return await BuildAsync(subject, group, students, canEdit: true, ct);
    }

    public async Task<RegisterResponse> AddColumnAsync(Actor actor, AddColumnRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        var kind = request.Kind.Trim();
        if (kind is not (JournalColumns.Lecture or JournalColumns.Laboratory or JournalColumns.Practical or JournalColumns.Control))
            throw new AppException(400, "Невідомий тип колонки");
        if (kind != JournalColumns.Control && request.Date is null)
            throw new AppException(400, "Вкажіть дату заняття");

        var subject = await RequireSubjectAsync(request.SubjectId, ct);
        await EnsureTeachesAsync(actor, subject, ct);
        var settings = await _settings.GetAsync(ct);
        var group = request.Group.Trim();
        if (request.MaxPoints > settings.CurrentMax)
            throw new AppException(400, $"Увага. Максимум колонки {request.MaxPoints} перевищує допустимий максимум поточних балів {settings.CurrentMax}.");

        var sessions = await _sessions.GetBySubjectGroupAsync(subject.Id, group, ct);
        var number = sessions.Where(session => ResolveKind(session) == kind).Select(session => session.Number).DefaultIfEmpty(0).Max() + 1;
        var code = kind == JournalColumns.Control
            ? (string.IsNullOrWhiteSpace(request.Code) ? $"К{number}" : request.Code.Trim())
            : "";
        await _sessions.CreateAsync(new ClassSession
        {
            SubjectId = subject.Id,
            Group = group,
            Date = Dates.NormalizeUtc(request.Date),
            GradeType = JournalColumns.ToGradeType(kind),
            ColumnKind = kind,
            Number = number,
            Code = code,
            Legend = string.IsNullOrWhiteSpace(request.Legend) ? request.Code?.Trim() : request.Legend.Trim(),
            MaxPoints = request.MaxPoints
        }, ct);

        if (JournalColumns.IsWork(kind) && !sessions.Any(item => JournalColumns.IsWork(ResolveKind(item))))
        {
            var sheet = await EnsureSheetAsync(subject, group, ct);
            sheet.WorkTitle = kind == JournalColumns.Practical ? "Практичні" : "Лабораторні";
            await _sheets.UpdateAsync(sheet, ct);
        }

        var students = (await _students.GetByGroupAsync(group, ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        return await BuildAsync(subject, group, students, canEdit: true, ct);
    }

    public async Task<RegisterResponse> SetCellAsync(Actor actor, SetCellRequest request, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");
        Ids.Ensure(request.StudentId);

        if (request.FinalColumn)
        {
            Ids.Ensure(request.SubjectId);
            var subject = await RequireSubjectAsync(request.SubjectId!, ct);
            var professor = await EnsureTeachesAsync(actor, subject, ct);
            var student = await _students.GetByIdAsync(request.StudentId, ct)
                ?? throw new AppException(404, "Студента не знайдено");
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

        var kind = ResolveKind(session);
        var parsed = ParseMark(request.Mark, kind == JournalColumns.Lecture);
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
        return await BuildAsync(subject, group, students, canEdit: true, ct);
    }

    private async Task SaveFinalAsync(Subject subject, Student student, string professorId, string? mark, CancellationToken ct)
    {
        var parsed = ParseMark(mark, allowsAbsence: false);
        var own = (await _grades.GetByStudentAsync(student.Id, ct))
            .Where(grade => grade.SubjectId == subject.Id)
            .ToList();
        var finals = own.Where(grade => GradeTypes.IsFinal(grade.GradeType)).OrderByDescending(grade => grade.Date).ToList();
        if (parsed.Clear)
        {
            foreach (var grade in finals)
                await _grades.DeleteAsync(grade.Id, ct);
            return;
        }

        var sheet = await EnsureSheetAsync(subject, student.Group, ct);
        var gradeType = sheet.ControlForm == "Залік" ? GradeTypes.Credit : GradeTypes.Exam;
        var settings = await _settings.GetAsync(ct);
        var others = own.Where(grade => !GradeTypes.IsFinal(grade.GradeType)).ToList();
        var preview = new List<Grade>(others)
        {
            new()
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                GradeType = gradeType,
                GradeValue = parsed.Points,
                Date = DateTime.UtcNow
            }
        };
        var standing = GradeBook.Evaluate(preview, settings);
        var messages = PointGuard.Describe(student.FullName, standing, settings);
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
            keeper.Date = DateTime.UtcNow;
            await _grades.UpdateAsync(keeper, ct);
            foreach (var extra in finals.Skip(1))
                await _grades.DeleteAsync(extra.Id, ct);
        }
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
        var rows = new List<RegisterRowResponse>();
        var number = 1;
        foreach (var student in students)
        {
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
                FinalPoints = standing.HasFinal ? standing.FinalPoints : null,
                Total = standing.Total,
                ShowScores = showScores,
                HasFinal = standing.HasFinal,
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
            ControlForm = sheet.ControlForm,
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
            Legend = controls
                .Where(column => !string.IsNullOrWhiteSpace(column.Code))
                .Select(column => new LegendEntryResponse
                {
                    Code = column.Code,
                    Text = string.IsNullOrWhiteSpace(column.Legend) ? column.Code : column.Legend
                })
                .ToList(),
            Rows = rows
        };
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
                DateLabel = dated ? $"{session.Date.Day:00}.{session.Date.Month:00}" : "",
                MaxPoints = session.MaxPoints,
                Legend = string.IsNullOrWhiteSpace(session.Legend)
                    ? kind == JournalColumns.Control ? session.GradeType : ""
                    : session.Legend.Trim(),
                AllowsAbsence = kind == JournalColumns.Lecture
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
                throw new AppException(400, "Позначку «н» можна ставити лише в колонках лекцій.");
            return new ParsedMark(false, true, 0);
        }

        if (!int.TryParse(text, out var points) || points < 0)
        {
            throw new AppException(400, allowsAbsence
                ? "Вкажіть ціле число або «н» для лекції."
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
}
