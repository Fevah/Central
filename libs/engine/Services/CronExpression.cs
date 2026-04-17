namespace Central.Engine.Services;

/// <summary>
/// Lightweight cron expression parser for job scheduling.
/// Supports standard 5-field format: minute hour day-of-month month day-of-week
/// Examples:
///   "0 */6 * * *"    → every 6 hours at :00
///   "30 2 * * *"     → daily at 02:30
///   "0 0 * * 1"      → every Monday at midnight
///   "*/15 * * * *"   → every 15 minutes
///   "0 9-17 * * 1-5" → hourly 9am-5pm weekdays
/// </summary>
public class CronExpression
{
    private readonly int[] _minutes;
    private readonly int[] _hours;
    private readonly int[] _daysOfMonth;
    private readonly int[] _months;
    private readonly int[] _daysOfWeek;

    private CronExpression(int[] minutes, int[] hours, int[] daysOfMonth, int[] months, int[] daysOfWeek)
    {
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
    }

    /// <summary>Parse a cron expression string.</summary>
    public static CronExpression Parse(string expression)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new FormatException($"Cron expression must have 5 fields, got {parts.Length}: '{expression}'");

        return new CronExpression(
            ParseField(parts[0], 0, 59),
            ParseField(parts[1], 0, 23),
            ParseField(parts[2], 1, 31),
            ParseField(parts[3], 1, 12),
            ParseField(parts[4], 0, 6) // 0=Sunday
        );
    }

    /// <summary>Try parse without throwing.</summary>
    public static bool TryParse(string expression, out CronExpression? result)
    {
        try { result = Parse(expression); return true; }
        catch { result = null; return false; }
    }

    /// <summary>Check if the given time matches this cron expression.</summary>
    public bool Matches(DateTime time)
    {
        return _minutes.Contains(time.Minute)
            && _hours.Contains(time.Hour)
            && _daysOfMonth.Contains(time.Day)
            && _months.Contains(time.Month)
            && _daysOfWeek.Contains((int)time.DayOfWeek);
    }

    /// <summary>Get the next occurrence after the given time.</summary>
    public DateTime? GetNextOccurrence(DateTime after, int maxSearchMinutes = 525960) // ~1 year
    {
        var candidate = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0).AddMinutes(1);
        var limit = after.AddMinutes(maxSearchMinutes);

        while (candidate < limit)
        {
            if (Matches(candidate)) return candidate;

            // Skip efficiently — if month doesn't match, jump to next month
            if (!_months.Contains(candidate.Month))
            {
                candidate = new DateTime(candidate.Year, candidate.Month, 1).AddMonths(1);
                continue;
            }
            // If day doesn't match, jump to next day
            if (!_daysOfMonth.Contains(candidate.Day) || !_daysOfWeek.Contains((int)candidate.DayOfWeek))
            {
                candidate = candidate.Date.AddDays(1);
                continue;
            }
            // If hour doesn't match, jump to next hour
            if (!_hours.Contains(candidate.Hour))
            {
                candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day, candidate.Hour, 0, 0).AddHours(1);
                continue;
            }
            candidate = candidate.AddMinutes(1);
        }
        return null;
    }

    public override string ToString()
    {
        return $"{FormatField(_minutes)} {FormatField(_hours)} {FormatField(_daysOfMonth)} {FormatField(_months)} {FormatField(_daysOfWeek)}";
    }

    // ── Parsing ──

    private static int[] ParseField(string field, int min, int max)
    {
        if (field == "*") return Enumerable.Range(min, max - min + 1).ToArray();

        var values = new HashSet<int>();
        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                var start = stepParts[0] == "*" ? min : int.Parse(stepParts[0]);
                var step = int.Parse(stepParts[1]);
                for (int i = start; i <= max; i += step) values.Add(i);
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var rangeStart = int.Parse(rangeParts[0]);
                var rangeEnd = int.Parse(rangeParts[1]);
                for (int i = rangeStart; i <= rangeEnd; i++) values.Add(i);
            }
            else
            {
                values.Add(int.Parse(part));
            }
        }
        return values.Where(v => v >= min && v <= max).OrderBy(v => v).ToArray();
    }

    private static string FormatField(int[] values)
    {
        if (values.Length == 0) return "*";
        return string.Join(",", values);
    }
}
