using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class AcademicService
{
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly ISubjectRepository _subjects;
    private readonly IGradeRepository _grades;
    private readonly IGradingSettingsRepository _settings;

    public AcademicService(
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IGradingSettingsRepository settings)
    {
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
        _settings = settings;
    }

    public async Task<StudentCardResponse> CardForUserAsync(string userId, CancellationToken ct = default)
    {
        var student = await _students.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль студента не знайдено");
        return await BuildCardAsync(student, ct);
    }

    public async Task<StudentCardResponse> CardAsync(Actor actor, string studentId, CancellationToken ct = default)
    {
        Ids.Ensure(studentId);
        var student = await _students.GetByIdAsync(studentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        await EnsureCanSeeStudentAsync(actor, student, ct);
        return await BuildCardAsync(student, ct);
    }

    public async Task<JournalResponse> JournalAsync(Actor actor, string studentId, string subjectId, CancellationToken ct = default)
    {
        Ids.Ensure(studentId);
        Ids.Ensure(subjectId);
        var student = await _students.GetByIdAsync(studentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        await EnsureCanSeeStudentAsync(actor, student, ct);
        var subject = await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        var settings = await _settings.GetAsync(ct);
        var grades = (await _grades.GetByStudentAsync(student.Id, ct))
            .Where(grade => grade.SubjectId == subject.Id)
            .ToList();
        var standing = GradeBook.Evaluate(grades, settings);
        var names = await ProfessorNameMapAsync(grades.Select(grade => grade.ProfessorId).Concat(subject.ProfessorIds), ct);

        return new JournalResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Credits = subject.Credits,
            ProfessorNames = subject.ProfessorIds.Select(id => names.GetValueOrDefault(id, "—")).ToList(),
            StudentId = student.Id,
            StudentName = student.FullName,
            Group = student.Group,
            CurrentPoints = standing.CurrentPoints,
            CurrentMax = settings.CurrentMax,
            FinalPoints = standing.FinalPoints,
            FinalMax = settings.FinalMax,
            Total = standing.Total,
            HasFinal = standing.HasFinal,
            Debt = standing.Debt,
            Ects = standing.Ects,
            NationalLabel = standing.NationalLabel,
            Outcome = standing.Outcome,
            Rows = GradeBook.Lines(grades).Select(line => new JournalRowResponse
            {
                Id = line.Id,
                Date = line.Date,
                GradeType = line.GradeType,
                Points = line.Points,
                RunningTotal = line.RunningTotal,
                ProfessorName = names.GetValueOrDefault(line.ProfessorId, "—"),
                IsFinal = line.IsFinal
            }).ToList()
        };
    }

    public async Task<StatementResponse> StatementAsync(Actor actor, string subjectId, string group, CancellationToken ct = default)
    {
        Ids.Ensure(subjectId);
        if (string.IsNullOrWhiteSpace(group))
            throw new AppException(400, "Оберіть групу");
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");

        var subject = await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        await EnsureTeachesAsync(actor, subject, ct);

        var settings = await _settings.GetAsync(ct);
        var students = (await _students.GetByGroupAsync(group.Trim(), ct))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .ToList();
        var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
        var names = await ProfessorNameMapAsync(subject.ProfessorIds.Concat(grades.Select(grade => grade.ProfessorId)), ct);
        var rows = new List<StatementStudentResponse>();
        var scored = new List<int>();
        var debtors = new List<AtRiskStudentResponse>();

        foreach (var student in students)
        {
            var own = grades.Where(grade => grade.StudentId == student.Id).ToList();
            var standing = GradeBook.Evaluate(own, settings);
            rows.Add(new StatementStudentResponse
            {
                StudentId = student.Id,
                FullName = student.FullName,
                StudentCardNumber = student.StudentCardNumber,
                CurrentPoints = standing.CurrentPoints,
                FinalPoints = standing.FinalPoints,
                FinalType = standing.FinalType,
                Total = standing.Total,
                HasFinal = standing.HasFinal,
                Debt = standing.Debt,
                CannotReach = standing.CannotReach,
                Ects = standing.Ects,
                NationalLabel = standing.NationalLabel,
                Outcome = standing.Outcome,
                Journal = MapLines(own, names)
            });

            if (own.Count > 0)
                scored.Add(standing.Total);

            if (own.Count > 0 && (standing.Debt || standing.CannotReach))
            {
                debtors.Add(RiskRow(student, subject, standing));
            }
        }

        return new StatementResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Credits = subject.Credits,
            Group = group.Trim(),
            ProfessorNames = subject.ProfessorIds.Select(id => names.GetValueOrDefault(id, "—")).ToList(),
            ClassAverage = GradeBook.Average(scored),
            PassThreshold = settings.PassThreshold,
            CurrentMax = settings.CurrentMax,
            FinalMax = settings.FinalMax,
            Students = rows,
            Debtors = debtors
        };
    }

    public async Task<AtRiskResponse> AtRiskAsync(Actor actor, CancellationToken ct = default)
    {
        if (actor.Role == Roles.Student)
            throw new AppException(403, "Недостатньо прав");

        var settings = await _settings.GetAsync(ct);
        var subjects = await VisibleSubjectsAsync(actor, ct);
        var students = await _students.GetAllAsync(ct);
        var studentMap = students.ToDictionary(student => student.Id);
        var people = new List<AtRiskStudentResponse>();
        var weakSubjects = new List<AtRiskSubjectResponse>();

        foreach (var subject in subjects)
        {
            var grades = await _grades.GetBySubjectAsync(subject.Id, ct);
            var standings = grades
                .GroupBy(grade => grade.StudentId)
                .Select(group => (StudentId: group.Key, Rows: group.ToList(), Standing: GradeBook.Evaluate(group.ToList(), settings)))
                .Where(item => item.Rows.Count > 0)
                .ToList();

            foreach (var item in standings.Where(item => item.Standing.Debt || item.Standing.CannotReach))
            {
                if (!studentMap.TryGetValue(item.StudentId, out var student))
                    continue;
                people.Add(RiskRow(student, subject, item.Standing));
            }

            var withFinal = standings.Where(item => item.Standing.HasFinal).ToList();
            if (withFinal.Count == 0)
                continue;
            var passed = withFinal.Count(item => !item.Standing.Debt);
            var share = Math.Round(passed * 100.0 / withFinal.Count, 1, MidpointRounding.AwayFromZero);
            if (share < 50)
            {
                weakSubjects.Add(new AtRiskSubjectResponse
                {
                    SubjectId = subject.Id,
                    SubjectTitle = subject.Title,
                    WithFinal = withFinal.Count,
                    Passed = passed,
                    PassShare = share,
                    AtRiskCount = standings.Count(item => item.Standing.Debt || item.Standing.CannotReach)
                });
            }
        }

        return new AtRiskResponse
        {
            PassThreshold = settings.PassThreshold,
            Students = people
                .OrderBy(item => item.Group, StringComparer.CurrentCulture)
                .ThenBy(item => item.FullName, StringComparer.CurrentCulture)
                .ToList(),
            Subjects = weakSubjects.OrderBy(item => item.PassShare).ThenBy(item => item.SubjectTitle).ToList()
        };
    }

    private async Task<StudentCardResponse> BuildCardAsync(Student student, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var grades = await _grades.GetByStudentAsync(student.Id, ct);
        var subjects = await _subjects.GetAllAsync(ct);
        var subjectMap = subjects.ToDictionary(subject => subject.Id);
        var scores = new List<SubjectScoreResponse>();

        foreach (var group in grades.GroupBy(grade => grade.SubjectId).OrderBy(group => subjectMap.GetValueOrDefault(group.Key)?.Title))
        {
            if (!subjectMap.TryGetValue(group.Key, out var subject))
                continue;
            var standing = GradeBook.Evaluate(group.ToList(), settings);
            var names = await ProfessorNameMapAsync(subject.ProfessorIds, ct);
            scores.Add(new SubjectScoreResponse
            {
                SubjectId = subject.Id,
                SubjectTitle = subject.Title,
                Credits = subject.Credits,
                ProfessorNames = subject.ProfessorIds.Select(id => names.GetValueOrDefault(id, "—")).ToList(),
                CurrentPoints = standing.CurrentPoints,
                CurrentMax = settings.CurrentMax,
                FinalPoints = standing.FinalPoints,
                FinalMax = settings.FinalMax,
                FinalType = standing.FinalType,
                Total = standing.Total,
                HasFinal = standing.HasFinal,
                Debt = standing.Debt,
                CannotReach = standing.CannotReach,
                Ects = standing.Ects,
                NationalScore = standing.NationalScore,
                NationalLabel = standing.NationalLabel,
                Outcome = standing.Outcome
            });
        }

        return new StudentCardResponse
        {
            StudentId = student.Id,
            FullName = student.FullName,
            Group = student.Group,
            StudentCardNumber = student.StudentCardNumber,
            EnrollmentYear = student.EnrollmentYear,
            AverageTotal = GradeBook.Average(scores.Select(score => score.Total)),
            PassThreshold = settings.PassThreshold,
            CurrentMax = settings.CurrentMax,
            FinalMax = settings.FinalMax,
            Subjects = scores
        };
    }

    private async Task EnsureCanSeeStudentAsync(Actor actor, Student student, CancellationToken ct)
    {
        if (actor.Role is Roles.Admin or Roles.Professor)
            return;
        if (actor.Role == Roles.Student)
        {
            var own = await _students.GetByUserIdAsync(actor.UserId, ct);
            if (own?.Id == student.Id)
                return;
        }

        throw new AppException(403, "Недостатньо прав");
    }

    private async Task EnsureTeachesAsync(Actor actor, Subject subject, CancellationToken ct)
    {
        if (actor.Role == Roles.Admin)
            return;
        if (actor.Role != Roles.Professor)
            throw new AppException(403, "Недостатньо прав");
        var professor = await _professors.GetByUserIdAsync(actor.UserId, ct)
            ?? throw new AppException(403, "Профіль викладача не знайдено");
        if (!subject.ProfessorIds.Contains(professor.Id))
            throw new AppException(403, "Ви не викладаєте цю дисципліну");
    }

    private async Task<IReadOnlyList<Subject>> VisibleSubjectsAsync(Actor actor, CancellationToken ct)
    {
        if (actor.Role == Roles.Admin)
            return await _subjects.GetAllAsync(ct);
        var professor = await _professors.GetByUserIdAsync(actor.UserId, ct)
            ?? throw new AppException(403, "Профіль викладача не знайдено");
        return await _subjects.GetByProfessorAsync(professor.Id, ct);
    }

    private async Task<Dictionary<string, string>> ProfessorNameMapAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var professors = await _professors.GetByIdsAsync(ids, ct);
        return professors.ToDictionary(professor => professor.Id, professor => professor.FullName);
    }

    private static List<JournalRowResponse> MapLines(IReadOnlyList<Grade> grades, IReadOnlyDictionary<string, string> names) =>
        GradeBook.Lines(grades).Select(line => new JournalRowResponse
        {
            Id = line.Id,
            Date = line.Date,
            GradeType = line.GradeType,
            Points = line.Points,
            RunningTotal = line.RunningTotal,
            ProfessorName = names.GetValueOrDefault(line.ProfessorId, "—"),
            IsFinal = line.IsFinal
        }).ToList();

    private static AtRiskStudentResponse RiskRow(Student student, Subject subject, SubjectStanding standing) => new()
    {
        StudentId = student.Id,
        FullName = student.FullName,
        Group = student.Group,
        SubjectId = subject.Id,
        SubjectTitle = subject.Title,
        Total = standing.Total,
        HasFinal = standing.HasFinal,
        Debt = standing.Debt,
        CannotReach = standing.CannotReach,
        Reason = standing.Debt ? "борг" : "вже не набере прохідний бал"
    };
}
