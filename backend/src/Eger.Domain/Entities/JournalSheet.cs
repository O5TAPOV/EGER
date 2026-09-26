namespace Eger.Domain.Entities;

public class JournalSheet
{
    public string Id { get; set; } = null!;

    public string SubjectId { get; set; } = null!;

    public string Group { get; set; } = null!;

    public int Hours { get; set; }

    public string ControlForm { get; set; } = "Екзамен";

    public string WorkTitle { get; set; } = "Лабораторні";

    public string CurrentProfessorId { get; set; } = "";

    public string FinalProfessorId { get; set; } = "";

    public string Specialty { get; set; } = "122 Комп'ютерні науки";

    public string Degree { get; set; } = "бакалавр";

    public int Semester { get; set; } = 5;

    public bool Finalized { get; set; }
}