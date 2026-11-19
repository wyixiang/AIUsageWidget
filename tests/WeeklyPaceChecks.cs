using CodexBoardWidget;
using CodexTaskbar;

internal static class WeeklyPaceChecks
{
    internal static void Run()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test+08", TimeSpan.FromHours(8), "test+08", "test+08");
        DateTimeOffset At(int day, int hour = 0) => new(2026, 11, day, hour, 0, 0, TimeSpan.FromHours(8));
        UsageWindow Window(DateTimeOffset reset, double remaining = 50, long duration = 10080)
            => new("周", remaining, reset.ToUnixTimeSeconds(), duration);
        WeeklyPace Pace(DateTimeOffset now, double remaining = 50) => WeeklyPace.Evaluate(Window(At(23), remaining), now, zone)!;
        Check(Near(Pace(At(16)).ExpectedUsedPercent, 0) && Near(Pace(At(16)).TotalWorkdays, 5), "weekly cycle starts with all five workdays available");
        Check(Near(Pace(At(18)).ExpectedUsedPercent, 40) && Near(Pace(At(18)).RemainingWorkdays, 3), "Wednesday budget reflects the remaining three workdays");
        Check(Near(Pace(At(18, 12)).ExpectedUsedPercent, 50), "partial workdays advance the budget smoothly");
        Check(Near(Pace(At(21)).ExpectedUsedPercent, 100) && Near(Pace(At(22, 12)).ExpectedUsedPercent, 100), "weekend hours do not advance the workday budget");
        Check(Pace(At(18), 50).IsAhead && Near(Pace(At(18), 50).Difference, 10), "actual consumption ahead of the weekday budget is flagged in percentage points");
        Check(!Pace(At(18), 70).IsAhead && Near(Pace(At(18), 70).Difference, -10), "remaining quota above the marker is within budget");
        Check(!Pace(At(18), 60).IsAhead && Near(Pace(At(18), 60).Difference, 0), "usage matching the workday budget is not marked ahead");
        Check(WeeklyPace.Evaluate(Window(At(23)), At(23), zone) is null && WeeklyPace.Evaluate(Window(At(23)), At(24), zone) is null,
            "expired reset waits for fresh service data instead of inventing another cycle");
        Check(WeeklyPace.Evaluate(Window(At(23)), At(15), zone) is null, "future cycle outside the current weekly window has no fabricated marker");
        Check(WeeklyPace.Evaluate(Window(At(23), duration: 300), At(18), zone) is null, "five-hour quota is excluded from weekly pacing");
        Check(WeeklyPace.Evaluate(new("周", 50, null, 10080), At(18), zone) is null &&
              WeeklyPace.Evaluate(new("周", 50, 253402300800, 10080), At(18), zone) is null, "missing or invalid reset time has no marker");
        var midweekReset = At(19, 12);
        var halfCycle = WeeklyPace.Evaluate(Window(midweekReset), At(17), zone)!;
        Check(Near(halfCycle.TotalWorkdays, 5) && Near(halfCycle.ExpectedUsedPercent, 50), "each provider uses its own non-midnight reset rather than a calendar-week boundary");
        Check(Near(WeeklyPace.Evaluate(Window(At(23)), At(18).ToUniversalTime(), zone)!.ExpectedUsedPercent, 40), "UTC reset timestamps are evaluated in the chosen local calendar");
        Check(WeeklyPace.Evaluate(Window(At(23), double.NaN), At(18), zone) is null, "invalid actual consumption cannot create a false warning");
        Check(Near(WeeklyPace.Evaluate(Window(At(21)), At(15, 12), zone)!.ExpectedUsedPercent, 0), "a weekend reset keeps the new cycle budget untouched until Monday");
        var summary = UsageLabels.Summary("Cursor", new([Window(DateTimeOffset.Now.AddDays(2))], "super", null, DateTimeOffset.Now), DateTimeOffset.Now);
        Check(summary.Contains("工作日预期") && summary.Contains("应剩"), "hover summary explains the marker using remaining-quota semantics");
    }
    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.000001;
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
