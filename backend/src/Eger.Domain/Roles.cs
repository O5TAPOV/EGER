namespace Eger.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Professor = "Professor";
    public const string Student = "Student";

    public static readonly string[] All = [Admin, Professor, Student];
}
