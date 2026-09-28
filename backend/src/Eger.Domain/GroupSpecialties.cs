namespace Eger.Domain;

public static class GroupSpecialties
{
    public static string Resolve(string code, IEnumerable<string?>? recorded)
    {
        if (string.Equals(code, "3СОІ", StringComparison.OrdinalIgnoreCase))
            return "Середня освіта. Інформатика";
        if (string.Equals(code, "3СОМ", StringComparison.OrdinalIgnoreCase))
            return "Середня освіта. Математика";

        var known = recorded?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(known) ? "122 Комп'ютерні науки" : known.Trim();
    }
}
