using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CodexBoardWidget;
namespace CodexTaskbar;

internal static class CursorClient
{
    internal static async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var token = ReadSession();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(TimeSpan.FromSeconds(25));
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        async Task<JsonElement> Query(string method)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,"https://api2.cursor.sh/aiserver.v1.DashboardService/" + method);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
            request.Headers.Add("Connect-Protocol-Version","1");
            request.Content = new StringContent("{}",Encoding.UTF8,"application/json");
            using var response = await client.SendAsync(request,deadline.Token).ConfigureAwait(false);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidOperationException("Cursor 查询被拒绝，请稍后重试");
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException("Cursor 额度暂不可用");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false));
            return doc.RootElement.Clone();
        }
        async Task<JsonElement?> Optional(string method)
        {
            try { return await Query(method).ConfigureAwait(false); }
            catch { cancellationToken.ThrowIfCancellationRequested(); return null; }
        }
        var usageTask=Query("GetCurrentPeriodUsage");
        var planTask=Optional("GetPlanInfo");
        var balanceTask=Optional("GetComplatCreditBalance");
        var prepaidTask=Optional("GetUserPrepaidBilling");
        await Task.WhenAll(usageTask,planTask,balanceTask,prepaidTask).ConfigureAwait(false);
        return Parse(await usageTask,await planTask,await balanceTask,await prepaidTask);
    }
    internal static UsageSnapshot Parse(JsonElement root, JsonElement? planResponse=null, JsonElement? balanceResponse=null, JsonElement? prepaidResponse=null)
    {
        if(!root.TryGetProperty("planUsage",out var usage) || usage.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("此账号暂未返回可显示额度");
        JsonElement plan=default;
        if(planResponse is { ValueKind:JsonValueKind.Object } info) info.TryGetProperty("planInfo",out plan);
        var planName=Text(plan,"planName");
        var period=Text(plan,"includedUsagePeriod");
        var weekly=period is "INCLUDED_USAGE_PERIOD_WEEKLY" or "WEEKLY" || Number(plan,"includedUsagePeriod")==2;
        var resetMs=Number(plan,"includedUsageResetsAt") ?? (weekly ? null : Number(root,"billingCycleEnd"));
        long? reset=resetMs is >0 and <253402300800000 ? (long)(resetMs.Value/1000) : null;
        var items=new List<UsageWindow>();
        if(weekly)
        {
            var used=Number(usage,"autoPercentUsed") ?? Number(usage,"totalPercentUsed");
            if(used is null && Number(usage,"limit") is { } limit && limit>0 && Number(usage,"remaining") is { } remaining) used=100-remaining/limit*100;
            if(used is { } percent) items.Add(new("本周额度",Math.Clamp(100-percent,0,100),reset,10080));
        }
        else
        {
            foreach(var (key,name) in new[]{("autoPercentUsed","Cursor 模型额度"),("apiPercentUsed","其他模型额度")})
                if(Number(usage,key) is { } used) items.Add(new(name,Math.Clamp(100-used,0,100),reset,null));
            if(items.Count==0 && Number(usage,"limit") is { } limit && limit>0 && Number(usage,"remaining") is { } remaining) items.Add(new("套餐额度",Math.Clamp(remaining/limit*100,0,100),reset,null));
            if(items.Count==0 && Number(usage,"totalPercentUsed") is { } total) items.Add(new("套餐额度",Math.Clamp(100-total,0,100),reset,null));
        }
        if(items.Count==0) throw new InvalidOperationException("此账号暂未返回可显示额度");
        double? cents=null;
        if(prepaidResponse is { ValueKind:JsonValueKind.Object } prepaid && prepaid.TryGetProperty("eligible",out var eligible) && eligible.ValueKind==JsonValueKind.True)
            cents=Number(prepaid,"availableCents") ?? 0;
        if(cents is null && balanceResponse is { } balance) cents=Number(balance,"usdRemainingCents");
        var credits=cents is { } money ? "$"+(money/100).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture) : null;
        return new UsageSnapshot(items.ToArray(),planName,credits,DateTimeOffset.Now);
    }
    private static string? Text(JsonElement item,string key) => item.ValueKind==JsonValueKind.Object && item.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String ? v.GetString() : null;
    private static double? Number(JsonElement item,string key)
    {
        if(item.ValueKind!=JsonValueKind.Object || !item.TryGetProperty(key,out var v)) return null;
        if(v.ValueKind==JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n)) return n;
        if(v.ValueKind==JsonValueKind.String && double.TryParse(v.GetString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n) && double.IsFinite(n)) return n;
        return null;
    }
    private static string ReadSession()
    {
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Cursor","User","globalStorage","state.vscdb");
        if(!File.Exists(path)) throw new InvalidOperationException("未找到 Cursor 登录，请先登录 Cursor");
        IntPtr db=IntPtr.Zero, statement=IntPtr.Zero;
        try
        {
            if(sqlite3_open_v2(path,out db,1,IntPtr.Zero)!=0) throw new InvalidOperationException("暂时无法读取 Cursor 登录状态");
            sqlite3_busy_timeout(db,1200);
            if(sqlite3_prepare_v2(db,"SELECT value FROM ItemTable WHERE key = 'cursorAuth/accessToken' LIMIT 1",-1,out statement,IntPtr.Zero)!=0 || sqlite3_step(statement)!=100)
                throw new InvalidOperationException("请先在 Cursor 中登录");
            var token=Marshal.PtrToStringUTF8(sqlite3_column_text(statement,0));
            if(string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("请先在 Cursor 中登录");
            return CursorSessionDecoder.Decode(token);
        }
        finally { if(statement!=IntPtr.Zero) sqlite3_finalize(statement); if(db!=IntPtr.Zero) sqlite3_close(db); }
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string file,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string query,int count,out IntPtr stmt,IntPtr tail);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr stmt,int index);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(IntPtr db,int milliseconds);
}
