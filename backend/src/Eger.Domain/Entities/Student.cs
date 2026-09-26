namespace Eger.Domain.Entities;

public class Student
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Group { get; set; } = null!;

    public string StudentCardNumber { get; set; } = null!;

    public int EnrollmentYear { get; set; }
}
