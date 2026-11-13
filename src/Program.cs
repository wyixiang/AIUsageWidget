using System.Text.Json;
using CodexBoardWidget;

namespace CodexTaskbar;

internal static class Program
{
    // Keep the legacy IPC names so older launchers still reach this single instance.
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--menu")
        {
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\CodexTaskbar.Menu"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return;
        }
        if (args.Length == 1 && args[0] == "--exit")
        {
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\CodexTaskbar.Exit"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return;
        }
        try { if (LaunchIsolation.TryRelaunch(args)) return; }
        catch (Exception error)
        {
            AppDiagnostics.LaunchFailure(error);
            MessageBox.Show("无法脱离启动宿主独立运行。请从资源管理器双击 AIUsageWidget.exe 启动。\n错误类型：" + error.GetType().Name,
                "AI 用量", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        using var mutex = new Mutex(true, @"Local\CodexTaskbar.Personal", out var first);
        if (!first)
        {
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\CodexTaskbar.Show"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return;
        }
        AppDiagnostics.Start();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error) AppDiagnostics.Fault("appdomain-unhandled", error);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => AppDiagnostics.Finish(Environment.ExitCode, "process-exit-without-shutdown-request");
        TaskScheduler.UnobservedTaskException += (_, e) => AppDiagnostics.Error("unobserved-background-task", e.Exception);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (_, e) => AppDiagnostics.Fault("dispatcher-unhandled", e.Exception);
            int exitCode;
            using (var context = new TaskbarContext(args.Contains("--inspect-ui")))
            {
                app.SessionEnding += (_, e) => context.PrepareForSystemShutdown(e.ReasonSessionEnding.ToString());
                if (args.Contains("--details")) app.Dispatcher.BeginInvoke(new Action(context.ShowDetails));
                exitCode = app.Run();
            }
            AppDiagnostics.Finish(exitCode, "dispatcher-loop-ended");
        }
        catch (Exception error)
        {
            AppDiagnostics.Fault("main-unhandled", error);
            throw;
        }
    }
}

internal sealed class Preferences
{
    public double LeftFraction { get; set; } = .16;
    public int Offset { get; set; } = 4;
    public bool Locked { get; set; } = true;
    public string Theme { get; set; } = "system";
    public string? MonitorDevice { get; set; }
    public string? MonitorIdentity { get; set; }
    internal static string FilePath => Path.Combine(AppContext.BaseDirectory, "preferences.json");
    internal static Preferences Load()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }
    internal void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}

