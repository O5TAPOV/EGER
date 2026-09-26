namespace Eger.Application.Common;

public static class Dates
{
    public static DateTime NormalizeUtc(DateTime? value)
    {
        var date = value ?? DateTime.UtcNow;
        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
    }
}
