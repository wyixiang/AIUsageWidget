using System.Globalization;

namespace CodexTaskbar;

internal sealed class ChinaWorkCalendar
{
    private readonly Dictionary<DateOnly, bool> _overrides = new();
    private readonly HashSet<int> _years = new();
    internal static ChinaWorkCalendar WeekdaysOnly { get; } = new();
    internal bool HasSchedule(int year) => _years.Contains(year);
    internal bool IsWorkday(DateOnly date) => _overrides.TryGetValue(date, out var workday)
        ? workday : date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    internal static ChinaWorkCalendar Parse(string content)
    {
        // Folded iCalendar content lines start with a single space or tab.
        var lines = content.ReplaceLineEndings("\n").Replace("\n ", "").Replace("\n\t", "").Trim().Split('\n');
        if (lines[0] != "BEGIN:VCALENDAR" || lines[^1] != "END:VCALENDAR")
            throw new InvalidDataException("Incomplete holiday calendar");

        var result = new ChinaWorkCalendar();
        bool inside = false;
        string? start = null, end = null, kind = null;
        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT")
            {
                if (inside) throw new InvalidDataException("Incomplete calendar event");
                inside = true;
                start = end = kind = null;
                continue;
            }
            if (!inside) continue;
            if (line == "END:VEVENT")
            {
                if (kind is "WORK-HOLIDAY" or "ALTERNATE-WORKDAY")
                {
                    var first = ReadDate(start);
                    var until = end is null ? first.AddDays(1) : ReadDate(end);
                    if (until <= first || until.DayNumber - first.DayNumber > 366)
                        throw new InvalidDataException("Invalid work schedule range");
                    // A year-end spillover alone does not establish an annual schedule.
                    if (first.Year == until.AddDays(-1).Year) result._years.Add(first.Year);
                    for (var date = first; date < until; date = date.AddDays(1))
                        result._overrides[date] = kind == "ALTERNATE-WORKDAY";
                }
                inside = false;
                continue;
            }
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Split(';')[0];
            switch (key)
            {
                case "DTSTART": start = line[(colon + 1)..]; break;
                case "DTEND": end = line[(colon + 1)..]; break;
                case "X-APPLE-SPECIAL-DAY": kind = line[(colon + 1)..]; break;
            }
        }
        if (inside || result._overrides.Count == 0) throw new InvalidDataException("Missing work schedule");
        return result;
    }

    private static DateOnly ReadDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : throw new InvalidDataException("Invalid work schedule date");
}