internal sealed class TaskbarContext : IDisposable
{
    internal readonly bool InspectUi;
    internal bool IsDark => Settings.Theme == "dark" || Settings.Theme == "system" && SystemTheme.IsDark;
    private bool? _lastDark;
    internal UsageSnapshot? Snapshot;
    internal string? Error;
    internal UsageSnapshot? CursorSnapshot;
    internal string? CursorError;
    internal bool CursorDetails;
    internal DateTimeOffset CountdownNow { get; private set; } = DateTimeOffset.Now;
    internal readonly Preferences Settings = Preferences.Load();
    private DockWindow? _dock;
    private DetailsWindow? _details;
    private readonly NotifyIcon _tray = new();
    private Icon? _trayIcon;
    private bool? _trayIconDark;
    private int _trayIconSize;
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskbarLayerWatcher _layerWatcher;
    private readonly EventWaitHandle _exitSignal = new(false, EventResetMode.AutoReset, @"Local\CodexTaskbar.Exit");
    private readonly EventWaitHandle _menuSignal = new(false, EventResetMode.AutoReset, @"Local\CodexTaskbar.Menu");
    private readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, @"Local\CodexTaskbar.Show");
    private bool _refreshing;
    private bool _quitting;
    internal bool IsQuitting => _quitting;
    private DateTimeOffset _nextRefresh = DateTimeOffset.MinValue;

    internal TaskbarContext(bool inspectUi = false)
    {
        InspectUi = inspectUi;
        UpdateTrayIcon(); _tray.Text = "AI 用量 · 双击打开详情"; _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowDetails();
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _dock?.ShowModernMenu(); };
        _layerWatcher = new(System.Windows.Application.Current.Dispatcher, RestoreDockLayer);
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerModeChanged;
        Settings.LeftFraction = Math.Clamp(Settings.LeftFraction, 0, .8);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }
    private void DisplaySettingsChanged(object? sender, EventArgs e) => ReconnectDisplays();
    private void PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if(e.Mode==Microsoft.Win32.PowerModes.Resume) ReconnectDisplays();
    }
    private void ReconnectDisplays()
    {
        var dispatcher=System.Windows.Application.Current?.Dispatcher;
        if(_quitting || dispatcher is null || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(()=>
        {
            if(_quitting) return;
            _dock?.PrepareForReconnect(); _dock?.DockToTaskbar();
            PositionDetails();
            _nextRefresh=DateTimeOffset.MinValue; WriteStatus();
        }));
    }
    internal void SelectDisplay(DisplayEntry display)
    {
        Settings.MonitorDevice=display.DeviceName; Settings.MonitorIdentity=display.Identity; Settings.Save();
        _dock?.PrepareForReconnect(); _dock?.DockToTaskbar(); PositionDetails(); WriteStatus();
    }
    private void Tick()
    {
        if (_exitSignal.WaitOne(0)) { RequestQuit("exit-signal"); return; }
        AppDiagnostics.Heartbeat();
        CountdownNow = DateTimeOffset.Now;
        UpdateTrayIcon();
        if (_dock is null || _dock.IsDisposed)
        {
            _dock = new DockWindow(this);
            AppDiagnostics.Event("dock-created", "ensure-resident-card");
            _dock.Show();
        }
        if (_lastDark != IsDark) Repaint();
        _dock.DockToTaskbar();
        _dock.UpdateCountdowns();
        if (_details is { IsDisposed: false }) _details.UpdateCountdowns();
        if (_menuSignal.WaitOne(0)) _dock.ShowModernMenu();
        if (_showSignal.WaitOne(0)) ShowDetails();
        if (!_refreshing && DateTimeOffset.Now >= _nextRefresh) _ = RefreshAsync();
    }
    internal async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try { await Task.WhenAll(ReadCodex(), ReadCursor()); }
        finally
        {
            _refreshing = false;
            _nextRefresh = DateTimeOffset.Now.AddMinutes(1);
            if (_dock is { IsDisposed: false }) _dock.Invalidate();
            if (_details is { IsDisposed: false }) _details.UpdateValues();
            WriteStatus();
        }
        async Task ReadCodex()
        {
            try { Snapshot = await Task.Run(() => CodexClient.ReadAsync(_lifetime.Token)); Error = null; }
            catch (OperationCanceledException) { if (!_lifetime.IsCancellationRequested) Error = "读取超时"; }
            catch { Error = "连接失败"; }
        }
        async Task ReadCursor()
        {
            try { CursorSnapshot = await Task.Run(() => CursorClient.ReadAsync(_lifetime.Token)); CursorError = null; }
            catch (OperationCanceledException) { if (!_lifetime.IsCancellationRequested) CursorError = "读取超时"; }
            catch (InvalidOperationException ex) { CursorError = ex.Message; }
            catch { CursorError = "连接失败，请稍后刷新"; }
        }
    }
    internal void WriteStatus()
    {
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "运行状态.json"), JsonSerializer.Serialize(new
            {
                Running = !_lifetime.IsCancellationRequested,
                ProcessId = Environment.ProcessId,
                StatusAt = DateTimeOffset.Now,
                Refreshing = _refreshing,
                AutoRefreshSeconds = 60,
                DetailsVisible = _details?.IsVisible ?? false,
                DetailsState = _details?.WindowState.ToString(),
                DetailsBounds = _details is { IsVisible: true, WindowState: System.Windows.WindowState.Normal } detail && Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(detail).Handle, out var detailsRect) ? new { detailsRect.Left, detailsRect.Top, Width = detailsRect.Right - detailsRect.Left, Height = detailsRect.Bottom - detailsRect.Top } : null,
                TrayVisible = _tray.Visible,
                DiagnosticsDirectory = AppDiagnostics.LogDirectory,
                LaunchEnvironment = AppDiagnostics.EnvironmentInfo,
                TrayIcon = "circle-gauge",
                TrayIconDark = _trayIconDark,
                TrayIconSize = _trayIconSize,
                AnchoredToTaskbar = _dock is { IsDisposed: false } && _dock.Attached,
                DisplayMode = "Taskbar-aligned independent window",
                MonitorDevice = _dock?.MonitorDevice,
                MonitorIdentity = _dock?.MonitorIdentity,
                PreferredMonitorIdentity = Settings.MonitorIdentity,
                AcrylicApplied = _dock?.AcrylicApplied,
                Visible = _dock is { IsDisposed: false, Visible: true },
                DockBounds = _dock is { IsDisposed: false } ? new { _dock.Bounds.Left, _dock.Bounds.Top, _dock.Bounds.Width, _dock.Bounds.Height } : null,
                QuotaAvailable = Snapshot is not null,
                LastUpdated = Snapshot?.FetchedAt,
                Remaining = Snapshot?.Windows.Select(w => new { w.Name, w.Remaining }),
                Plan = Snapshot?.Plan,
                CursorPlan = CursorSnapshot?.Plan,
                CursorCredits = CursorSnapshot?.Credits,
                CursorAvailable = CursorSnapshot is not null,
                CursorUpdated = CursorSnapshot?.FetchedAt,
                CursorRemaining = CursorSnapshot?.Windows.Select(w => new { w.Name, w.Remaining }),
                CursorError,
                Error
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
    internal void ShowDetails()
    {
        if (_details is null || _details.IsDisposed) _details = new DetailsWindow(this);
        System.Windows.Application.Current.MainWindow = _details;

        _details.UpdateValues();
        _details.Show();
        _details.WindowState = System.Windows.WindowState.Normal;
        PositionDetails();
        var handle = new System.Windows.Interop.WindowInteropHelper(_details).Handle;
        Native.ShowWindow(handle, 9);
        _details.Activate(); Native.SetForegroundWindow(handle); WriteStatus();
    }
    private void UpdateTrayIcon()
    {
        var dark = SystemTheme.IsTaskbarDark;
        var size = Math.Clamp(SystemInformation.SmallIconSize.Width, 16, 64);
        if (_trayIcon is not null && _trayIconDark == dark && _trayIconSize == size) return;
        var next = GaugeIcon.Create(size, dark);
        var windowIcon = GaugeIcon.Render(32, dark);
        _tray.Icon = next;
        var previous = _trayIcon;
        _trayIcon = next; _trayIconDark = dark; _trayIconSize = size;
        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows) window.Icon = windowIcon;
        previous?.Dispose();
    }
    internal void PositionDetails(DetailsWindow? expected = null)
    {
        if (_quitting || _details is not { IsDisposed: false, IsVisible: true } details || expected is not null && details != expected || _dock is not { IsDisposed: false, Visible: true } dock) return;
        var bounds = dock.Bounds;
        var scale = Math.Max(96, Native.GetDpiForWindow(dock.Handle)) / 96.0;
        details.PositionAbove(bounds, Screen.FromRectangle(bounds).WorkingArea, scale);
    }
    internal void DismissDetails() { if (_details is { IsDisposed: false }) _details.Dismiss(); }
    internal void RestoreDockLayer()
    {
        if (_quitting || _dock is not { IsDisposed: false, IsClosing: false } dock) return;
        dock.DockToTaskbar();
        WriteStatus();
    }
    internal void Repaint()
    {
        _lastDark = IsDark; _dock?.Invalidate(); Settings.Save();
        if (_details is { IsDisposed: false } previous)
        {
            var visible = previous.IsVisible; var state = previous.WindowState;
            var left = previous.Left; var top = previous.Top;
            previous.CloseForTheme(); _details = null;
            if (visible)
            {
                _details = new DetailsWindow(this) { Left = left, Top = top, WindowState = state, ShowActivated = state != System.Windows.WindowState.Minimized };
                System.Windows.Application.Current.MainWindow = _details;
                _details.UpdateValues(); _details.Show();
                PositionDetails();
            }
        }
    }
    internal void Quit() => RequestQuit("menu-exit");
    internal void PrepareForSystemShutdown(string reason)
    {
        if (_quitting) return;
        AppDiagnostics.RequestStop("windows-session-" + reason);
        _quitting = true; _timer.Stop(); _lifetime.Cancel();
    }
    private async void RequestQuit(string reason)
    {
        if (_quitting) return;
        AppDiagnostics.RequestStop(reason);
        _quitting = true;
        _timer.Stop();
        _lifetime.Cancel();
        var until = DateTimeOffset.Now.AddSeconds(3);
        while (_refreshing && DateTimeOffset.Now < until) await Task.Delay(40);
        if (_details is { IsDisposed: false, IsClosing: false }) _details.Close();
        if (_dock is { IsDisposed: false, IsClosing: false }) _dock.Close();
        WriteStatus();
        System.Windows.Application.Current.Shutdown();
    }
    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= PowerModeChanged;
        _layerWatcher.Dispose();
        _tray.Visible = false; _tray.Dispose(); _trayIcon?.Dispose(); _timer.Stop(); _lifetime.Cancel(); _lifetime.Dispose(); _exitSignal.Dispose(); _menuSignal.Dispose(); _showSignal.Dispose();

    }
}
