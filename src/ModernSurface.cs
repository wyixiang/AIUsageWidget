using System.Runtime.InteropServices;
using W = System.Windows;
using C = System.Windows.Controls;
using M = System.Windows.Media;
using H = System.Windows.Interop;
using S = System.Windows.Shell;

namespace CodexTaskbar;

internal sealed class Palette(bool dark)
{
    internal M.SolidColorBrush Text => Brush(dark ? "#F4F6F8" : "#18242E");
    internal M.SolidColorBrush Muted => Brush(dark ? "#BAC4CC" : "#53616C");
    internal M.SolidColorBrush Hover => Brush(dark ? "#16FFFFFF" : "#0D000000");
    internal M.SolidColorBrush Stroke => Brush(dark ? "#29FFFFFF" : "#18000000");
    internal M.SolidColorBrush Card => Brush(dark ? "#20FFFFFF" : "#B3FFFFFF");
    internal M.SolidColorBrush Usage => Brush(dark ? "#88B4FF" : "#366FC9");
    internal M.SolidColorBrush Track => Brush(dark ? "#354356" : "#DCE5EF");
    internal M.SolidColorBrush Accent => Brush(dark ? "#72DEC2" : "#00735C");
    internal static M.SolidColorBrush Brush(string color) => new((M.Color)M.ColorConverter.ConvertFromString(color));
}

