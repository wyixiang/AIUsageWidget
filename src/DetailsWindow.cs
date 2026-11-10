using System.Diagnostics;
using W = System.Windows;
using C = System.Windows.Controls;

namespace CodexTaskbar;

internal sealed class DetailsWindow : GlassWindow
{
    private readonly TaskbarContext _context;
    private bool _closeForTheme;
    private bool _positioning;
    private readonly C.StackPanel _body;
    private readonly List<(C.TextBlock Text, CodexBoardWidget.UsageWindow Window)> _resetTimes = new();
    private readonly C.Button _codexTab, _cursorTab;
    internal DetailsWindow(TaskbarContext context) : base(context.IsDark)
    {
        _context = context;
        Title = "AI 用量";
        Width = 410; SizeToContent = W.SizeToContent.Height;
        ShowInTaskbar = true; Topmost = false; ShowActivated = true; ResizeMode = W.ResizeMode.CanMinimize;
        IsVisibleChanged += (_, _) => context.WriteStatus();
        StateChanged += (_, _) => context.WriteStatus();
        Deactivated += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            // Deactivation may precede minimization; wait for the final window state.
            if (!context.IsQuitting && !_closeForTheme && !IsDisposed && !IsClosing && IsVisible && !IsActive && WindowState == W.WindowState.Normal) Dismiss();
        }));
        SizeChanged += (_, _) => context.PositionDetails(this);
        ContentRendered += (_, _) => context.PositionDetails(this);
        WindowStartupLocation = W.WindowStartupLocation.Manual;
        var layout = new C.StackPanel { Margin = new W.Thickness(22, 16, 22, 20) };
        Surface.Child = new C.ScrollViewer { Content = layout, VerticalScrollBarVisibility = C.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = C.ScrollBarVisibility.Disabled };
        var header = new C.Grid { Margin = new W.Thickness(0, 0, 0, 12) };
        header.Children.Add(new C.TextBlock { Text = "AI 用量", FontSize = 20, FontWeight = W.FontWeights.SemiBold, VerticalAlignment = W.VerticalAlignment.Center });
        C.Button Caption(string name, string glyph, Action action)
        {
            var button = Button(name, Colors, action);
            button.Width = 32; button.Height = 32; button.Padding = new W.Thickness(0); button.ToolTip = name;
            button.Content = new C.TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons"), FontSize = 14, HorizontalAlignment = W.HorizontalAlignment.Center, VerticalAlignment = W.VerticalAlignment.Center };
            return button;
        }
        var captions = new C.StackPanel { Orientation = C.Orientation.Horizontal, HorizontalAlignment = W.HorizontalAlignment.Right };
        captions.Children.Add(Caption("最小化", "\uE921", () => WindowState = W.WindowState.Minimized));
        captions.Children.Add(Caption("关闭到系统托盘", "\uE8BB", Dismiss));
        header.Children.Add(captions);
        Closing += (_, e) => { if (!context.IsQuitting && !_closeForTheme) { e.Cancel = true; Dismiss(); } };
        layout.Children.Add(header);
        var tabs = new C.Grid { Margin = new W.Thickness(0, 0, 0, 8) };
        tabs.ColumnDefinitions.Add(new C.ColumnDefinition()); tabs.ColumnDefinitions.Add(new C.ColumnDefinition());
        var codex = _codexTab = Button("Codex", Colors, () => { context.CursorDetails = false; UpdateValues(); }, "\uE945");
        var cursor = _cursorTab = Button("Cursor", Colors, () => { context.CursorDetails = true; UpdateValues(); }, "\uE8B0");
        var codexContent=(C.Grid)codex.Content; codexContent.Children.RemoveAt(0); codexContent.Children.Insert(0, ProductIcons.Create(false, Colors.Muted));
        var cursorContent=(C.Grid)cursor.Content; cursorContent.Children.RemoveAt(0); cursorContent.Children.Insert(0, ProductIcons.Create(true, Colors.Muted));
        C.Grid.SetColumn(cursor, 1); tabs.Children.Add(codex); tabs.Children.Add(cursor); layout.Children.Add(tabs);
        _body = new C.StackPanel(); layout.Children.Add(_body);
        var footer = new C.StackPanel { Margin = new W.Thickness(0, 12, 0, 0) };
        footer.Children.Add(Button("刷新额度", Colors, () => _ = context.RefreshAsync(), "\uE72C"));
        footer.Children.Add(Button("打开当前平台用量页", Colors, () => Process.Start(new ProcessStartInfo(context.CursorDetails ? "https://cursor.com/dashboard/usage" : "https://chatgpt.com/settings/usage") { UseShellExecute = true }), "\uE774"));
        layout.Children.Add(footer);
    }
    internal void Dismiss()
    {
        if (IsDisposed || IsClosing || _context.IsQuitting) return;
        Hide();
        _context.RestoreDockLayer();
    }
    internal void PositionAbove(Rectangle anchor, Rectangle workArea, double scale)
    {
        if (_positioning || IsDisposed || !IsVisible || WindowState != W.WindowState.Normal || anchor.IsEmpty) return;
        _positioning = true;
        try
        {
            var margin = (int)Math.Round(8 * scale);
            var gap = (int)Math.Round(10 * scale);
            MaxWidth = Math.Max(1, (workArea.Width - 2 * margin) / scale);
            MaxHeight = Math.Max(1, (Math.Min(workArea.Bottom, anchor.Top - gap) - workArea.Top - margin) / scale);
            UpdateLayout();
            var rect = DetailsPlacement.Above(anchor, workArea, new Size((int)Math.Ceiling(ActualWidth * scale), (int)Math.Ceiling(ActualHeight * scale)), gap, margin);
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Native.SetWindowPos(handle, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height, 0x0014);
        }
        finally { _positioning = false; }
    }
    internal void CloseForTheme() { _closeForTheme = true; Close(); }
    internal void UpdateCountdowns()
    {
        foreach(var (text, window) in _resetTimes) text.Text = UsageLabels.Reset(window.ResetsAt, window.Duration, _context.CountdownNow);
    }
    internal void UpdateValues()
    {
        _codexTab.Background = _context.CursorDetails ? System.Windows.Media.Brushes.Transparent : Colors.Hover;
        _cursorTab.Background = _context.CursorDetails ? Colors.Hover : System.Windows.Media.Brushes.Transparent;
        _resetTimes.Clear(); _body.Children.Clear();
        var error = _context.CursorDetails ? _context.CursorError : _context.Error;
        if ((_context.CursorDetails ? _context.CursorSnapshot : _context.Snapshot) is not { } snapshot)
        {
            _body.Children.Add(new C.TextBlock { Text = error ?? "正在读取额度…", TextWrapping = W.TextWrapping.Wrap, Foreground = Colors.Muted, Margin = new W.Thickness(0, 8, 0, 8) });
            return;
        }
        _body.Children.Add(new C.TextBlock { Text = "当前套餐  ·  " + UsageLabels.Plan(snapshot.Plan), FontSize = 14, FontWeight = W.FontWeights.SemiBold, Foreground = Colors.Text, Margin = new W.Thickness(1, 2, 1, 10) });
        foreach (var w in snapshot.Windows)
        {
            var content = new C.StackPanel { Margin = new W.Thickness(15, 12, 15, 12) };
            content.Children.Add(new C.TextBlock { Text = w.Name, Foreground = Colors.Muted, FontSize = 13 });
            var amount = new C.StackPanel { Orientation = C.Orientation.Horizontal, Margin = new W.Thickness(0, 5, 0, 10) };
            amount.Children.Add(new C.TextBlock { Text = $"{w.Remaining:0.#}%", FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 30, FontWeight = W.FontWeights.SemiBold, Foreground = Colors.Accent });
            amount.Children.Add(new C.TextBlock { Text = "剩余", FontSize = 12, Foreground = Colors.Muted, Margin = new W.Thickness(9, 0, 0, 5), VerticalAlignment = W.VerticalAlignment.Bottom });
            content.Children.Add(amount);
            var track = new C.Grid { Height = 5, Margin = new W.Thickness(0, 0, 0, 9) };
            track.Children.Add(new C.Border { CornerRadius = new W.CornerRadius(3), Background = Colors.Stroke });
            var fill = new C.Border { CornerRadius = new W.CornerRadius(3), Background = Colors.Accent, HorizontalAlignment = W.HorizontalAlignment.Left };
            track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * Math.Clamp(w.Remaining / 100, 0, 1);
            track.Children.Add(fill); content.Children.Add(track);
            var resetText = new C.TextBlock { Text = UsageLabels.Reset(w.ResetsAt, w.Duration, _context.CountdownNow), FontSize = 12, Foreground = Colors.Muted, TextWrapping = W.TextWrapping.Wrap };
            content.Children.Add(resetText); _resetTimes.Add((resetText, w));
            _body.Children.Add(new C.Border { Background = Colors.Card, BorderBrush = Colors.Stroke, BorderThickness = new W.Thickness(1), CornerRadius = new W.CornerRadius(8), Margin = new W.Thickness(0, 4, 0, 4), Child = content });
        }
        if (_context.CursorDetails)
        {
            var credit = new C.StackPanel { Margin = new W.Thickness(15, 12, 15, 12) };
            credit.Children.Add(new C.TextBlock { Text = "Credit 余额", FontSize = 13, Foreground = Colors.Muted });
            credit.Children.Add(new C.TextBlock { Text = snapshot.Credits ?? "暂不可用", FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 28, FontWeight = W.FontWeights.SemiBold, Foreground = Colors.Text, Margin = new W.Thickness(0, 4, 0, 8) });
            credit.Children.Add(new C.TextBlock { Text = "独立余额 · 不随每周额度重置", FontSize = 12, Foreground = Colors.Muted });
            _body.Children.Add(new C.Border { Background = Colors.Card, BorderBrush = Colors.Stroke, BorderThickness = new W.Thickness(1), CornerRadius = new W.CornerRadius(8), Margin = new W.Thickness(0, 4, 0, 4), Child = credit });
        }
        var status = $"更新于 {snapshot.FetchedAt:HH:mm:ss} · 每 1 分钟刷新";

        if (error is not null) status += "\n本次刷新失败，以上为上次数据。";
        _body.Children.Add(new C.TextBlock { Text = status, FontSize = 12, Foreground = Colors.Muted, TextWrapping = W.TextWrapping.Wrap, Margin = new W.Thickness(1, 12, 1, 0) });
    }
}
