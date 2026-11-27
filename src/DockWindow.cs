using System.Runtime.InteropServices;
using W = System.Windows;
using C = System.Windows.Controls;
using M = System.Windows.Media;
using H = System.Windows.Interop;
using I = System.Windows.Input;
namespace CodexTaskbar;

internal sealed class DockWindow : GlassWindow
{
    private readonly TaskbarContext _context;
    private readonly List<QuotaMeter> _meters = new();
    private readonly List<(W.FrameworkElement Element, bool Cursor)> _platformRegions = new();
    private TaskbarLayout _layout = TaskbarLayout.Create(true, true, false, 1);
    private Point? _press;
    private int _startOffset;
    private bool _dragged;
    private ModernMenu? _menu;
    private readonly MonitorSelection _monitorSelection = new();
    internal bool Attached { get; private set; }
    internal string LayoutMode => !_layout.HasContent ? "Hidden" : _layout.IsHorizontal ? "Horizontal" : "Stacked";
    internal string? MonitorDevice { get; private set; }
    internal string? MonitorIdentity { get; private set; }
    internal void PrepareForReconnect() => _monitorSelection.BeginReconnect(DateTimeOffset.UtcNow);
    internal IntPtr Handle => IsDisposed || IsClosing ? IntPtr.Zero : new H.WindowInteropHelper(this).EnsureHandle();
    internal bool Visible => IsVisible;
    internal Rectangle Bounds => Native.GetWindowRect(Handle, out var r) ? Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom) : Rectangle.Empty;
    internal DockWindow(TaskbarContext context) : base(context.IsDark)
    {
        _context = context;
        Title = "AI 用量 · 任务栏"; ShowActivated = false; ShowInTaskbar = false;
        Width = 425; Height = 44; Surface.CornerRadius = new W.CornerRadius(9);
        ToolTip = "剩余额度 · 单击查看详情 · 右键设置屏幕和位置";
        SourceInitialized += (_, _) => Native.SetWindowLongPtr(Handle,-20,new IntPtr(Native.GetWindowLongPtr(Handle,-20).ToInt64() | 0x08000000L));
        MouseLeftButtonDown += (_, e) => { _press = System.Windows.Forms.Cursor.Position; _startOffset = context.Settings.Offset; _dragged = false; e.Handled = true; };
        MouseMove += (_, e) =>
        {
            if (_press is not { } start || e.LeftButton != I.MouseButtonState.Pressed || context.Settings.Locked) return;
            var delta = System.Windows.Forms.Cursor.Position.X - start.X;
            if (!_dragged && Math.Abs(delta) < 5) return;
            if (!_dragged) CaptureMouse();
            _dragged = true;
            context.Settings.Offset = _startOffset + (int)Math.Round(delta / (Math.Max(96, Native.GetDpiForWindow(Handle)) / 96.0));
            DockToTaskbar();
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (!_press.HasValue) return;
            _press = null; ReleaseMouseCapture(); e.Handled = true;
            if (_dragged) context.Settings.Save(); else { context.CursorDetails = PlatformAt(e.GetPosition(Surface.Child)); context.ShowDetails(); }
        };
        LostMouseCapture += (_, _) => _press = null;
        MouseRightButtonUp += (_, e) => { ShowModernMenu(); e.Handled = true; };
        Closed += (_, _) => { if (_menu is { IsDisposed: false }) _menu.Close(); };
        Invalidate();
    }
    protected override void OnEscape() => _context.DismissDetails();
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_context.IsQuitting)
        {
            e.Cancel = true;
            _context.DismissDetails();
            AppDiagnostics.Event("dock-close-blocked", "resident-card-requires-explicit-exit");
        }
        base.OnClosing(e);
    }
    internal void Invalidate()
    {
        _meters.Clear();
        _platformRegions.Clear();
        var settings = _context.Settings;
        _layout = TaskbarLayout.Create(settings.ShowCodex, _context.ShowCursor, settings.ShowCursorCredit, _context.Snapshot?.Windows.Length ?? 0, settings.HorizontalWidthLimit);
        Width = Math.Max(1, _layout.Width);
        var dark = _context.IsDark; UpdateBackdrop(dark); var p = new Palette(dark);
        Surface.Background = Palette.Brush(dark ? "#C8262C33" : "#C8F8FAFC"); Surface.BorderBrush = p.Stroke;
        if (!_layout.IsHorizontal)
        {
            Surface.Child = StackedContent(p);
            ToolTip = Summary();
            return;
        }
        var grid = new C.Grid { Margin = new W.Thickness(TaskbarLayout.ContentInset - Surface.BorderThickness.Left, 2, TaskbarLayout.ContentInset - Surface.BorderThickness.Right, 2), Cursor = I.Cursors.Hand };
        void Section(W.FrameworkElement element, double width, bool cursor)
        {
            if (grid.ColumnDefinitions.Count > 0)
            {
                grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.SectionGap) });
                var divider = new C.Border { Width = 1, Height = 25, Background = p.Stroke, VerticalAlignment = W.VerticalAlignment.Center };
                C.Grid.SetColumn(divider, grid.ColumnDefinitions.Count - 1); grid.Children.Add(divider);
            }
            grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(width) });
            C.Grid.SetColumn(element, grid.ColumnDefinitions.Count - 1); grid.Children.Add(element);
            _platformRegions.Add((element, cursor));
        }
        void Platform(string label, CodexBoardWidget.UsageSnapshot? snapshot, string? error, bool cursor)
        {
            var section = new C.Grid();
            section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.BrandWidth) });
            var brand = new C.Grid { Margin = new W.Thickness(0, 0, 10, 0), VerticalAlignment = W.VerticalAlignment.Center };
            brand.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(24) });
            brand.ColumnDefinitions.Add(new C.ColumnDefinition());
            var symbol = ProductIcons.Create(cursor, p.Accent); symbol.Width = symbol.Height = 18;
            brand.Children.Add(symbol);
            var identity = new C.StackPanel { VerticalAlignment = W.VerticalAlignment.Center };
            identity.Children.Add(new C.TextBlock { Text = label, FontFamily = new M.FontFamily("Segoe UI"), FontSize = 12, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text });
            identity.Children.Add(new C.TextBlock { Text = snapshot?.Plan is { } plan ? UsageLabels.Plan(plan) : "—", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 10.5, Foreground = p.Muted, TextTrimming = W.TextTrimming.CharacterEllipsis });
            C.Grid.SetColumn(identity, 1); brand.Children.Add(identity); section.Children.Add(brand);
            var windows = snapshot?.Windows.Take(cursor ? 1 : 2).ToArray();
            if(windows is { Length: >0 })
            {
                for(var index=0;index<windows.Length;index++)
                {
                    if (index > 0) section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.MeterGap) });
                    section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.MeterWidth) });
                    var quota = windows[index];
                    var name = MeterLabel(cursor, quota);
                    var meter = new QuotaMeter(name, quota, p, _context.CountdownNow);
                    _meters.Add(meter);
                    C.Grid.SetColumn(meter, section.ColumnDefinitions.Count - 1); section.Children.Add(meter);
                }
            }
            else
            {
                section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.MeterWidth) });
                var message = new C.TextBlock { Text = error is not null ? "Unavailable" : snapshot is null ? "Loading…" : "—", FontSize = 11, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center };
                C.Grid.SetColumn(message, 1); section.Children.Add(message);
            }
            section.ToolTip = UsageLabels.Summary(label, snapshot, _context.CountdownNow, settings.ShowCursorCredit);
            Section(section, cursor ? _layout.CursorWidth : _layout.CodexWidth, cursor);
        }
        if (_layout.Codex) Platform("Codex", _context.Snapshot, _context.Error, false);
        if (_layout.Cursor) Platform("Cursor", _context.CursorSnapshot, _context.CursorError, true);
        if (_layout.Credit)
        {
            var credit = new C.StackPanel { VerticalAlignment = W.VerticalAlignment.Center, ToolTip = "Cursor Credit · 独立余额" };
            credit.Children.Add(new C.TextBlock { Text = "Credit", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 11, Foreground = p.Muted });
            credit.Children.Add(new C.TextBlock { Text = _context.CursorSnapshot?.Credits ?? "—", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 18, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text, TextTrimming = W.TextTrimming.CharacterEllipsis });
            Section(credit, TaskbarLayout.CreditWidth, true);
        }
        ToolTip = Summary();
        Surface.Child = grid;
    }
    private static string MeterLabel(bool cursor, CodexBoardWidget.UsageWindow quota)
        => cursor ? quota.Duration == 10080 ? "Wk" : "Plan" : quota.Duration == 300 ? "5h" : "Wk";
    private C.Grid StackedContent(Palette p)
    {
        var grid = new C.Grid { Margin = new W.Thickness(TaskbarLayout.ContentInset - Surface.BorderThickness.Left, 0, TaskbarLayout.ContentInset - Surface.BorderThickness.Right, 0), Cursor = I.Cursors.Hand };
        void Row(bool cursor)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new C.RowDefinition());
            var section = new C.Grid();
            section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.CompactBrandWidth) });
            for (var column = 0; column < _layout.StackedColumns; column++)
            {
                if (column > 0) section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.CompactMeterGap) });
                section.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(TaskbarLayout.CompactMeterWidth) });
            }
            C.Grid.SetRow(section, row); grid.Children.Add(section); _platformRegions.Add((section, cursor));
            var snapshot = cursor ? _context.CursorSnapshot : _context.Snapshot;
            var error = cursor ? _context.CursorError : _context.Error;
            var brand = new C.Grid { Margin = new W.Thickness(0, 0, 6, 0), VerticalAlignment = W.VerticalAlignment.Center };
            foreach (var width in new[] { new W.GridLength(22), new W.GridLength(40), new W.GridLength(1, W.GridUnitType.Star) })
                brand.ColumnDefinitions.Add(new C.ColumnDefinition { Width = width });
            brand.Children.Add(ProductIcons.Create(cursor, p.Muted));
            var name = new C.TextBlock { Text = cursor ? "Cursor" : "Codex", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 12, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text };
            C.Grid.SetColumn(name, 1); brand.Children.Add(name);
            var plan = new C.TextBlock { Text = snapshot?.Plan is { } value ? UsageLabels.Plan(value) : "—", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 10.5, Foreground = p.Muted, TextTrimming = W.TextTrimming.CharacterEllipsis, VerticalAlignment = W.VerticalAlignment.Center };
            C.Grid.SetColumn(plan, 2); brand.Children.Add(plan); section.Children.Add(brand);
            var windows = snapshot?.Windows.Take(cursor ? 1 : 2).ToArray();
            if (windows is { Length: > 0 })
            {
                for (var index = 0; index < windows.Length; index++)
                {
                    var meter = new QuotaMeter(MeterLabel(cursor, windows[index]), windows[index], p, _context.CountdownNow, compact: true);
                    _meters.Add(meter); C.Grid.SetColumn(meter, index == 0 ? 1 : 3); section.Children.Add(meter);
                }
            }
            else
            {
                var message = new C.TextBlock { Text = error is not null ? "Unavailable" : snapshot is null ? "Loading…" : "—", FontSize = 11, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center };
                C.Grid.SetColumn(message, 1); section.Children.Add(message);
            }
            if (cursor && _layout.Credit)
            {
                var credit = new C.Grid { Height = 20, VerticalAlignment = W.VerticalAlignment.Center };
                credit.Children.Add(new C.TextBlock { Text = "Credit", FontSize = 11, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center });
                credit.Children.Add(new C.TextBlock { Text = snapshot?.Credits ?? "—", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 13, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text, HorizontalAlignment = W.HorizontalAlignment.Right, VerticalAlignment = W.VerticalAlignment.Center });
                C.Grid.SetColumn(credit, 3); section.Children.Add(credit);
            }
        }
        if (_layout.Codex) Row(false);
        if (_layout.Cursor) Row(true);
        return grid;
    }
    private bool PlatformAt(W.Point point)
    {
        foreach (var (element, cursor) in _platformRegions)
        {
            var origin = element.TransformToAncestor((M.Visual)Surface.Child).Transform(new W.Point());
            if (new W.Rect(origin, element.RenderSize).Contains(point)) return cursor;
        }
        return !_context.Settings.ShowCodex;
    }
    private string Summary()
    {
        var summaries = new List<string>();
        if (_context.Settings.ShowCodex) summaries.Add(UsageLabels.Summary("Codex", _context.Snapshot, _context.CountdownNow));
        if (_context.ShowCursor) summaries.Add(UsageLabels.Summary("Cursor", _context.CursorSnapshot, _context.CountdownNow, _context.Settings.ShowCursorCredit));
        return string.Join("\n\n", summaries);
    }
    internal void UpdateCountdowns()
    {
        foreach(var meter in _meters) meter.UpdateCountdown(_context.CountdownNow);
        foreach (var (element, cursor) in _platformRegions)
            element.ToolTip = UsageLabels.Summary(cursor ? "Cursor" : "Codex", cursor ? _context.CursorSnapshot : _context.Snapshot, _context.CountdownNow, _context.Settings.ShowCursorCredit);
        var summary = Summary();
        if (!Equals(ToolTip, summary)) ToolTip = summary;
    }
    internal void ShowModernMenu()
    {
        if(_menu is { IsDisposed:false }) { _menu.Close(); return; }
        _context.DismissDetails();
        _menu=new ModernMenu(_context,this);  _menu.Show(); _menu.Activate();
    }
    internal void DockToTaskbar()
    {
        if (IsDisposed || IsClosing || _context.IsQuitting) return;
        if (!_layout.HasContent) { Hide(); Attached = false; return; }
        var displays=DisplayCatalog.Read();
        var candidates=displays.Select(d=>new MonitorCandidate(d.DeviceName,d.Identity,d.Screen.Primary)).ToArray();
        var choice=_monitorSelection.Resolve(candidates,_context.Settings.MonitorIdentity,_context.Settings.MonitorDevice,DateTimeOffset.UtcNow);
        if(choice is null) { Hide(); Attached=false; MonitorDevice=null; MonitorIdentity=null; return; }
        var entry=displays.First(d=>d.DeviceName==choice.DeviceName);
        var selected=entry.Screen;
        MonitorDevice=selected.DeviceName; MonitorIdentity=entry.Identity;
        if(!string.IsNullOrWhiteSpace(entry.Identity))
        {
            var legacy=string.IsNullOrWhiteSpace(_context.Settings.MonitorIdentity);
            var matches=string.Equals(entry.Identity,_context.Settings.MonitorIdentity,StringComparison.OrdinalIgnoreCase);
            if((legacy && (string.IsNullOrWhiteSpace(_context.Settings.MonitorDevice) || _context.Settings.MonitorDevice==entry.DeviceName)) || (matches && _context.Settings.MonitorDevice!=entry.DeviceName))
            {
                _context.Settings.MonitorIdentity=entry.Identity; _context.Settings.MonitorDevice=entry.DeviceName; _context.Settings.Save();
            }
        }
        var taskbar=Native.Taskbars().FirstOrDefault(h=>Screen.FromHandle(h).DeviceName==selected.DeviceName);
        if(taskbar==IntPtr.Zero || !Native.GetWindowRect(taskbar,out var r)) { Hide(); Attached=false; return; }
        var scale=Math.Max(96,Native.GetDpiForWindow(taskbar))/96.0;
        var height=Math.Min((int)Math.Round(44*scale),Math.Max(24,r.Bottom-r.Top-4)); var width=(int)Math.Round(Width*scale);
        var x=Math.Clamp((int)((r.Right-r.Left)*_context.Settings.LeftFraction+_context.Settings.Offset*scale),8,Math.Max(8,r.Right-r.Left-width-(int)Math.Round(96*scale))); var y=(r.Bottom-r.Top-height)/2;
        var screen=selected.Bounds;
        if(r.Top>=screen.Bottom-3 || r.Bottom<=screen.Top+3 || Native.IsFullscreenForeground(screen,Handle)) { Hide(); return; }
        var next=new Rectangle(r.Left+x,r.Top+y,width,height);
        Attached=Native.SetWindowPos(Handle,new IntPtr(-1),next.Left,next.Top,width,height,0x0050);
        if(!IsVisible) Show();
    }
}
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string className,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string className,string? title);
    internal static IEnumerable<IntPtr> Taskbars()
    {
        var main=FindWindow("Shell_TrayWnd",null); if(main!=IntPtr.Zero) yield return main;
        var next=IntPtr.Zero; while((next=FindWindowEx(IntPtr.Zero,next,"Shell_SecondaryTrayWnd",null))!=IntPtr.Zero) yield return next;
    }
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int count);
    internal static bool IsTaskbar(IntPtr window)
    {
        if(window == IntPtr.Zero) return false;
        var name = new System.Text.StringBuilder(128); GetClassName(window, name, name.Capacity);
        return name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }
    internal static bool IsFullscreenForeground(Rectangle screen,IntPtr own)
    {
        var foreground=GetForegroundWindow(); if(foreground==IntPtr.Zero || foreground==own || !GetWindowRect(foreground,out var r)) return false;
        var name=new System.Text.StringBuilder(128); GetClassName(foreground,name,name.Capacity);
        if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        return r.Left<=screen.Left && r.Top<=screen.Top && r.Right>=screen.Right && r.Bottom>=screen.Bottom;
    }
}
