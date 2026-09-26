namespace Eger.Domain;

public static class GradeTypes
{
    public const string Attendance = "Відвідування";
    public const string Homework = "Домашнє завдання";
    public const string Practical = "Практична";
    public const string Module = "Модульний контроль";
    public const string Credit = "Залік";
    public const string Exam = "Екзамен";

    public static readonly string[] Current = [Attendance, Homework, Practical, Module];
    public static readonly string[] Final = [Credit, Exam];
    public static readonly string[] All = [Attendance, Homework, Practical, Module, Credit, Exam];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value);

    public static bool IsCurrent(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Current.Contains(value);

    public static bool IsFinal(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Final.Contains(value);
}
