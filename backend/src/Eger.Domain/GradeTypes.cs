namespace Eger.Domain;

public static class GradeTypes
{
    public const string Exam = "Екзамен";
    public const string Credit = "Залік";
    public const string Module = "Модульний контроль";
    public const string CourseWork = "Курсова робота";
    public const string Practice = "Практика";

    public static readonly string[] All = [Exam, Credit, Module, CourseWork, Practice];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value);
}
