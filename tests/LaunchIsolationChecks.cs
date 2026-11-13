using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CodexTaskbar;

internal static class LaunchIsolationChecks
{
    private static readonly string[] Payload = ["", "with spaces", "中文路径", "quoted\"value", @"C:\folder with space\", "slashes\\\\\"end"];

    private sealed record Probe(int ProcessId, LaunchEnvironment Environment, string[] Arguments);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--probe")
        {
            File.WriteAllText(args[1] + ".pre", JsonSerializer.Serialize(LaunchIsolation.Current));
            if (LaunchIsolation.TryRelaunch(args)) return 0;
            var file = args[1];
            File.WriteAllText(file, JsonSerializer.Serialize(new Probe(Environment.ProcessId, LaunchIsolation.Current, args)));
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(file + ".stop") && DateTime.UtcNow < deadline) Thread.Sleep(20);
            return 0;
        }
        if (args.FirstOrDefault() == "--host") return RunHost(args[1], uint.Parse(args[2]));

        var directory = Path.Combine(Path.GetTempPath(), "AIUsageWidget-launch-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Check(!new LaunchEnvironment(null, false, null).HostBound, "ordinary desktop launch stays in the same process");
        Check(new LaunchEnvironment("example.package", false, null).HostBound, "inherited package identity requires independent launch");
        Check(new LaunchEnvironment(null, true, 0x2000).HostBound, "kill-on-close host job requires independent launch");
        Check(!new LaunchEnvironment(null, true, 0x800).HostBound, "ordinary non-terminating jobs do not cause relaunch loops");
        foreach (var policy in new uint[] { 0x2800, 0x3000 })
        {
            var marker = Path.Combine(directory, policy + ".json");
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--host"); start.ArgumentList.Add(marker); start.ArgumentList.Add(policy.ToString());
            using var host = Process.Start(start)!;
            if (!host.WaitForExit(10000) || host.ExitCode != 0)
                throw new Exception("Host launch failed: " + policy + " " + host.StandardError.ReadToEnd());
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!File.Exists(marker) && DateTime.UtcNow < deadline) Thread.Sleep(20);
            if (!File.Exists(marker)) throw new Exception("Child did not survive host shutdown: " + policy);
            Probe? probe = null;
            while (probe is null && DateTime.UtcNow < deadline)
            {
                try { probe = JsonSerializer.Deserialize<Probe>(File.ReadAllText(marker)); }
                catch (JsonException) { Thread.Sleep(20); }
            }
            if (probe is null) throw new Exception("Invalid child state");
            using var child = Process.GetProcessById(probe.ProcessId);
            try
            {
                Check(!child.HasExited && probe.Environment.PackageFullName is null && !probe.Environment.InJob,
                    $"resident survives kill-on-close host exit outside its job ({policy:X})");
                Check(probe.Arguments.Skip(2).Take(Payload.Length).SequenceEqual(Payload),
                    $"independent launch preserves empty, Unicode, quoted and trailing-slash arguments ({policy:X})");
                Check(probe.Arguments.Count(a => a == LaunchIsolation.RelaunchFlag) == 1,
                    $"independent relaunch completes once without recursion ({policy:X})");
            }
            finally { File.WriteAllText(marker + ".stop", "stop"); child.WaitForExit(5000); }
        }
        return 0;
    }

    private static int RunHost(string marker, uint policy)
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) throw new Win32Exception();
        var launched = false;
        try
        {
            var limits = new ExtendedLimits { Basic = new() { LimitFlags = policy } };
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()) ||
                !AssignProcessToJobObject(job, Process.GetCurrentProcess().Handle)) throw new Win32Exception();
            Check(LaunchIsolation.Current.HostBound, "test host has a kill-on-close job");
            if (!LaunchIsolation.TryRelaunch(new[] { "--probe", marker }.Concat(Payload).ToArray()))
                throw new Exception("Host isolation was skipped");
            launched = true;
            return 0;
        }
        // On success Windows releases the job at process exit, after the host has
        // returned normally. Closing it here would terminate this test host early.
        finally { if (!launched) CloseHandle(job); }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
