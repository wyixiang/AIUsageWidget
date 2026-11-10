using System.Text;
using System.Text.Json;
namespace CodexTaskbar;

internal sealed class LifecycleJournal
{
    internal const long MaxLogBytes = 512 * 1024;
    private readonly object _gate = new();
    private readonly string _directory;
    private Session _session;
    private bool _finished;
    private bool _stopRequested;
    private bool _faulted;
    internal bool PreviousRunInterrupted { get; private set; }
    internal string DirectoryPath => _directory;

    private sealed record Failure(string Type, string HResult, string? StackTrace);
    private sealed record Session(string RunId, int ProcessId, string Version, DateTimeOffset StartedAt,
        DateTimeOffset LastSeenAt, string Phase, string Reason, bool CleanExit, int? ExitCode, Failure? Error);
    private sealed record Entry(DateTimeOffset At, string RunId, int ProcessId, string Version,
        string Event, string Reason, Failure? Error, string? RelatedRunId);

    internal LifecycleJournal(string directory, string version, int processId)
    {
        _directory = directory;
        var now = DateTimeOffset.Now;
        _session = new(Guid.NewGuid().ToString("N"), processId, version, now, now, "running", "started", false, null, null);
        Attempt(() =>
        {
            Directory.CreateDirectory(directory);
            Session? previous = null;
            var previousPath = Path.Combine(directory, "session.json");
            try { if (File.Exists(previousPath)) previous = JsonSerializer.Deserialize<Session>(File.ReadAllText(previousPath)); }
            catch (JsonException) { Append("previous-state-unreadable", "invalid-session-record"); }
            if (previous is not null)
            {
                PreviousRunInterrupted = !previous.CleanExit;
                if (PreviousRunInterrupted && previous.Phase == "running")
                    previous = previous with { Phase = "interrupted", Reason = "unknown-no-clean-exit-record" };
                WriteJson("last-exit.json", previous);
                if (PreviousRunInterrupted)
                    Append("previous-run-interrupted", previous.Reason, previous.Error, previous.RunId);
            }
            Append("started", "primary-instance-started");
            WriteSession();
        });
    }

    internal void Event(string name, string reason) => Attempt(() => Append(name, reason));
    internal void Error(string reason, Exception error) => Attempt(() => Append("runtime-error", reason, Describe(error)));
    internal void Fault(string reason, Exception error) => Attempt(() =>
    {
        _faulted = true;
        var failure = Describe(error);
        _session = _session with { Phase = "faulted", Reason = reason, Error = failure, CleanExit = false, LastSeenAt = DateTimeOffset.Now };
        Append("fatal-error", reason, failure);
        WriteSession();
        WriteJson("last-exit.json", _session);
    });
    internal void RequestStop(string reason) => Attempt(() =>
    {
        if (_finished || _stopRequested) return;
        _stopRequested = true;
        if (!_faulted) _session = _session with { Phase = "stopping", Reason = reason, LastSeenAt = DateTimeOffset.Now };
        Append("shutdown-requested", reason);
        WriteSession();
    });
    internal void Heartbeat() => Attempt(() =>
    {
        if (_finished || DateTimeOffset.Now - _session.LastSeenAt < TimeSpan.FromSeconds(30)) return;
        _session = _session with { LastSeenAt = DateTimeOffset.Now };
        WriteSession();
    });
    internal void Finish(int exitCode, string fallbackReason) => Attempt(() =>
    {
        if (_finished) return;
        _finished = true;
        var clean = _stopRequested && !_faulted && exitCode == 0;
        _session = _session with
        {
            Phase = _faulted ? "faulted" : clean ? "exited" : "unexpected-exit",
            Reason = _faulted || _stopRequested ? _session.Reason : fallbackReason,
            CleanExit = clean, ExitCode = exitCode, LastSeenAt = DateTimeOffset.Now
        };
        Append("exited", _session.Reason, _session.Error);
        WriteSession();
        WriteJson("last-exit.json", _session);
    });

    private static Failure Describe(Exception error)
    {
        var stack = error.StackTrace;
        if (stack?.Length > 12000) stack = stack[..12000];
        // Exception messages or data can contain service payloads or credentials.
        return new(error.GetType().FullName ?? error.GetType().Name, $"0x{error.HResult:X8}", stack);
    }
    private void Attempt(Action action)
    {
        try { lock (_gate) action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        { /* Diagnostic I/O must never terminate the widget. */ }
    }
    private void Append(string name, string reason, Failure? failure = null, string? relatedRunId = null)
    {
        Directory.CreateDirectory(_directory);
        if (reason.Length > 256) reason = reason[..256];
        var entry = new Entry(DateTimeOffset.Now, _session.RunId, _session.ProcessId, _session.Version, name, reason, failure, relatedRunId);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
        var path = Path.Combine(_directory, "lifecycle.jsonl");
        if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > MaxLogBytes)
        {
            var previous = Path.Combine(_directory, "lifecycle.1.jsonl");
            if (File.Exists(previous)) File.Move(previous, Path.Combine(_directory, "lifecycle.2.jsonl"), true);
            File.Move(path, previous, true);
        }
        using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        file.Write(bytes); file.Flush(true);
    }
    private void WriteSession() => WriteJson("session.json", _session);
    private void WriteJson<T>(string name, T data)
    {
        var path = Path.Combine(_directory, name);
        var temporary = path + ".tmp";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read))
        { file.Write(bytes); file.Flush(true); }
        File.Move(temporary, path, true);
    }
}
