namespace Eger.Domain.Entities;

public class ClassSession
{
    public string Id { get; set; } = null!;

    public string SubjectId { get; set; } = null!;

    public string Group { get; set; } = null!;

    public DateTime Date { get; set; }

    public string GradeType { get; set; } = null!;

    public string ColumnKind { get; set; } = "";

    public int Number { get; set; }

    public string? Code { get; set; }

    public string? Legend { get; set; }

    public int MaxPoints { get; set; }
}
