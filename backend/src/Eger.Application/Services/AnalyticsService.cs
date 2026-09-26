using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class AnalyticsService
{
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly ISubjectRepository _subjects;
    private readonly IGradeRepository _grades;
    private readonly IGradingSettingsRepository _settings;

    public AnalyticsService(
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

    public async Task<OverviewResponse> OverviewAsync(CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);
        var grades = await _grades.GetAllAsync(ct);
        var standings = grades
            .GroupBy(grade => (grade.StudentId, grade.SubjectId))
            .Select(group => GradeBook.Evaluate(group.ToList(), settings))
            .ToList();
        var withFinal = standings.Where(item => item.HasFinal).ToList();

        return new OverviewResponse
        {
            Students = await _students.CountAsync(ct),
            Professors = await _professors.CountAsync(ct),
            Subjects = await _subjects.CountAsync(ct),
            Grades = grades.Count,
            AverageScore = GradeBook.Average(standings.Select(item => item.Total)),
            PassRate = Share(withFinal.Count(item => !item.Debt), withFinal.Count),
            PassThreshold = settings.PassThreshold
        };
    }

    public async Task<ClassAnalyticsResponse> SubjectAsync(Actor actor, string subjectId, string? group, CancellationToken ct = default)
    {
        Ids.Ensure(subjectId);
        var subject = await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        await EnsureCanSeeSubjectAsync(actor, subject, ct);

        var settings = await _settings.GetAsync(ct);
        var students = string.IsNullOrWhiteSpace(group)
            ? await _students.GetAllAsync(ct)
            : await _students.GetByGroupAsync(group.Trim(), ct);
        var allowed = students.Select(student => student.Id).ToHashSet();
        var grades = (await _grades.GetBySubjectAsync(subject.Id, ct))
            .Where(grade => allowed.Contains(grade.StudentId))
            .ToList();
        var standings = grades
            .GroupBy(grade => grade.StudentId)
            .Select(group => GradeBook.Evaluate(group.ToList(), settings))
            .ToList();
        var withFinal = standings.Where(item => item.HasFinal).ToList();

        return new ClassAnalyticsResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim(),
            StudentCount = string.IsNullOrWhiteSpace(group)
                ? grades.Select(grade => grade.StudentId).Distinct().Count()
                : students.Count,
            GradeCount = grades.Count,
            AverageScore = GradeBook.Average(standings.Select(item => item.Total)),
            PassRate = Share(withFinal.Count(item => !item.Debt), withFinal.Count),
            PassThreshold = settings.PassThreshold,
            Distribution = GradeBook.Bands
                .Select(band => new DistributionBucket
                {
                    Label = $"{band.Letter} {band.Min}–{band.Max}",
                    Count = withFinal.Count(item => item.Ects == band.Letter)
                })
                .ToList()
        };
    }

    public async Task<StudentAnalyticsResponse> ForUserAsync(string userId, CancellationToken ct = default)
    {
        var student = await _students.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль студента не знайдено");
        return await ForStudentAsync(student, ct);
    }

    public async Task<StudentAnalyticsResponse> ForStudentIdAsync(Actor actor, string studentId, CancellationToken ct = default)
    {
        Ids.Ensure(studentId);
        var student = await _students.GetByIdAsync(studentId, ct)
            ?? throw new AppException(404, "Студента не знайдено");

        if (actor.Role == Roles.Student)
        {
            var own = await _students.GetByUserIdAsync(actor.UserId, ct);
            if (own?.Id != student.Id)
                throw new AppException(403, "Недостатньо прав");
        }
        else if (actor.Role is not (Roles.Admin or Roles.Professor))
        {
            throw new AppException(403, "Недостатньо прав");
        }

        return await ForStudentAsync(student, ct);
    }

    private async Task<StudentAnalyticsResponse> ForStudentAsync(Student student, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var grades = await _grades.GetByStudentAsync(student.Id, ct);
        var subjects = await _subjects.GetAllAsync(ct);
        var subjectMap = subjects.ToDictionary(subject => subject.Id);
        var professors = await _professors.GetByIdsAsync(grades.Select(grade => grade.ProfessorId), ct);
        var professorNames = professors.ToDictionary(professor => professor.Id, professor => professor.FullName);
        var standings = grades
            .GroupBy(grade => grade.SubjectId)
            .Select(group => GradeBook.Evaluate(group.ToList(), settings))
            .ToList();
        var withFinal = standings.Where(item => item.HasFinal).ToList();

        return new StudentAnalyticsResponse
        {
            StudentId = student.Id,
            FullName = student.FullName,
            Group = student.Group,
            AverageScore = GradeBook.Average(standings.Select(item => item.Total)),
            PassRate = Share(withFinal.Count(item => !item.Debt), withFinal.Count),
            PassThreshold = settings.PassThreshold,
            GradeCount = grades.Count,
            Transcript = grades
                .OrderBy(grade => grade.Date)
                .Select(grade =>
                {
                    subjectMap.TryGetValue(grade.SubjectId, out var subject);
                    return new TranscriptItem
                    {
                        GradeId = grade.Id,
                        SubjectId = grade.SubjectId,
                        SubjectTitle = subject?.Title ?? "—",
                        Credits = subject?.Credits ?? 0,
                        GradeValue = grade.GradeValue,
                        GradeType = grade.GradeType,
                        Date = grade.Date,
                        ProfessorName = professorNames.TryGetValue(grade.ProfessorId, out var name) ? name : "—"
                    };
                })
                .ToList()
        };
    }

    private async Task EnsureCanSeeSubjectAsync(Actor actor, Subject subject, CancellationToken ct)
    {
        if (actor.Role == Roles.Professor)
        {
            var professor = await _professors.GetByUserIdAsync(actor.UserId, ct)
                ?? throw new AppException(403, "Профіль викладача не знайдено");
            if (!subject.ProfessorIds.Contains(professor.Id))
                throw new AppException(403, "Ви не викладаєте цю дисципліну");
            return;
        }

        if (actor.Role != Roles.Admin)
            throw new AppException(403, "Недостатньо прав");
    }

    private static double Share(int passed, int total) =>
        total == 0 ? 0 : Math.Round(passed * 100.0 / total, 1, MidpointRounding.AwayFromZero);
}
