using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Common;

public sealed record EctsBand(int Min, int Max, string Letter, int? NationalScore, string ExamLabel);

public sealed class SubjectStanding
{
    public int CurrentPoints { get; init; }
    public int? FinalPoints { get; init; }
    public string? FinalType { get; init; }
    public bool HasFinal { get; init; }
    public int Total { get; init; }
    public bool Debt { get; init; }
    public bool CannotReach { get; init; }
    public string? Ects { get; init; }
    public int? NationalScore { get; init; }
    public string? NationalLabel { get; init; }
    public string Outcome { get; init; } = "";
}

public sealed class JournalLine
{
    public string Id { get; init; } = "";
    public DateTime Date { get; init; }
    public string GradeType { get; init; } = "";
    public int Points { get; init; }
    public int RunningTotal { get; init; }
    public string ProfessorId { get; init; } = "";
    public bool IsFinal { get; init; }
}

public static class GradeBook
{
    public static readonly EctsBand[] Bands =
    [
        new(90, 100, "A", 4, "відмінно"),
        new(80, 89, "B", 4, "дуже добре"),
        new(75, 79, "C", 3, "добре"),
        new(60, 74, "D", 3, "задовільно"),
        new(50, 59, "E", 3, "достатньо"),
        new(35, 49, "FX", null, "незадовільно"),
        new(1, 34, "F", null, "неприйнятно")
    ];

    public static EctsBand? BandFor(int total)
    {
        if (total > 100)
            return Bands[0];
        if (total <= 0)
            return Bands[^1];
        return Bands.FirstOrDefault(band => total >= band.Min && total <= band.Max);
    }

    public static SubjectStanding Evaluate(IEnumerable<Grade> rows, GradingSettings settings)
    {
        var list = rows.ToList();
        var current = list.Where(row => GradeTypes.IsCurrent(row.GradeType)).Sum(row => row.GradeValue);
        var final = list
            .Where(row => GradeTypes.IsFinal(row.GradeType))
            .OrderByDescending(row => row.Date)
            .FirstOrDefault();
        var hasFinal = final is not null;
        var total = current + (final?.GradeValue ?? 0);
        var debt = hasFinal && total < settings.PassThreshold;
        var cannotReach = !hasFinal && list.Count > 0 && current + settings.FinalMax < settings.PassThreshold;

        string? ects = null;
        int? nationalScore = null;
        string? nationalLabel = null;
        string outcome;

        if (!hasFinal)
        {
            outcome = "набрано на зараз";
        }
        else if (final!.GradeType == GradeTypes.Credit)
        {
            var band = BandFor(total);
            ects = band?.Letter;
            nationalLabel = total >= settings.PassThreshold ? "зараховано" : "не зараховано";
            outcome = nationalLabel;
        }
        else
        {
            var band = BandFor(total);
            ects = band?.Letter;
            nationalScore = band?.NationalScore;
            nationalLabel = band?.ExamLabel;
            outcome = nationalLabel ?? "";
        }

        return new SubjectStanding
        {
            CurrentPoints = current,
            FinalPoints = final?.GradeValue,
            FinalType = final?.GradeType,
            HasFinal = hasFinal,
            Total = total,
            Debt = debt,
            CannotReach = cannotReach,
            Ects = ects,
            NationalScore = nationalScore,
            NationalLabel = nationalLabel,
            Outcome = outcome
        };
    }

    public static List<JournalLine> Lines(IEnumerable<Grade> rows)
    {
        var running = 0;
        var lines = new List<JournalLine>();
        foreach (var row in rows.OrderBy(row => row.Date).ThenBy(row => row.Id, StringComparer.Ordinal))
        {
            running += row.GradeValue;
            lines.Add(new JournalLine
            {
                Id = row.Id,
                Date = row.Date,
                GradeType = row.GradeType,
                Points = row.GradeValue,
                RunningTotal = running,
                ProfessorId = row.ProfessorId,
                IsFinal = GradeTypes.IsFinal(row.GradeType)
            });
        }

        return lines;
    }

    public static double Average(IEnumerable<int> totals)
    {
        var list = totals.ToList();
        if (list.Count == 0)
            return 0;
        return Math.Round(list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}
