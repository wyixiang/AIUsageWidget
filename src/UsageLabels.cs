using System.Globalization;
using CodexBoardWidget;
namespace CodexTaskbar;
internal static class UsageLabels
{
    internal static string Plan(string? value) => string.IsNullOrWhiteSpace(value) ? "套餐暂未返回" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_',' '));
    internal static string Countdown(long? seconds, long? duration, DateTimeOffset? now = null)
    {
        if(seconds is null or <=0 or >=253402300800) return "—";
        var delta = DateTimeOffset.FromUnixTimeSeconds(seconds.Value) - (now ?? DateTimeOffset.Now);
        if(delta.TotalSeconds <= 0) return "待刷新";
        if(delta.TotalSeconds <= 60) return $"{RemainingSeconds(delta)}s";
        if(duration != 300 && delta.TotalDays >= 1)
        {
            var hours = (long)Math.Floor(delta.TotalHours);
            var days = hours / 24; var remainder = hours % 24;
            return remainder > 0 ? $"{days}d {remainder}h" : $"{days}d";
        }
        var minutes = RemainingMinutes(delta);
        var totalHours = minutes / 60; var remainingMinutes = minutes % 60;
        if(totalHours == 0) return $"{remainingMinutes}m";
        return remainingMinutes > 0 ? $"{totalHours}h {remainingMinutes}m" : $"{totalHours}h";
    }
    internal static string BarCountdown(long? seconds, long? duration, DateTimeOffset? now = null)
    {
        var text = Countdown(seconds, duration, now);
        return text == "待刷新" ? "Due" : text.Replace(" ", "");
    }
    internal static string Reset(long? seconds, long? duration = null, DateTimeOffset? now = null)
    {
        if(seconds is null or <=0 or >=253402300800) return "重置时间暂未返回";
        var reset=DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToLocalTime();
        var delta=reset-(now ?? DateTimeOffset.Now);
        var minutes=RemainingMinutes(delta);
        var left=delta.TotalSeconds<=0 ? "等待刷新" : delta.TotalSeconds<=60 ? $"{RemainingSeconds(delta)}秒后" : delta.TotalDays>=1 ? $"{(int)delta.TotalDays}天{delta.Hours}小时后" : minutes>=60 ? $"{minutes/60}小时{minutes%60}分钟后" : $"{minutes}分钟后";
        return $"{reset:MM月dd日 HH:mm} 重置 · {left}";
    }
    private static long RemainingMinutes(TimeSpan remaining) => Math.Max(1, (long)Math.Ceiling(remaining.TotalMinutes));
    private static long RemainingSeconds(TimeSpan remaining) => Math.Max(1, (long)Math.Ceiling(remaining.TotalSeconds));
    internal static string Summary(string provider, UsageSnapshot? snapshot, DateTimeOffset? now = null)
    {
        if(snapshot is null) return provider+" · 正在读取";
        var lines=new List<string> { provider+" · "+Plan(snapshot.Plan) };
        foreach(var window in snapshot.Windows) lines.Add($"{window.Name}：剩余 {window.Remaining:0.#}%\n{Reset(window.ResetsAt, window.Duration, now)}");
        if(provider=="Cursor") lines.Add("Credit："+(snapshot.Credits??"暂不可用")+" · 独立余额");
        return string.Join("\n",lines);
    }
}
