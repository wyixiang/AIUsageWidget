using System.Text.Json;
using CodexTaskbar;

static JsonElement Json(string json) { using var doc=JsonDocument.Parse(json); return doc.RootElement.Clone(); }
static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS: "+name); }
var usage=Json("""{"billingCycleEnd":"1896000000000","planUsage":{"autoPercentUsed":42.1}}""");
var plan=Json("""{"planInfo":{"planName":"Super","includedUsagePeriod":"INCLUDED_USAGE_PERIOD_WEEKLY","includedUsageResetsAt":"1893542400000"}}""");
var balance=Json("""{"usdRemainingCents":"3754"}""");
var result=CursorClient.Parse(usage,plan,balance);
Check(result.Plan=="Super" && result.Windows.Length==1 && result.Windows[0].Name=="本周额度", "weekly subscription names and plan");
Check(Math.Abs(result.Windows[0].Remaining-57.9)<0.0001, "used percentage is converted to remaining");
Check(result.Windows[0].ResetsAt==1893542400, "weekly reset uses included period, not monthly billing");
Check(result.Credits=="$37.54", "credit cents converted to dollars");
var noReset=Json("""{"planInfo":{"planName":"Super","includedUsagePeriod":2}}""");
Check(CursorClient.Parse(usage,noReset).Windows[0].ResetsAt is null, "missing weekly reset remains unknown");
Check(CursorClient.Parse(usage,plan).Credits is null, "missing credit balance remains unknown");
Check(CursorClient.Parse(usage,plan,Json("""{"usdRemainingCents":"0"}""")).Credits=="$0.00", "zero credit is shown explicitly");
Check(CursorClient.Parse(usage,plan,null,Json("""{"eligible":true,"availableCents":"1250"}""")).Credits=="$12.50", "prepaid balance fallback");
Check(CursorClient.Parse(usage,plan,null,Json("""{"eligible":false,"availableCents":"0"}""")).Credits is null, "ineligible prepaid account is not shown as zero credit");
Check(CursorClient.Parse(usage,plan,Json("""{"usdRemainingCents":"0"}"""),Json("""{"eligible":true,"availableCents":"3754"}""")).Credits=="$37.54", "Cursor prepaid balance takes priority over separate commerce wallet");
var now=DateTimeOffset.UtcNow;
var monitorChoice=new MonitorSelection();
var before=new[]{new MonitorCandidate("DISPLAY1","physical-right",false),new MonitorCandidate("DISPLAY2","physical-left",true)};
var after=new[]{new MonitorCandidate("DISPLAY1","physical-left",true),new MonitorCandidate("DISPLAY2","physical-right",false)};
Check(monitorChoice.Resolve(before,"physical-right","DISPLAY1",now)?.DeviceName=="DISPLAY1", "original physical monitor selected");
Check(monitorChoice.Resolve(after,"physical-right","DISPLAY1",now)?.DeviceName=="DISPLAY2", "monitor renumbering follows physical identity");
monitorChoice.BeginReconnect(now);
var onlyLeft=new[]{new MonitorCandidate("DISPLAY1","physical-left",true)};
Check(monitorChoice.Resolve(onlyLeft,"physical-right","DISPLAY1",now) is null, "wake-up waits instead of jumping to recycled display number");
Check(monitorChoice.Resolve(onlyLeft,"physical-right","DISPLAY1",now.AddSeconds(14)) is null, "reconnect grace period remains active");
Check(monitorChoice.Resolve(onlyLeft,"physical-right","DISPLAY1",now.AddSeconds(16))?.Identity=="physical-left", "real disconnection permits temporary primary fallback");
Check(monitorChoice.Resolve(after,"physical-right","DISPLAY1",now.AddSeconds(17))?.Identity=="physical-right", "preferred physical monitor restored when it reconnects");
Check(monitorChoice.Resolve(after,null,"DISPLAY2",now)?.Identity=="physical-right", "legacy preference remains migratable");
var fixedNow=DateTimeOffset.FromUnixTimeSeconds(1900000000);
Check(UsageLabels.Countdown(fixedNow.AddHours(3).AddMinutes(25).ToUnixTimeSeconds(),300,fixedNow)=="3h 25m", "five-hour reset uses hours and minutes");
Check(UsageLabels.Countdown(fixedNow.AddDays(6).AddHours(2).ToUnixTimeSeconds(),10080,fixedNow)=="6d 2h", "weekly reset uses days and hours");
Check(UsageLabels.Countdown(fixedNow.AddHours(12).ToUnixTimeSeconds(),10080,fixedNow)=="12h", "weekly reset below one day drops the zero-day unit");
Check(UsageLabels.Countdown(fixedNow.AddHours(26).AddMinutes(13).ToUnixTimeSeconds(),300,fixedNow)=="26h 13m", "five-hour period keeps hour-minute units across a day boundary");
Check(UsageLabels.Countdown(fixedNow.AddSeconds(30).ToUnixTimeSeconds(),300,fixedNow)=="30s", "sub-minute duration switches to seconds");
Check(UsageLabels.Countdown(fixedNow.ToUnixTimeSeconds(),300,fixedNow)=="待刷新", "elapsed reset waits for server refresh instead of wrapping");
Check(UsageLabels.Countdown(null,10080,fixedNow)=="—", "missing reset is not fabricated");
Check(UsageLabels.Countdown(fixedNow.AddHours(12).AddMinutes(30).ToUnixTimeSeconds(),10080,fixedNow)=="12h 30m", "weekly reset below one day uses hours and minutes");
Check(UsageLabels.Countdown(fixedNow.AddMinutes(35).ToUnixTimeSeconds(),300,fixedNow)=="35m", "five-hour reset below an hour displays minutes only");
Check(UsageLabels.Countdown(fixedNow.AddMinutes(35).ToUnixTimeSeconds(),10080,fixedNow)=="35m", "weekly reset below an hour displays minutes only");
Check(UsageLabels.Countdown(fixedNow.AddDays(2).ToUnixTimeSeconds(),10080,fixedNow)=="2d", "weekly reset drops zero hours after days");
Check(UsageLabels.Countdown(fixedNow.AddHours(3).ToUnixTimeSeconds(),300,fixedNow)=="3h", "hour reset drops zero minutes");
Check(UsageLabels.Reset(fixedNow.AddHours(3).AddMinutes(25).ToUnixTimeSeconds(),300,fixedNow).EndsWith("3小时25分钟后"), "details keep Chinese hour-minute countdown");
Check(UsageLabels.Reset(fixedNow.AddDays(6).AddHours(2).ToUnixTimeSeconds(),10080,fixedNow).EndsWith("6天2小时后"), "details keep Chinese day-hour countdown");
Check(UsageLabels.ResetSuffix(fixedNow.AddMinutes(35).ToUnixTimeSeconds(),300,fixedNow)=="35m后", "taskbar reset annotation ends in after suffix");
Check(UsageLabels.ResetSuffix(fixedNow.AddDays(5).AddHours(6).ToUnixTimeSeconds(),10080,fixedNow)=="5d6h后", "weekly taskbar reset annotation keeps compact units without padding");
Check(UsageLabels.ResetSuffix(null,10080,fixedNow)=="—", "unknown reset does not get misleading after suffix");
Check(UsageLabels.ResetSuffix(fixedNow.ToUnixTimeSeconds(),300,fixedNow)=="待刷新", "elapsed reset remains a refresh status");
Check(UsageLabels.ResetSuffix(fixedNow.AddHours(4).AddMinutes(51).ToUnixTimeSeconds(),300,fixedNow)=="4h51m后", "hour-minute taskbar reset has natural compact spacing");
Check(UsageLabels.ResetSuffix(fixedNow.AddDays(6).AddHours(23).ToUnixTimeSeconds(),10080,fixedNow)=="6d23h后", "day-hour taskbar reset has natural compact spacing");
var partialMinuteReset=fixedNow.AddMinutes(17).AddSeconds(20).ToUnixTimeSeconds();
foreach (var period in new long[] {300,10080})
{
    foreach (var remainingSeconds in new[] {60,59,30,1})
    {
        var reset=fixedNow.AddSeconds(remainingSeconds).ToUnixTimeSeconds();
        Check(UsageLabels.ResetSuffix(reset,period,fixedNow)==$"{remainingSeconds}s后" && UsageLabels.Reset(reset,period,fixedNow).EndsWith($"{remainingSeconds}秒后"), $"{period}-minute period agrees on seconds at {remainingSeconds}s remaining");
    }
}
var subSecondReset=fixedNow.AddSeconds(1).ToUnixTimeSeconds();
Check(UsageLabels.ResetSuffix(subSecondReset,300,fixedNow.AddMilliseconds(900))=="1s后" && UsageLabels.Reset(subSecondReset,300,fixedNow.AddMilliseconds(900)).EndsWith("1秒后"), "positive fractional second does not prematurely show zero");
Check(UsageLabels.ResetSuffix(subSecondReset,300,fixedNow.AddSeconds(1))=="待刷新" && UsageLabels.Reset(subSecondReset,300,fixedNow.AddSeconds(1)).EndsWith("等待刷新"), "second countdown transitions to waiting at expiration");
var summarySnapshot=new CodexBoardWidget.UsageSnapshot(new[]{new CodexBoardWidget.UsageWindow("5 小时额度",50,fixedNow.AddSeconds(45).ToUnixTimeSeconds(),300)},"plus",null,fixedNow);
Check(UsageLabels.Summary("Codex",summarySnapshot,fixedNow).Contains("45秒后"), "hover summary uses the same seconds format as details");
Check(UsageLabels.ResetSuffix(partialMinuteReset,300,fixedNow)=="18m后" && UsageLabels.Reset(partialMinuteReset,300,fixedNow).EndsWith("18分钟后"), "taskbar and details both round partial minutes up");
partialMinuteReset=fixedNow.AddMinutes(59).AddSeconds(59).ToUnixTimeSeconds();
Check(UsageLabels.ResetSuffix(partialMinuteReset,10080,fixedNow)=="1h后" && UsageLabels.Reset(partialMinuteReset,10080,fixedNow).EndsWith("1小时0分钟后"), "taskbar and details agree across the hour boundary");
partialMinuteReset=fixedNow.AddHours(3).AddMinutes(25).AddSeconds(1).ToUnixTimeSeconds();
Check(UsageLabels.ResetSuffix(partialMinuteReset,300,fixedNow)=="3h26m后" && UsageLabels.Reset(partialMinuteReset,300,fixedNow).EndsWith("3小时26分钟后"), "hour-minute labels share the same minute rounding");
var popup = DetailsPlacement.Above(new(220,1034,425,44),new(0,0,1920,1032),new(410,550),10,8);
Check(popup.Left==220 && popup.Bottom==1024, "details open above and aligned with the taskbar card");
popup = DetailsPlacement.Above(new(3700,1034,425,44),new(1920,0,1920,1032),new(410,550),10,8);
Check(popup.Right==3832 && popup.Bottom==1024, "details stay within the anchor monitor at the right edge");
popup = DetailsPlacement.Above(new(-1912,1034,425,44),new(-1920,0,1920,1032),new(410,550),10,8);
Check(popup.Left==-1912 && popup.Bottom==1024, "details support monitors with negative desktop coordinates");
popup = DetailsPlacement.Above(new(2000,1395,638,66),new(1920,0,2560,1392),new(615,825),15,12);
Check(popup.Left==2000 && popup.Bottom==1380 && popup.Width==615, "placement uses target-monitor physical pixels at higher DPI");
popup = DetailsPlacement.Above(new(0,300,425,44),new(0,0,800,300),new(410,550),10,8);
Check(popup.Top==8 && popup.Bottom==292, "short work areas constrain details instead of placing them offscreen");
