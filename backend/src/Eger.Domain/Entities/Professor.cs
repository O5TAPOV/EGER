namespace Eger.Domain.Entities;

public class Professor
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Department { get; set; } = null!;

    public string AcademicDegree { get; set; } = null!;
}
