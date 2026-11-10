namespace CodexTaskbar;

internal static class AppDiagnostics
{
    private static LifecycleJournal? _journal;
    internal static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIUsageWidget", "logs");
    internal static void Start() => _journal = new(LogDirectory,
        typeof(AppDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown", Environment.ProcessId);
    internal static void Event(string name, string reason) => _journal?.Event(name, reason);
    internal static void Error(string reason, Exception error) => _journal?.Error(reason, error);
    internal static void Fault(string reason, Exception error) => _journal?.Fault(reason, error);
    internal static void RequestStop(string reason) => _journal?.RequestStop(reason);
    internal static void Heartbeat() => _journal?.Heartbeat();
    internal static void Finish(int exitCode, string reason) => _journal?.Finish(exitCode, reason);
}
