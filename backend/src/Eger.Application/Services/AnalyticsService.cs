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

    public AnalyticsService(
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades)
    {
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
    }

    public async Task<OverviewResponse> OverviewAsync(CancellationToken ct = default)
    {
        var grades = await _grades.GetAllAsync(ct);
        var subjects = await _subjects.GetAllAsync(ct);
        var credits = subjects.ToDictionary(s => s.Id, s => s.Credits);
        var stats = ScoreScale.Aggregate(grades.Select(g => (g.GradeValue, credits.GetValueOrDefault(g.SubjectId, 1))).ToList());

        return new OverviewResponse
        {
            Students = await _students.CountAsync(ct),
            Professors = await _professors.CountAsync(ct),
            Subjects = await _subjects.CountAsync(ct),
            Grades = await _grades.CountAsync(ct),
            AverageScore = stats.AverageScore,
            AverageGpa = stats.AverageGpa,
            PassRate = stats.PassRate
        };
    }

    public async Task<ClassAnalyticsResponse> SubjectAsync(Actor actor, string subjectId, string? group, CancellationToken ct = default)
    {
        Ids.Ensure(subjectId);
        var subject = await _subjects.GetByIdAsync(subjectId, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");

        if (actor.Role == Roles.Professor)
        {
            var professor = await _professors.GetByUserIdAsync(actor.UserId, ct)
                ?? throw new AppException(403, "Профіль викладача не знайдено");
            if (!subject.ProfessorIds.Contains(professor.Id))
                throw new AppException(403, "Ви не викладаєте цю дисципліну");
        }
        else if (actor.Role != Roles.Admin)
        {
            throw new AppException(403, "Недостатньо прав");
        }

        var students = string.IsNullOrWhiteSpace(group)
            ? await _students.GetAllAsync(ct)
            : await _students.GetByGroupAsync(group.Trim(), ct);
        var allowed = students.Select(s => s.Id).ToHashSet();
        var grades = (await _grades.GetBySubjectAsync(subject.Id, ct))
            .Where(g => allowed.Contains(g.StudentId))
            .ToList();

        var stats = ScoreScale.Aggregate(grades.Select(g => (g.GradeValue, subject.Credits)).ToList());
        var buckets = ScoreScale.Bands
            .Select(label => new DistributionBucket
            {
                Label = label,
                Count = grades.Count(g => ScoreScale.Band(g.GradeValue) == label)
            })
            .ToList();

        return new ClassAnalyticsResponse
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim(),
            StudentCount = string.IsNullOrWhiteSpace(group)
                ? grades.Select(g => g.StudentId).Distinct().Count()
                : students.Count,
            GradeCount = grades.Count,
            AverageScore = stats.AverageScore,
            AverageGpa = stats.AverageGpa,
            PassRate = stats.PassRate,
            Distribution = buckets
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
        var grades = await _grades.GetByStudentAsync(student.Id, ct);
        var subjects = await _subjects.GetAllAsync(ct);
        var subjectMap = subjects.ToDictionary(s => s.Id);
        var professors = await _professors.GetByIdsAsync(grades.Select(g => g.ProfessorId), ct);
        var professorNames = professors.ToDictionary(p => p.Id, p => p.FullName);

        var transcript = grades
            .OrderByDescending(g => g.Date)
            .Select(g =>
            {
                subjectMap.TryGetValue(g.SubjectId, out var subject);
                return new TranscriptItem
                {
                    GradeId = g.Id,
                    SubjectId = g.SubjectId,
                    SubjectTitle = subject?.Title ?? "—",
                    Credits = subject?.Credits ?? 0,
                    GradeValue = g.GradeValue,
                    GradeType = g.GradeType,
                    Date = g.Date,
                    ProfessorName = professorNames.TryGetValue(g.ProfessorId, out var name) ? name : "—",
                    GpaPoints = ScoreScale.ToGpa(g.GradeValue)
                };
            })
            .ToList();

        var stats = ScoreScale.Aggregate(transcript.Select(t => (t.GradeValue, t.Credits)).ToList());
        return new StudentAnalyticsResponse
        {
            StudentId = student.Id,
            FullName = student.FullName,
            Group = student.Group,
            AverageScore = stats.AverageScore,
            AverageGpa = stats.AverageGpa,
            PassRate = stats.PassRate,
            GradeCount = transcript.Count,
            Transcript = transcript
        };
    }
}
