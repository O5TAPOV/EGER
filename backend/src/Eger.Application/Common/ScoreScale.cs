namespace Eger.Application.Common;

public static class ScoreScale
{
    public const int PassMark = 60;

    public static double ToGpa(int score) => score switch
    {
        >= 90 => 4.0,
        >= 82 => 3.5,
        >= 75 => 3.0,
        >= 67 => 2.5,
        >= 60 => 2.0,
        _ => 0.0
    };

    public static string Band(int score) => score switch
    {
        >= 90 => "Відмінно (90–100)",
        >= 75 => "Добре (75–89)",
        >= 60 => "Задовільно (60–74)",
        _ => "Незадовільно (0–59)"
    };

    public static readonly string[] Bands =
    [
        "Відмінно (90–100)",
        "Добре (75–89)",
        "Задовільно (60–74)",
        "Незадовільно (0–59)"
    ];

    public static (double AverageScore, double AverageGpa, double PassRate) Aggregate(
        IReadOnlyList<(int Score, int Credits)> rows)
    {
        if (rows.Count == 0)
            return (0, 0, 0);

        double weight = 0;
        double score = 0;
        double gpa = 0;
        var passed = 0;
        foreach (var row in rows)
        {
            var credits = row.Credits <= 0 ? 1 : row.Credits;
            weight += credits;
            score += row.Score * credits;
            gpa += ToGpa(row.Score) * credits;
            if (row.Score >= PassMark)
                passed++;
        }

        return (
            Math.Round(score / weight, 2, MidpointRounding.AwayFromZero),
            Math.Round(gpa / weight, 2, MidpointRounding.AwayFromZero),
            Math.Round(passed * 100.0 / rows.Count, 2, MidpointRounding.AwayFromZero));
    }
}
