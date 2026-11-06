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
    private Point? _press;
    private int _startOffset;
    private bool _dragged;
    private ModernMenu? _menu;
    private readonly MonitorSelection _monitorSelection = new();
    internal bool Attached { get; private set; }
    internal string? MonitorDevice { get; private set; }
    internal string? MonitorIdentity { get; private set; }
    internal void PrepareForReconnect() => _monitorSelection.BeginReconnect(DateTimeOffset.UtcNow);
    internal IntPtr Handle => new H.WindowInteropHelper(this).EnsureHandle();
    internal bool Visible => IsVisible;
    internal Rectangle Bounds => Native.GetWindowRect(Handle, out var r) ? Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom) : Rectangle.Empty;
    internal DockWindow(TaskbarContext context) : base(context.IsDark)
    {
        _context = context;
        Title = "Codex 任务栏额度"; ShowActivated = false; ShowInTaskbar = context.InspectUi;
        Width = 360; Height = 40; Surface.CornerRadius = new W.CornerRadius(9);
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
            if (_dragged) context.Settings.Save(); else { context.CursorDetails = e.GetPosition(this).Y >= ActualHeight / 2; context.ShowDetails(); }
        };
        LostMouseCapture += (_, _) => _press = null;
        MouseRightButtonUp += (_, e) => { ShowModernMenu(); e.Handled = true; };
        Closed += (_, _) => { if (_menu is { IsDisposed: false }) _menu.Close(); };
        Invalidate();
    }
    internal void Invalidate()
    {
        var dark = _context.IsDark; UpdateBackdrop(dark); var p = new Palette(dark);
        Surface.Background = Palette.Brush(dark ? "#C8262C33" : "#C8F8FAFC"); Surface.BorderBrush = p.Stroke;
        var grid = new C.Grid { Margin = new W.Thickness(10, 2, 10, 2), Cursor = I.Cursors.Hand };
        grid.RowDefinitions.Add(new C.RowDefinition()); grid.RowDefinitions.Add(new C.RowDefinition());
        foreach(var width in new[]{new W.GridLength(108),new W.GridLength(1,W.GridUnitType.Star),new W.GridLength(10),new W.GridLength(1,W.GridUnitType.Star)}) grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = width });
        void Row(int row, string label, CodexBoardWidget.UsageSnapshot? snapshot, string? error, bool cursor)
        {
            var brand = new C.StackPanel { Orientation = C.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center };
            brand.Children.Add(new C.TextBlock { Text = cursor ? "\uE8B0" : "\uE945", FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 12, Foreground = cursor ? p.Text : p.Accent, VerticalAlignment = W.VerticalAlignment.Center });
            brand.Children.Add(new C.TextBlock { Text = label, FontFamily = new M.FontFamily("Segoe UI"), FontSize = 12, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text, Margin = new W.Thickness(6,0,0,0) });
            if (snapshot?.Plan is { } package) brand.Children.Add(new C.TextBlock { Text = UsageLabels.Plan(package), FontFamily = new M.FontFamily("Segoe UI"), FontSize = 10, Foreground = p.Muted, Margin = new W.Thickness(5, 1, 0, 0), VerticalAlignment = W.VerticalAlignment.Center });
            C.Grid.SetRow(brand,row); grid.Children.Add(brand);
            var windows = snapshot?.Windows.Take(cursor ? 1 : 2).ToArray();
            if(windows is { Length: >0 })
            {
                for(var index=0;index<windows.Length;index++)
                {
                    var quota = windows[index]; var line = new C.StackPanel { Orientation = C.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center };
                    var name = cursor ? quota.Duration==10080 ? "本周" : "套餐" : quota.Duration==300 ? "5h" : "本周";
                    line.Children.Add(new C.TextBlock { Text = name, FontSize = 10, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center, Margin = new W.Thickness(0,0,6,0) });
                    line.Children.Add(new C.TextBlock { Text = $"{quota.Remaining:0}%", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 14, FontWeight = W.FontWeights.SemiBold, Foreground = quota.Remaining<=15 ? Palette.Brush("#B77718") : p.Text });
                    C.Grid.SetRow(line,row); C.Grid.SetColumn(line,index==0?1:3); grid.Children.Add(line);
                }
            }
            else
            {
                var message = new C.TextBlock { Text = error is null ? "正在读取…" : "额度暂不可用", FontSize = 11, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center };
                C.Grid.SetRow(message,row); C.Grid.SetColumn(message,1); C.Grid.SetColumnSpan(message,3); grid.Children.Add(message);
            }
        }
        Row(0,"Codex",_context.Snapshot,_context.Error,false);
        Row(1,"Cursor",_context.CursorSnapshot,_context.CursorError,true);
        ToolTip = _context.Error is not null || _context.CursorError is not null ? "部分额度未更新。点击详情切换 Codex / Cursor 查看状态。" : "剩余额度 · 单击详情 · 右键设置屏幕和位置";
        if (_context.CursorSnapshot is { } cursorSnapshot)
        {
            var creditLine = new C.StackPanel { Orientation = C.Orientation.Horizontal, VerticalAlignment = W.VerticalAlignment.Center };
            creditLine.Children.Add(new C.TextBlock { Text = "Credit", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 10, Foreground = p.Muted, VerticalAlignment = W.VerticalAlignment.Center, Margin = new W.Thickness(0, 0, 5, 0) });
            creditLine.Children.Add(new C.TextBlock { Text = cursorSnapshot.Credits ?? "—", FontFamily = new M.FontFamily("Segoe UI"), FontSize = 13, FontWeight = W.FontWeights.SemiBold, Foreground = p.Text });
            C.Grid.SetRow(creditLine, 1); C.Grid.SetColumn(creditLine, 3); grid.Children.Add(creditLine);
        }
        ToolTip = UsageLabels.Summary("Codex", _context.Snapshot) + "\n\n" + UsageLabels.Summary("Cursor", _context.CursorSnapshot);
        Surface.Child = grid;
    }
    internal void ShowModernMenu()
    {
        if(_menu is { IsDisposed:false }) { _menu.Close(); return; }
        _menu=new ModernMenu(_context,this);  _menu.Show(); _menu.Activate();
    }
    internal void DockToTaskbar()
    {
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
        var height=Math.Min((int)Math.Round(40*scale),Math.Max(24,r.Bottom-r.Top-4)); var width=(int)Math.Round(360*scale);
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
    internal static bool IsFullscreenForeground(Rectangle screen,IntPtr own)
    {
        var foreground=GetForegroundWindow(); if(foreground==IntPtr.Zero || foreground==own || !GetWindowRect(foreground,out var r)) return false;
        var name=new System.Text.StringBuilder(128); GetClassName(foreground,name,name.Capacity);
        if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        return r.Left<=screen.Left && r.Top<=screen.Top && r.Right>=screen.Right && r.Bottom>=screen.Bottom;
    }
}
