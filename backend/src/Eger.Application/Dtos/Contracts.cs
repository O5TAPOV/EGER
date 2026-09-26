using System.ComponentModel.DataAnnotations;

namespace Eger.Application.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "Електронна пошта обов'язкова")]
    [EmailAddress(ErrorMessage = "Некоректна електронна пошта")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Пароль обов'язковий")]
    public string Password { get; set; } = "";
}

public class Send2FaRequest
{
    [Required(ErrorMessage = "Ідентифікатор користувача обов'язковий")]
    public string UserId { get; set; } = "";

    [Required(ErrorMessage = "Оберіть канал надсилання")]
    public string Channel { get; set; } = "";
}

public class Verify2FaRequest
{
    [Required(ErrorMessage = "Ідентифікатор користувача обов'язковий")]
    public string UserId { get; set; } = "";

    [Required(ErrorMessage = "Код обов'язковий")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Код має містити 6 цифр")]
    public string Code { get; set; } = "";
}

public class Update2FaRequest
{
    public bool Is2FAEnabled { get; set; }

    [MaxLength(32, ErrorMessage = "Telegram Chat ID задовгий")]
    public string? TelegramChatId { get; set; }
}

public class MeResponse
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? ProfileId { get; set; }
    public bool Is2FAEnabled { get; set; }
    public string? TelegramChatId { get; set; }
    public string? Group { get; set; }
    public string? StudentCardNumber { get; set; }
    public int? EnrollmentYear { get; set; }
    public string? Department { get; set; }
    public string? AcademicDegree { get; set; }
}

public class LoginResponse
{
    public bool Requires2FA { get; set; }
    public string? UserId { get; set; }
    public string? Token { get; set; }
    public string? Message { get; set; }
    public MeResponse? User { get; set; }
}

public class CreateStudentRequest
{
    [Required(ErrorMessage = "Електронна пошта обов'язкова")]
    [EmailAddress(ErrorMessage = "Некоректна електронна пошта")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Пароль обов'язковий")]
    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "ПІБ обов'язкове")]
    [MinLength(3, ErrorMessage = "ПІБ закоротке")]
    [MaxLength(120, ErrorMessage = "ПІБ задовге")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Група обов'язкова")]
    [MaxLength(32, ErrorMessage = "Назва групи задовга")]
    public string Group { get; set; } = "";

    [Required(ErrorMessage = "Номер залікової книжки обов'язковий")]
    [MaxLength(32, ErrorMessage = "Номер залікової книжки задовгий")]
    public string StudentCardNumber { get; set; } = "";

    [Range(1991, 2100, ErrorMessage = "Рік вступу має бути між 1991 і 2100")]
    public int EnrollmentYear { get; set; }
}

public class UpdateStudentRequest
{
    [Required(ErrorMessage = "Електронна пошта обов'язкова")]
    [EmailAddress(ErrorMessage = "Некоректна електронна пошта")]
    public string Email { get; set; } = "";

    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "ПІБ обов'язкове")]
    [MinLength(3, ErrorMessage = "ПІБ закоротке")]
    [MaxLength(120, ErrorMessage = "ПІБ задовге")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Група обов'язкова")]
    [MaxLength(32, ErrorMessage = "Назва групи задовга")]
    public string Group { get; set; } = "";

    [Required(ErrorMessage = "Номер залікової книжки обов'язковий")]
    [MaxLength(32, ErrorMessage = "Номер залікової книжки задовгий")]
    public string StudentCardNumber { get; set; } = "";

    [Range(1991, 2100, ErrorMessage = "Рік вступу має бути між 1991 і 2100")]
    public int EnrollmentYear { get; set; }
}

public class StudentResponse
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Group { get; set; } = "";
    public string StudentCardNumber { get; set; } = "";
    public int EnrollmentYear { get; set; }
    public bool Is2FAEnabled { get; set; }
}

public class CreateProfessorRequest
{
    [Required(ErrorMessage = "Електронна пошта обов'язкова")]
    [EmailAddress(ErrorMessage = "Некоректна електронна пошта")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Пароль обов'язковий")]
    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "ПІБ обов'язкове")]
    [MinLength(3, ErrorMessage = "ПІБ закоротке")]
    [MaxLength(120, ErrorMessage = "ПІБ задовге")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Кафедра обов'язкова")]
    [MaxLength(120, ErrorMessage = "Назва кафедри задовга")]
    public string Department { get; set; } = "";

    [Required(ErrorMessage = "Науковий ступінь або посада обов'язкові")]
    [MaxLength(120, ErrorMessage = "Значення задовге")]
    public string AcademicDegree { get; set; } = "";
}

