using Eger.Domain.Entities;

namespace Eger.Application.Common;

public static class PointGuard
{
    public static string Column(string fullName, int points, int maxPoints) =>
        $"Увага. Бал студента {fullName} становить {points} і перевищує максимум колонки {maxPoints}.";

    public static string Current(string fullName, int current, int currentMax) =>
        $"Увага. Поточні бали студента {fullName} становлять {current} і перевищують максимум {currentMax}. Перевірте поточні роботи.";

    public static string Final(string fullName, int final, int finalMax) =>
        $"Увага. Підсумок студента {fullName} становить {final} і перевищує максимум {finalMax}. Перевірте підсумковий контроль.";

    public static string Total(string fullName, int total) =>
        $"Увага. Сума балів студента {fullName} становить {total} і перевищує 100. Перевірте поточні роботи та підсумковий контроль.";

    public static List<string> Describe(string fullName, SubjectStanding standing, GradingSettings settings)
    {
        var messages = new List<string>();
        if (standing.CurrentPoints > settings.CurrentMax)
            messages.Add(Current(fullName, standing.CurrentPoints, settings.CurrentMax));
        if (standing.HasFinal && standing.FinalPoints is int final && final > settings.FinalMax)
            messages.Add(Final(fullName, final, settings.FinalMax));
        if (standing.Total > 100)
            messages.Add(Total(fullName, standing.Total));
        return messages;
    }

    public static string Join(IEnumerable<string> messages) => string.Join("\n", messages);
}
