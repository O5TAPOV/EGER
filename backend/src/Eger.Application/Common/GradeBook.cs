using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Common;

public sealed record EctsBand(int Min, int Max, string Letter, int? NationalScore, string ExamLabel);

public sealed class SubjectStanding
{
    public int CurrentPoints { get; init; }
    public int? AttemptPoints { get; init; }
    public string? AttemptType { get; init; }
    public bool HasAttempt { get; init; }
    public int? RetakePoints { get; init; }
    public DateTime? RetakeDate { get; init; }
    public bool HasRetake { get; init; }
    public int? FinalPoints { get; init; }
    public string? FinalType { get; init; }
    public bool HasFinal { get; init; }
    public int Total { get; init; }
    public bool Debt { get; init; }
    public bool CannotReach { get; init; }
    public bool WithinLimits { get; init; }
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
        var attempt = list
            .Where(row => GradeTypes.IsFinal(row.GradeType))
            .OrderBy(row => row.Date)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var retake = list
            .Where(row => GradeTypes.IsRetake(row.GradeType))
            .OrderByDescending(row => row.Date)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var used = retake ?? attempt;
        var hasFinal = used is not null;
        var total = current + (used?.GradeValue ?? 0);
        var withinLimits = current <= settings.CurrentMax
            && (used?.GradeValue ?? 0) <= settings.FinalMax
            && total <= 100;
        var debt = hasFinal && total < settings.PassThreshold;
        var cannotReach = !hasFinal && list.Count > 0 && current + settings.FinalMax < settings.PassThreshold;

        string? ects = null;
        int? nationalScore = null;
        string? nationalLabel = null;
        string outcome;

        if (!withinLimits)
        {
            outcome = "перевищення";
        }
        else if (!hasFinal)
        {
            outcome = "набрано на зараз";
        }
        else if ((attempt?.GradeType ?? used!.GradeType) == GradeTypes.Credit)
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
            AttemptPoints = attempt?.GradeValue,
            AttemptType = attempt?.GradeType,
            HasAttempt = attempt is not null,
            RetakePoints = retake?.GradeValue,
            RetakeDate = retake?.Date,
            HasRetake = retake is not null,
            FinalPoints = used?.GradeValue,
            FinalType = attempt?.GradeType ?? used?.GradeType,
            HasFinal = hasFinal,
            Total = total,
            Debt = debt,
            CannotReach = cannotReach,
            WithinLimits = withinLimits,
            Ects = ects,
            NationalScore = nationalScore,
            NationalLabel = nationalLabel,
            Outcome = outcome
        };
    }

    public static List<JournalLine> Lines(IEnumerable<Grade> rows)
    {
        var source = rows.ToList();
        var hasRetake = source.Any(row => GradeTypes.IsRetake(row.GradeType));
        var running = 0;
        var lines = new List<JournalLine>();
        foreach (var row in source.OrderBy(row => row.Date).ThenBy(row => row.Id, StringComparer.Ordinal))
        {
            var counts = !(hasRetake && GradeTypes.IsFinal(row.GradeType));
            if (counts && !row.Absent)
                running += row.GradeValue;
            lines.Add(new JournalLine
            {
                Id = row.Id,
                Date = row.Date,
                GradeType = row.GradeType,
                Points = row.GradeValue,
                RunningTotal = running,
                ProfessorId = row.ProfessorId,
                IsFinal = GradeTypes.IsFinal(row.GradeType) || GradeTypes.IsRetake(row.GradeType)
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
