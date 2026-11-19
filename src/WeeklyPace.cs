using CodexBoardWidget;

namespace CodexTaskbar;

internal sealed record WeeklyPace(double ExpectedUsedPercent, double ExpectedRemainingPercent,
    double ActualUsedPercent, double RemainingWorkdays, double TotalWorkdays)
{
    internal double Difference => Math.Round(ActualUsedPercent - ExpectedUsedPercent, 1, MidpointRounding.AwayFromZero);
    internal bool IsAhead => Difference > 0;
    internal string Comparison => Difference > 0 ? $"消耗超前 {Difference:0.#} 个百分点" :
        Difference < 0 ? $"比预期少用 {-Difference:0.#} 个百分点" : "消耗与预期相当";
    internal string Description => $"工作日预期已用 {ExpectedUsedPercent:0.#}%（应剩 {ExpectedRemainingPercent:0.#}%）\n{Comparison} · 剩余 {RemainingWorkdays:0.##} 个工作日";
    internal string Tooltip => $"{Description}\n实际已用 {ActualUsedPercent:0.#}%\n竖线表示预期剩余额度。按周一至周五均摊，周末暂停。";

    internal static WeeklyPace? Evaluate(UsageWindow window, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        if (window.Duration != 10080 || window.ResetsAt is null or <= 0 or >= 253402300800 || !double.IsFinite(window.Remaining)) return null;
        var reset = DateTimeOffset.FromUnixTimeSeconds(window.ResetsAt.Value);
        var start = reset.AddDays(-7);
        // Expired data must wait for the service; never invent the next cycle.
        if (now < start || now >= reset) return null;
        zone ??= TimeZoneInfo.Local;
        var total = WorkingDays(start, reset, zone);
        var remaining = WorkingDays(now, reset, zone);
        var expectedRemaining = total == 0 ? 100 : Math.Clamp(remaining / total * 100, 0, 100);
        return new(100 - expectedRemaining, expectedRemaining, 100 - Math.Clamp(window.Remaining, 0, 100), remaining, total);
    }

    private static double WorkingDays(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone)
    {
        // Use local calendar days, including partial first/last days. A workday
        // has equal budget weight, with no assumed office hours.
        var first = TimeZoneInfo.ConvertTime(from, zone).DateTime;
        var last = TimeZoneInfo.ConvertTime(to, zone).DateTime;
        var days = 0d;
        for (var day = first.Date; day < last; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var begin = first > day ? first : day;
            var end = last < day.AddDays(1) ? last : day.AddDays(1);
            if (end > begin) days += (end - begin).TotalDays;
        }
        return days;
    }
}
