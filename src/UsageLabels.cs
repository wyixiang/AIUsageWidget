using System.Globalization;
using CodexBoardWidget;
namespace CodexTaskbar;
internal static class UsageLabels
{
    internal static string Plan(string? value) => string.IsNullOrWhiteSpace(value) ? "套餐暂未返回" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_',' '));
    internal static string Reset(long? seconds)
    {
        if(seconds is null or <=0 or >=253402300800) return "重置时间暂未返回";
        var reset=DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToLocalTime();
        var delta=reset-DateTimeOffset.Now;
        var left=delta.TotalSeconds<=0 ? "等待刷新" : delta.TotalDays>=1 ? $"{(int)delta.TotalDays}天{delta.Hours}小时后" : delta.TotalHours>=1 ? $"{(int)delta.TotalHours}小时{delta.Minutes}分钟后" : $"{Math.Max(1,(int)Math.Ceiling(delta.TotalMinutes))}分钟后";
        return $"{reset:MM月dd日 HH:mm} 重置 · {left}";
    }
    internal static string Summary(string provider, UsageSnapshot? snapshot)
    {
        if(snapshot is null) return provider+" · 正在读取";
        var lines=new List<string> { provider+" · "+Plan(snapshot.Plan) };
        foreach(var window in snapshot.Windows) lines.Add($"{window.Name}：剩余 {window.Remaining:0.#}%\n{Reset(window.ResetsAt)}");
        if(provider=="Cursor") lines.Add("Credit："+(snapshot.Credits??"暂不可用")+" · 独立余额");
        return string.Join("\n",lines);
    }
}
