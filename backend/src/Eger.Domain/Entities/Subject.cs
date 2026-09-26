namespace Eger.Domain.Entities;

public class Subject
{
    public string Id { get; set; } = null!;

    public string Title { get; set; } = null!;

    public List<string> ProfessorIds { get; set; } = [];

    public int Credits { get; set; }

    public string? ControlForm { get; set; }
}
