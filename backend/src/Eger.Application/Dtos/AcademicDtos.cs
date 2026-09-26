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
    public bool WithinLimits { get; set; }
    public List<string> Warnings { get; set; } = [];
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
    public bool HasInvalidSubjects { get; set; }
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

public class RegisterColumnResponse
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Number { get; set; }
    public string Code { get; set; } = "";
    public string DateLabel { get; set; } = "";
    public int MaxPoints { get; set; }
    public string Legend { get; set; } = "";
    public bool AllowsAbsence { get; set; }
}

public class LegendEntryResponse
{
    public string Code { get; set; } = "";
    public string Text { get; set; } = "";
}

public class RegisterCellResponse
{
    public string SessionId { get; set; } = "";
    public int? Points { get; set; }
    public bool Absent { get; set; }
    public string Display { get; set; } = "";
}

public class RegisterRowResponse
{
    public int Number { get; set; }
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public int CurrentPoints { get; set; }
    public int? FinalPoints { get; set; }
    public int Total { get; set; }
    public bool ShowScores { get; set; }
    public bool HasFinal { get; set; }
    public bool Debt { get; set; }
    public bool WithinLimits { get; set; }
    public string? Ects { get; set; }
    public string? NationalLabel { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<RegisterCellResponse> Lectures { get; set; } = [];
    public List<RegisterCellResponse> Works { get; set; } = [];
    public List<RegisterCellResponse> Controls { get; set; } = [];
}

public class RegisterResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public string Group { get; set; } = "";
    public int Hours { get; set; }
    public string ControlForm { get; set; } = "";
    public string WorkTitle { get; set; } = "";
    public string CurrentProfessor { get; set; } = "";
    public string FinalProfessor { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string Degree { get; set; } = "";
    public int Semester { get; set; }
    public bool Finalized { get; set; }
    public int CurrentMax { get; set; }
    public int FinalMax { get; set; }
    public int PlannedCurrentMax { get; set; }
    public int PassThreshold { get; set; }
    public bool CanEdit { get; set; }
    public double? ClassAverage { get; set; }
    public int DebtCount { get; set; }
    public List<RegisterColumnResponse> Lectures { get; set; } = [];
    public List<RegisterColumnResponse> Works { get; set; } = [];
    public List<RegisterColumnResponse> Controls { get; set; } = [];
    public List<LegendEntryResponse> Legend { get; set; } = [];
    public List<RegisterRowResponse> Rows { get; set; } = [];
}

public class AddColumnRequest
{
    [Required(ErrorMessage = "Дисципліна обов'язкова")]
    public string SubjectId { get; set; } = "";

    [Required(ErrorMessage = "Група обов'язкова")]
    public string Group { get; set; } = "";

    [Required(ErrorMessage = "Тип колонки обов'язковий")]
    public string Kind { get; set; } = "";

    public DateTime? Date { get; set; }

    public string? Code { get; set; }

    public string? Legend { get; set; }

    [Range(1, 100, ErrorMessage = "Максимум колонки має бути від 1 до 100")]
    public int MaxPoints { get; set; }
}

public class SetCellRequest
{
    public string? SessionId { get; set; }

    public string? SubjectId { get; set; }

    public bool FinalColumn { get; set; }

    [Required(ErrorMessage = "Студент обов'язковий")]
    public string StudentId { get; set; } = "";

    public string? Mark { get; set; }
}

public class UpdateSheetRequest
{
    [Required(ErrorMessage = "Дисципліна обов'язкова")]
    public string SubjectId { get; set; } = "";

    [Required(ErrorMessage = "Група обов'язкова")]
    public string Group { get; set; } = "";

    public bool Finalized { get; set; }
}
