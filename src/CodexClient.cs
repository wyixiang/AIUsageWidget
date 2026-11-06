using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexBoardWidget;

internal sealed record UsageWindow(string Name, double Remaining, long? ResetsAt, long? Duration);
internal sealed record UsageSnapshot(UsageWindow[] Windows, string? Plan, string? Credits, DateTimeOffset FetchedAt);

internal static class CodexClient
{
    internal static string Locate()
    {
        var configured = Environment.GetEnvironmentVariable("CODEX_USAGE_WIDGET_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(desktop))
        {
            var newest = Directory.EnumerateFiles(desktop, "codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (newest is not null) return newest;
        }
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try { var path = Path.Combine(folder.Trim('"'), "codex.exe"); if (File.Exists(path)) return path; }
            catch (ArgumentException) { }
        }
        throw new InvalidOperationException("未找到本机 Codex。请先启动并登录 Codex 桌面版。");
    }

    internal static async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(35));
        var token = deadline.Token;
        var start = new ProcessStartInfo(Locate())
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("analytics.enabled=false");
        start.ArgumentList.Add("app-server");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 Codex 用量接口。");
        // Drain diagnostics without persisting potentially sensitive service output.
        var errors = Task.Run(async () => { try { while (await process.StandardError.ReadLineAsync(token) is not null) { } } catch { } });
        try
        {
            await Send(new { id = 0, method = "initialize", @params = new { clientInfo = new { name = "codex_board_widget", title = "Codex 用量卡片", version = "1.0.0" } } });
            await Receive(0);
            await Send(new { method = "initialized", @params = new { } });
            await Send(new { id = 1, method = "account/rateLimits/read" });
            return Parse(await Receive(1));
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            deadline.Cancel();
            await errors;
        }

        async Task Send(object message)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), token);
            await process.StandardInput.FlushAsync(token);
        }
        async Task<JsonElement> Receive(int id)
        {
            while (await process.StandardOutput.ReadLineAsync(token) is { } line)
            {
                JsonDocument document;
                try { document = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                using (document)
                {
                    var root = document.RootElement;
                    if (!root.TryGetProperty("id", out var responseId) || !responseId.TryGetInt32(out var number) || number != id) continue;
                    if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("Codex 无法提供订阅额度，请在 Codex 中确认登录状态后重试。");
                    return root.GetProperty("result").Clone();
                }
            }
            throw new InvalidOperationException("Codex 用量接口已退出。");
        }
    }

    internal static UsageSnapshot Parse(JsonElement result)
    {
        JsonElement limits = default;
        if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object)
        {
            if (buckets.TryGetProperty("codex", out var general)) limits = general;
        }
        if (limits.ValueKind != JsonValueKind.Object && result.TryGetProperty("rateLimits", out var legacy)) limits = legacy;
        if (limits.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("账号暂未返回订阅额度；API Key 登录不提供此信息。");
        var windows = new List<UsageWindow>();
        foreach (var key in new[] { "primary", "secondary" })
        {
            if (!limits.TryGetProperty(key, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (!window.TryGetProperty("usedPercent", out var used) || !used.TryGetDouble(out var percent) || !double.IsFinite(percent)) continue;
            long? duration = window.TryGetProperty("windowDurationMins", out var d) && d.TryGetInt64(out var minutes) ? minutes : null;
            long? reset = window.TryGetProperty("resetsAt", out var r) && r.TryGetInt64(out var seconds) && seconds > 0 && seconds < 253402300800 ? seconds : null;
            string name = duration switch { 300 => "5 小时额度", 10080 => "每周额度", > 0 and < 60 => $"{duration} 分钟额度", > 0 when duration % 60 == 0 => $"{duration / 60} 小时额度", _ => key == "primary" ? "主要额度" : "次要额度" };
            windows.Add(new UsageWindow(name, Math.Clamp(100 - percent, 0, 100), reset, duration));
        }
        var plan = limits.TryGetProperty("planType", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        string? credits = null;
        if (limits.TryGetProperty("credits", out var c) && c.ValueKind == JsonValueKind.Object)
        {
            if (c.TryGetProperty("unlimited", out var unlimited) && unlimited.ValueKind == JsonValueKind.True) credits = "无限额";
            else if (c.TryGetProperty("balance", out var balance) && balance.ValueKind is JsonValueKind.String or JsonValueKind.Number) credits = balance.ToString();
        }
        return new UsageSnapshot(windows.ToArray(), plan, credits, DateTimeOffset.Now);
    }
}