internal class GlassWindow : W.Window
{
    private bool _dark;
    internal bool IsDisposed { get; private set; }
    internal bool IsClosing { get; private set; }
    internal bool AcrylicApplied { get; private set; }
    internal readonly Palette Colors;
    internal readonly C.Border Surface;
    internal GlassWindow(bool dark)
    {
        _dark = dark;
        Colors = new(dark);
        WindowStyle = W.WindowStyle.None;
        ResizeMode = W.ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = M.Brushes.Transparent;
        FontFamily = new M.FontFamily("Microsoft YaHei UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        FontSize = 14;
        Foreground = Colors.Text;
        S.WindowChrome.SetWindowChrome(this, new S.WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new W.Thickness(0),
            GlassFrameThickness = new W.Thickness(-1), CornerRadius = new W.CornerRadius(10),
            UseAeroCaptionButtons = false
        });
        Surface = new C.Border
        {
            CornerRadius = new W.CornerRadius(10), BorderThickness = new W.Thickness(1),
            BorderBrush = Colors.Stroke, SnapsToDevicePixels = true,
            Background = Palette.Brush(dark ? "#D925292E" : "#D9FAFAFC")
        };
        Content = Surface;
        M.TextOptions.SetTextFormattingMode(this, M.TextFormattingMode.Display);
        M.TextOptions.SetTextRenderingMode(this, M.TextRenderingMode.Grayscale);
        SourceInitialized += (_, _) => ApplyBackdrop();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => IsDisposed = true;

        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico"));
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e); IsClosing = !e.Cancel;
    }
    internal void UpdateBackdrop(bool dark)
    {
        if (_dark == dark) return;
        _dark = dark; if (W.PresentationSource.FromVisual(this) is not null) ApplyBackdrop();
    }
    private void ApplyBackdrop()
    {
        var handle = new H.WindowInteropHelper(this).Handle;
        var source = H.HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget is { } target) target.BackgroundColor = M.Colors.Transparent;
        try
        {
            int dark = _dark ? 1 : 0, corners = 2, acrylic = 3;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);
            AcrylicApplied = DwmSetWindowAttribute(handle, 38, ref acrylic, sizeof(int)) == 0;
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        if (!AcrylicApplied) Surface.Background = Palette.Brush(_dark ? "#25292E" : "#FAFAFC");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { internal int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    internal static C.Button Button(string label, Palette palette, Action action, string? symbol = null, bool selected = false)
    {
        var button = new C.Button
        {
            Height = 36, Margin = new W.Thickness(0, 1, 0, 1),
            Background = M.Brushes.Transparent, BorderThickness = new W.Thickness(0), Foreground = palette.Text,
            HorizontalContentAlignment = W.HorizontalAlignment.Stretch, FocusVisualStyle = null,
            Padding = new W.Thickness(10, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var border = new W.FrameworkElementFactory(typeof(C.Border));
        border.SetValue(C.Border.CornerRadiusProperty, new W.CornerRadius(5));
        border.SetBinding(C.Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var presenter = new W.FrameworkElementFactory(typeof(C.ContentPresenter));
        presenter.SetBinding(W.FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        presenter.SetValue(W.FrameworkElement.VerticalAlignmentProperty, W.VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new C.ControlTemplate(typeof(C.Button)) { VisualTree = border };
        var hover = new W.Trigger { Property = W.UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new W.Setter(C.Control.BackgroundProperty, palette.Hover));
        template.Triggers.Add(hover);
        var focus = new W.Trigger { Property = W.UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new W.Setter(C.Control.BackgroundProperty, palette.Hover));
        template.Triggers.Add(focus);
        button.Template = template;
        var grid = new C.Grid();
        grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(28) });
        grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(1, W.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(selected ? 20 : 0) });
        grid.Children.Add(new C.TextBlock { Text = symbol ?? "", FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 15, VerticalAlignment = W.VerticalAlignment.Center, Foreground = palette.Muted });
        var text = new C.TextBlock { Text = label, FontSize = 14, VerticalAlignment = W.VerticalAlignment.Center };
        C.Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (selected)
        {
            var check = new C.TextBlock { Text = "✓", Foreground = palette.Accent, FontSize = 15, VerticalAlignment = W.VerticalAlignment.Center };
            C.Grid.SetColumn(check, 2); grid.Children.Add(check);
        }
        button.Content = grid;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }
}

internal sealed class ModernMenu : GlassWindow
{
    private System.Windows.Controls.Primitives.Popup? _submenu;
    private readonly System.Windows.Threading.DispatcherTimer _closeDelay = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private C.Button? _submenuOwner;
    internal ModernMenu(TaskbarContext context, DockWindow dock) : base(context.IsDark)
    {
        Title = "AI 用量快捷菜单";
        ShowInTaskbar = context.InspectUi;
        Width = 254; SizeToContent = W.SizeToContent.Height;
        var stack = new C.StackPanel { Margin = new W.Thickness(6) };
        Surface.Child = stack;
        stack.Children.Add(new C.TextBlock { Text = "AI 用量", FontSize = 12, Foreground = Colors.Muted, Margin = new W.Thickness(12, 5, 0, 6) });
        void HideSubmenu() { _closeDelay.Stop(); if (_submenu is not null) _submenu.IsOpen = false; _submenu = null; _submenuOwner = null; }
        void Item(string label, string icon, Action action, bool selected = false)
        {
            var button = Button(label, Colors, () => { HideSubmenu(); Close(); action(); }, icon, selected);
            button.MouseEnter += (_, _) => HideSubmenu();
            stack.Children.Add(button);
        }
        void Separator() => stack.Children.Add(new C.Border { Height = 1, Background = Colors.Stroke, Margin = new W.Thickness(10, 5, 10, 5) });
        _closeDelay.Tick += (_, _) =>
        {
            _closeDelay.Stop();
            if (_submenuOwner?.IsMouseOver != true && _submenu?.Child.IsMouseOver != true) HideSubmenu();
        };
        void Parent(string label, string icon, Action<C.StackPanel> populate, double width = 192)
        {
            var button = Button(label, Colors, () => { }, icon);
            var content = (C.Grid)button.Content;
            content.ColumnDefinitions[2].Width = new W.GridLength(20);
            var arrow = new C.TextBlock { Text = "\uE76C", FontFamily = new M.FontFamily("Segoe Fluent Icons"), FontSize = 11, Foreground = Colors.Muted, HorizontalAlignment = W.HorizontalAlignment.Right, VerticalAlignment = W.VerticalAlignment.Center };
            C.Grid.SetColumn(arrow, 2); content.Children.Add(arrow);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, "展开子菜单");
            void Open()
            {
                _closeDelay.Stop();
                if (_submenuOwner == button && _submenu?.IsOpen == true) return;
                HideSubmenu();
                var items = new C.StackPanel(); populate(items);
                var border = new C.Border { Padding = new W.Thickness(5), CornerRadius = new W.CornerRadius(8), BorderBrush = Colors.Stroke, BorderThickness = new W.Thickness(1), Background = Palette.Brush(context.IsDark ? "#F5262A30" : "#F5FAFAFC"), Child = items };
                var popup = new System.Windows.Controls.Primitives.Popup { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Right, HorizontalOffset = 4, AllowsTransparency = true, StaysOpen = true, PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade, Width = width, Child = border };
                _submenu = popup; _submenuOwner = button;
                border.MouseEnter += (_, _) => _closeDelay.Stop();
                border.MouseLeave += (_, _) => _closeDelay.Start();
                popup.IsOpen = true;
            }
            button.MouseEnter += (_, _) => Open();
            button.MouseLeave += (_, _) => _closeDelay.Start();
            button.Click += (_, _) => Open();
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Right) { Open(); (_submenu?.Child as C.Border)?.Child.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First)); e.Handled = true; }
            };
            stack.Children.Add(button);
        }
        void Child(C.StackPanel items, string label, string icon, Action action, bool selected)
            => items.Children.Add(Button(label, Colors, () => { HideSubmenu(); Close(); action(); }, icon, selected));
        Item("刷新额度", "\uE72C", () => _ = context.RefreshAsync());
        Item("查看 Codex 额度", "\uE946", () => { context.CursorDetails = false; context.ShowDetails(); });
        Item("查看 Cursor 额度", "\uE8B0", () => { context.CursorDetails = true; context.ShowDetails(); });
        Separator();
        Item("固定位置", "\uE718", () => { context.Settings.Locked = !context.Settings.Locked; context.Settings.Save(); }, context.Settings.Locked);
        Parent("颜色", "\uE790", items =>
        {
            Child(items, "跟随系统", "\uE713", () => { context.Settings.Theme = "system"; context.Repaint(); }, context.Settings.Theme == "system");
            Child(items, "浅色", "\uE706", () => { context.Settings.Theme = "light"; context.Repaint(); }, context.Settings.Theme == "light");
            Child(items, "深色", "\uE708", () => { context.Settings.Theme = "dark"; context.Repaint(); }, context.Settings.Theme == "dark");
        });
        Parent("显示区域", "\uE7F4", items =>
        {
            foreach (var display in DisplayCatalog.Read())
            {
                var target = display;
                var button = Button(display.Label, Colors, () => { HideSubmenu(); Close(); context.SelectDisplay(target); }, "\uE7F4", dock.MonitorDevice == target.DeviceName);
                button.Height = 52;
                button.ToolTip = display.DeviceName;
                var text = ((C.Grid)button.Content).Children.OfType<C.TextBlock>().Skip(1).First();
                text.FontSize = 12; text.TextTrimming = W.TextTrimming.CharacterEllipsis;
                items.Children.Add(button);
            }
        }, 304);
        Separator();
        Item("退出", "\uE8BB", context.Quit);
        Deactivated += (_, _) => { if (!IsDisposed && !IsClosing) Close(); };
        Closing += (_, _) => HideSubmenu();
        Closed += (_, _) => _closeDelay.Stop();
        ContentRendered += (_, _) =>
        {
            var bounds = dock.Bounds;
            var scale = Math.Max(1, Native.GetDpiForWindow(dock.Handle)) / 96.0;
            var screen = Screen.FromRectangle(bounds).WorkingArea;
            Left = Math.Clamp(bounds.Left / scale, screen.Left / scale + 6, screen.Right / scale - Width - 6);
            Top = Math.Max(screen.Top / scale + 6, bounds.Top / scale - ActualHeight - 8);
            (stack.Children.OfType<C.Button>().FirstOrDefault())?.Focus();
        };
    }
}
