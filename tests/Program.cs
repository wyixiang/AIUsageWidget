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