public class UpdateProfessorRequest
{
    [Required(ErrorMessage = "Електронна пошта обов'язкова")]
    [EmailAddress(ErrorMessage = "Некоректна електронна пошта")]
    public string Email { get; set; } = "";

    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "ПІБ обов'язкове")]
    [MinLength(3, ErrorMessage = "ПІБ закоротке")]
    [MaxLength(120, ErrorMessage = "ПІБ задовге")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Кафедра обов'язкова")]
    [MaxLength(120, ErrorMessage = "Назва кафедри задовга")]
    public string Department { get; set; } = "";

    [Required(ErrorMessage = "Науковий ступінь або посада обов'язкові")]
    [MaxLength(120, ErrorMessage = "Значення задовге")]
    public string AcademicDegree { get; set; } = "";
}

public class ProfessorResponse
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public string AcademicDegree { get; set; } = "";
    public bool Is2FAEnabled { get; set; }
}

public class SubjectRequest
{
    [Required(ErrorMessage = "Назва дисципліни обов'язкова")]
    [MaxLength(160, ErrorMessage = "Назва дисципліни задовга")]
    public string Title { get; set; } = "";

    public List<string> ProfessorIds { get; set; } = [];

    public List<string> Groups { get; set; } = [];

    public string? ControlForm { get; set; }

    [Range(1, 15, ErrorMessage = "Кількість кредитів має бути від 1 до 15")]
    public int Credits { get; set; }
}

public class SubjectResponse
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> ProfessorIds { get; set; } = [];
    public List<string> ProfessorNames { get; set; } = [];
    public List<string> Groups { get; set; } = [];
    public int Credits { get; set; }
    public string ControlForm { get; set; } = "";
}

public class GradeRequest
{
    [Required(ErrorMessage = "Студент обов'язковий")]
    public string StudentId { get; set; } = "";

    [Required(ErrorMessage = "Дисципліна обов'язкова")]
    public string SubjectId { get; set; } = "";

    public string? ProfessorId { get; set; }

    [Range(0, 100, ErrorMessage = "Бали мають бути від 0 до 100")]
    public int GradeValue { get; set; }

    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Тип оцінювання обов'язковий")]
    public string GradeType { get; set; } = "";
}

public class BulkGradeItem
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Студент обов'язковий")]
    public string StudentId { get; set; } = "";

    [Range(0, 100, ErrorMessage = "Бали мають бути від 0 до 100")]
    public int GradeValue { get; set; }

    [Required(ErrorMessage = "Тип оцінювання обов'язковий")]
    public string GradeType { get; set; } = "";

    public DateTime? Date { get; set; }
}

public class BulkGradeRequest
{
    [Required(ErrorMessage = "Дисципліна обов'язкова")]
    public string SubjectId { get; set; } = "";

    [MinLength(1, ErrorMessage = "Додайте хоча б одну оцінку")]
    public List<BulkGradeItem> Items { get; set; } = [];
}

public class GradeResponse
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public string ProfessorId { get; set; } = "";
    public string ProfessorName { get; set; } = "";
    public int GradeValue { get; set; }
    public DateTime Date { get; set; }
    public string GradeType { get; set; } = "";
    public int Credits { get; set; }
}

public class GradeGridRow
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string StudentCardNumber { get; set; } = "";
    public List<GradeResponse> Grades { get; set; } = [];
}

public class GradeGridResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Credits { get; set; }
    public string Group { get; set; } = "";
    public List<GradeGridRow> Rows { get; set; } = [];
}

public class OverviewResponse
{
    public long Students { get; set; }
    public long Professors { get; set; }
    public long Subjects { get; set; }
    public long Grades { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public int PassThreshold { get; set; }
}

public class DistributionBucket
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public class ClassAnalyticsResponse
{
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public string? Group { get; set; }
    public int StudentCount { get; set; }
    public int GradeCount { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public int PassThreshold { get; set; }
    public List<DistributionBucket> Distribution { get; set; } = [];
}

public class TranscriptItem
{
    public string GradeId { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string SubjectTitle { get; set; } = "";
    public int Credits { get; set; }
    public int GradeValue { get; set; }
    public string GradeType { get; set; } = "";
    public DateTime Date { get; set; }
    public string ProfessorName { get; set; } = "";
}

public class StudentAnalyticsResponse
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Group { get; set; } = "";
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public int PassThreshold { get; set; }
    public int GradeCount { get; set; }
    public List<TranscriptItem> Transcript { get; set; } = [];
}

public class SeedResult
{
    public bool AlreadySeeded { get; set; }
    public string Message { get; set; } = "";
    public int Students { get; set; }
    public int Professors { get; set; }
    public int Subjects { get; set; }
    public int Grades { get; set; }
}
