namespace Eger.Domain;

public static class JournalColumns
{
    public const string Lecture = "Лекція";
    public const string Laboratory = "Лабораторна";
    public const string Practical = "Практична";
    public const string Control = "Контроль";

    public static string FromGradeType(string? gradeType)
    {
        if (gradeType == GradeTypes.Attendance)
            return Lecture;
        if (gradeType == GradeTypes.Practical)
            return Laboratory;
        if (GradeTypes.IsFinal(gradeType))
            return "Підсумок";
        return Control;
    }

    public static string ToGradeType(string kind) => kind switch
    {
        Lecture => GradeTypes.Attendance,
        Laboratory => GradeTypes.Practical,
        Practical => GradeTypes.Practical,
        _ => GradeTypes.Module
    };

    public static bool IsWork(string? kind) => kind is Laboratory or Practical;
}
