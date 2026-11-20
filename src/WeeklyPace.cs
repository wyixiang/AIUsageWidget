using CodexBoardWidget;

namespace CodexTaskbar;

internal sealed record WeeklyPace(double ExpectedUsedPercent, double ExpectedRemainingPercent,
    double ActualUsedPercent, double RemainingWorkdays, double TotalWorkdays, string CalendarNote)
{
    internal static ChinaWorkCalendar Calendar { get; set; } = ChinaWorkCalendar.WeekdaysOnly;
    internal double Difference => Math.Round(ActualUsedPercent - ExpectedUsedPercent, 1, MidpointRounding.AwayFromZero);
    internal bool IsAhead => Difference > 0;
    internal string Comparison => Difference > 0 ? $"消耗超前 {Difference:0.#} 个百分点" :
        Difference < 0 ? $"比预期少用 {-Difference:0.#} 个百分点" : "消耗与预期相当";
    internal string ComparisonDetail => $"{Comparison} · 剩余 {RemainingWorkdays:0.##} 个工作日"
        + (TotalWorkdays == 0 ? "\n本周期全休" : "") + CalendarNote;
    internal string Description => $"工作日预期已用 {ExpectedUsedPercent:0.#}%（应剩 {ExpectedRemainingPercent:0.#}%）\n{ComparisonDetail}";
    internal string Tooltip => $"{Description}\n实际已用 {ActualUsedPercent:0.#}%\n橙红色竖线表示预期剩余额度。放假暂停，补班照常计入。";

    internal static WeeklyPace? Evaluate(UsageWindow window, DateTimeOffset now, TimeZoneInfo? zone = null, ChinaWorkCalendar? calendar = null)
    {
        if (window.Duration != 10080 || window.ResetsAt is null or <= 0 or >= 253402300800 || !double.IsFinite(window.Remaining)) return null;
        var reset = DateTimeOffset.FromUnixTimeSeconds(window.ResetsAt.Value);
        var start = reset.AddDays(-7);
        // Expired data must wait for the service; never invent the next cycle.
        if (now < start || now >= reset) return null;
        zone ??= TimeZoneInfo.Local;
        calendar ??= Calendar;
        var total = WorkingDays(start, reset, zone, calendar);
        var remaining = WorkingDays(now, reset, zone, calendar);
        var expectedRemaining = total == 0 ? 100 : Math.Clamp(remaining / total * 100, 0, 100);
        var firstYear = TimeZoneInfo.ConvertTime(start, zone).Year;
        var lastYear = TimeZoneInfo.ConvertTime(reset.AddTicks(-1), zone).Year;
        var fallbackYears = !calendar.HasSchedule(firstYear) ? firstYear.ToString() : "";
        if (lastYear != firstYear && !calendar.HasSchedule(lastYear))
            fallbackYears += (fallbackYears.Length == 0 ? "" : "、") + lastYear;
        var note = fallbackYears.Length == 0 ? "" : $"\n{fallbackYears} 年暂无班休数据，按周一至周五估算";
        return new(100 - expectedRemaining, expectedRemaining, 100 - Math.Clamp(window.Remaining, 0, 100), remaining, total, note);
    }

    private static double WorkingDays(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone, ChinaWorkCalendar calendar)
    {
        // Use local calendar days, including partial first/last days. A workday
        // has equal budget weight, including weekend make-up shifts.
        var first = TimeZoneInfo.ConvertTime(from, zone).DateTime;
        var last = TimeZoneInfo.ConvertTime(to, zone).DateTime;
        var days = 0d;
        for (var day = first.Date; day < last; day = day.AddDays(1))
        {
            if (!calendar.IsWorkday(DateOnly.FromDateTime(day))) continue;
            var begin = first > day ? first : day;
            var end = last < day.AddDays(1) ? last : day.AddDays(1);
            if (end > begin) days += (end - begin).TotalDays;
        }
        return days;
    }
}
