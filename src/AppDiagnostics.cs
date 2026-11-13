namespace CodexTaskbar;

internal static class AppDiagnostics
{
    private static LifecycleJournal? _journal;
    internal static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIUsageWidget", "logs");
    internal static LaunchEnvironment? EnvironmentInfo { get; private set; }
    internal static void Start()
    {
        _journal = new(LogDirectory, typeof(AppDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown", Environment.ProcessId);
        EnvironmentInfo = LaunchIsolation.Current;
        _journal.Event("launch-environment", System.Text.Json.JsonSerializer.Serialize(EnvironmentInfo));
    }
    internal static void LaunchFailure(Exception error)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "launch-error.json"), System.Text.Json.JsonSerializer.Serialize(new
            { At = DateTimeOffset.Now, ProcessId = Environment.ProcessId, Type = error.GetType().FullName, error.HResult }));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
    internal static void Event(string name, string reason) => _journal?.Event(name, reason);
    internal static void Error(string reason, Exception error) => _journal?.Error(reason, error);
    internal static void Fault(string reason, Exception error) => _journal?.Fault(reason, error);
    internal static void RequestStop(string reason) => _journal?.RequestStop(reason);
    internal static void Heartbeat() => _journal?.Heartbeat();
    internal static void Finish(int exitCode, string reason) => _journal?.Finish(exitCode, reason);
}
