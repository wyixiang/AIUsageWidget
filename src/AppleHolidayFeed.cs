using System.Net;
using System.Net.Http;

namespace CodexTaskbar;

internal static class AppleHolidayFeed
{
    private const string Url = "https://calendars.icloud.com/holidays/cn_zh.ics";
    private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private static readonly string CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIUsageWidget", "cn_zh.ics");
    private static bool _loadedCache;
    private static DateTimeOffset _nextCheck;

    // Called only by the single desktop dispatcher. Network work never blocks quota reads.
    internal static async Task RefreshIfDueAsync(CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _nextCheck) return;
        _nextCheck = DateTimeOffset.MaxValue;
        try
        {
            if (!_loadedCache)
            {
                _loadedCache = true;
                try
                {
                    if (File.Exists(CachePath))
                    {
                        WeeklyPace.Calendar = ChinaWorkCalendar.Parse(await File.ReadAllTextAsync(CachePath, token));
                        var written = new DateTimeOffset(File.GetLastWriteTimeUtc(CachePath));
                        if (written <= now && written.AddDays(1) > now)
                        {
                            _nextCheck = written.AddDays(1);
                            return;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                { AppDiagnostics.Error("holiday-cache-read", ex); }
            }
            var content = await Client.GetStringAsync(Url, token);
            var calendar = ChinaWorkCalendar.Parse(content);
            token.ThrowIfCancellationRequested();
            WeeklyPace.Calendar = calendar;
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var temp = CachePath + ".tmp";
            await File.WriteAllTextAsync(temp, content, token);
            File.Move(temp, CachePath, overwrite: true);
            _nextCheck = now.AddDays(1);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _nextCheck = now.AddHours(1);
            AppDiagnostics.Error("holiday-calendar-refresh", ex);
        }
    }
}
