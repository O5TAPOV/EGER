namespace Eger.Domain.Entities;

public class Grade
{
    public string Id { get; set; } = null!;

    public string StudentId { get; set; } = null!;

    public string SubjectId { get; set; } = null!;

    public string ProfessorId { get; set; } = null!;

    public int GradeValue { get; set; }

    public DateTime Date { get; set; }

    public string GradeType { get; set; } = null!;
}
