using System.Text.Json;
using CodexTaskbar;

internal static class LifecycleChecks
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "AIUsageWidget-lifecycle-checks", Guid.NewGuid().ToString("N"));
        var normal = Path.Combine(root, "normal");
        var journal = new LifecycleJournal(normal, "test", 123);
        journal.RequestStop("menu-exit");
        journal.Finish(0, "dispatcher-loop-ended");
        using var exit = JsonDocument.Parse(File.ReadAllText(Path.Combine(normal, "last-exit.json")));
        Check(exit.RootElement.GetProperty("CleanExit").GetBoolean() && exit.RootElement.GetProperty("Reason").GetString() == "menu-exit", "normal exit preserves the explicit shutdown reason");
        var next = new LifecycleJournal(normal, "test", 124);
        Check(!next.PreviousRunInterrupted, "normal shutdown is not reported as a crash on restart");
        next.RequestStop("windows-session-Shutdown"); next.Finish(0, "dispatcher-loop-ended");

        var interrupted = Path.Combine(root, "interrupted");
        _ = new LifecycleJournal(interrupted, "test", 200);
        var recovered = new LifecycleJournal(interrupted, "test", 201);
        using var prior = JsonDocument.Parse(File.ReadAllText(Path.Combine(interrupted, "last-exit.json")));
        Check(recovered.PreviousRunInterrupted && prior.RootElement.GetProperty("Reason").GetString() == "unknown-no-clean-exit-record", "interrupted process is detected without inventing a cause");

        var fatal = Path.Combine(root, "fatal");
        var crashed = new LifecycleJournal(fatal, "test", 300);
        try { throw new InvalidOperationException("Bearer example-secret-must-not-be-logged"); }
        catch (Exception error) { crashed.Fault("dispatcher-unhandled", error); }
        crashed.Finish(1, "process-exit");
        using var failed = JsonDocument.Parse(File.ReadAllText(Path.Combine(fatal, "last-exit.json")));
        Check(!failed.RootElement.GetProperty("CleanExit").GetBoolean() && failed.RootElement.GetProperty("Error").GetProperty("Type").GetString() == typeof(InvalidOperationException).FullName, "fatal exception type and non-clean exit are retained");
        var contents = string.Join("", Directory.GetFiles(fatal).Select(File.ReadAllText));
        Check(!contents.Contains("example-secret") && contents.Contains(nameof(LifecycleChecks)), "diagnostics retain stack frames without exception payloads");

        var rotation = Path.Combine(root, "rotation");
        var rotating = new LifecycleJournal(rotation, "test", 400);
        for (var index = 0; index < 4; index++)
        {
            File.WriteAllText(Path.Combine(rotation, "lifecycle.jsonl"), new string('x', (int)LifecycleJournal.MaxLogBytes));
            rotating.Event("rotation-check", "bounded-history");
        }
        Check(Directory.GetFiles(rotation, "lifecycle*.jsonl").Length == 3 && Directory.GetFiles(rotation, "lifecycle*.jsonl").All(path => new FileInfo(path).Length <= LifecycleJournal.MaxLogBytes), "log rotation retains three size-bounded files");

        var invalid = Path.Combine(root, "not-a-directory"); File.WriteAllText(invalid, "occupied");
        var unavailable = new LifecycleJournal(invalid, "test", 500);
        unavailable.RequestStop("test-exit"); unavailable.Finish(0, "exit");
        Check(File.ReadAllText(invalid) == "occupied", "unwritable diagnostics do not throw or overwrite unrelated data");
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
