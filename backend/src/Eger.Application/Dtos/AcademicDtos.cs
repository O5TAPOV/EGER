using System.ComponentModel.DataAnnotations;

namespace Eger.Application.Dtos;

public class GradingSettingsResponse
{
    public int PassThreshold { get; set; }
    public int CurrentMax { get; set; }
    public int FinalMax { get; set; }
}

public class UpdateGradingSettingsRequest
{
    [Range(1, 100, ErrorMessage = "Мінімальний бал зарахування має бути від 1 до 100")]
    public int PassThreshold { get; set; }

    [Range(1, 100, ErrorMessage = "Максимум поточних балів має бути від 1 до 100")]
    public int CurrentMax { get; set; }

    [Range(1, 100, ErrorMessage = "Максимум підсумкових балів має бути від 1 до 100")]
    public int FinalMax { get; set; }
}

public class SubjectScoreResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Credits { get; set; }
    public List<string> ProfessorNames { get; set; } = [];
    public int CurrentPoints { get; set; }
    public int CurrentMax { get; set; }
    public int? FinalPoints { get; set; }
    public int FinalMax { get; set; }
    public string? FinalType { get; set; }
    public int Total { get; set; }
    public bool HasFinal { get; set; }
    public bool Debt { get; set; }
    public bool CannotReach { get; set; }
    public string? Ects { get; set; }
    public int? NationalScore { get; set; }
    public string? NationalLabel { get; set; }
    public string Outcome { get; set; } = "";
}

public class StudentCardResponse
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Group { get; set; } = "";
    public string StudentCardNumber { get; set; } = "";
    public int EnrollmentYear { get; set; }
    public double AverageTotal { get; set; }
    public int PassThreshold { get; set; }
    public int CurrentMax { get; set; }
    public int FinalMax { get; set; }
    public List<SubjectScoreResponse> Subjects { get; set; } = [];
}

public class JournalRowResponse
{
    public string Id { get; set; } = "";
    public DateTime Date { get; set; }
    public string GradeType { get; set; } = "";
    public int Points { get; set; }
    public int RunningTotal { get; set; }
    public string ProfessorName { get; set; } = "";
    public bool IsFinal { get; set; }
}

public class JournalResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Credits { get; set; }
    public List<string> ProfessorNames { get; set; } = [];
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Group { get; set; } = "";
    public int CurrentPoints { get; set; }
    public int CurrentMax { get; set; }
    public int? FinalPoints { get; set; }
    public int FinalMax { get; set; }
    public int Total { get; set; }
    public bool HasFinal { get; set; }
    public bool Debt { get; set; }
    public string? Ects { get; set; }
    public string? NationalLabel { get; set; }
    public string Outcome { get; set; } = "";
    public List<JournalRowResponse> Rows { get; set; } = [];
}

public class StatementStudentResponse
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string StudentCardNumber { get; set; } = "";
    public int CurrentPoints { get; set; }
    public int? FinalPoints { get; set; }
    public string? FinalType { get; set; }
    public int Total { get; set; }
    public bool HasFinal { get; set; }
    public bool Debt { get; set; }
    public bool CannotReach { get; set; }
    public string? Ects { get; set; }
    public string? NationalLabel { get; set; }
    public string Outcome { get; set; } = "";
    public List<JournalRowResponse> Journal { get; set; } = [];
}

public class StatementResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Credits { get; set; }
    public string Group { get; set; } = "";
    public List<string> ProfessorNames { get; set; } = [];
    public double ClassAverage { get; set; }
    public int PassThreshold { get; set; }
    public int CurrentMax { get; set; }
    public int FinalMax { get; set; }
    public List<StatementStudentResponse> Students { get; set; } = [];
    public List<AtRiskStudentResponse> Debtors { get; set; } = [];
}

public class AtRiskStudentResponse
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Group { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Total { get; set; }
    public bool HasFinal { get; set; }
    public bool Debt { get; set; }
    public bool CannotReach { get; set; }
    public string Reason { get; set; } = "";
}

public class AtRiskSubjectResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int WithFinal { get; set; }
    public int Passed { get; set; }
    public double PassShare { get; set; }
    public int AtRiskCount { get; set; }
}

public class AtRiskResponse
{
    public int PassThreshold { get; set; }
    public List<AtRiskStudentResponse> Students { get; set; } = [];
    public List<AtRiskSubjectResponse> Subjects { get; set; } = [];
}
