using CodexBoardWidget;
using W = System.Windows;
using C = System.Windows.Controls;

namespace CodexTaskbar;

internal sealed class WeeklyPaceMark : C.Border
{
    private readonly UsageWindow _window;
    internal WeeklyPace? Pace { get; private set; }
    internal double TopOffset { get; set; }
    internal WeeklyPaceMark(UsageWindow window, Palette palette, double height)
    {
        _window = window;
        Width = 2; Height = height;
        CornerRadius = new W.CornerRadius(1);
        Background = palette.PaceMarker;
        HorizontalAlignment = W.HorizontalAlignment.Left;
        VerticalAlignment = W.VerticalAlignment.Center;
        IsHitTestVisible = false;
        Visibility = W.Visibility.Collapsed;
        W.Automation.AutomationProperties.SetName(this, "工作日预期剩余额度基准线");
    }
    internal WeeklyPace? Update(DateTimeOffset now, double width)
    {
        Pace = WeeklyPace.Evaluate(_window, now);
        Visibility = Pace is null ? W.Visibility.Collapsed : W.Visibility.Visible;
        Position(width);
        return Pace;
    }
    internal void Position(double width)
    {
        if (Pace is null || width <= 0 || !double.IsFinite(width)) return;
        Margin = new W.Thickness(Math.Clamp(width * Pace.ExpectedRemainingPercent / 100 - Width / 2, 0, Math.Max(0, width - Width)), TopOffset, 0, 0);
    }
}
