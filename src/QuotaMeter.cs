using CodexBoardWidget;
using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
namespace CodexTaskbar;
internal sealed class QuotaMeter : C.Grid
{
    internal readonly UsageWindow Window;
    internal readonly C.ProgressBar UsageBar;
    internal readonly C.TextBlock ResetText;
    private readonly C.TextBlock _resetIcon;
    internal QuotaMeter(string name,UsageWindow window,Palette palette,DateTimeOffset now)
    {
        Window=window;
        VerticalAlignment=W.VerticalAlignment.Center;
        Height=20;
        RowDefinitions.Add(new C.RowDefinition { Height=new W.GridLength(18) });
        RowDefinitions.Add(new C.RowDefinition { Height=new W.GridLength(2) });
        ColumnDefinitions.Add(new C.ColumnDefinition { Width=new W.GridLength(24) });
        ColumnDefinitions.Add(new C.ColumnDefinition { Width=new W.GridLength(44) });
        ColumnDefinitions.Add(new C.ColumnDefinition { Width=new W.GridLength(1,W.GridUnitType.Star) });
        ColumnDefinitions.Add(new C.ColumnDefinition { Width=new W.GridLength(64) });
        Children.Add(new C.TextBlock { Text=name,FontSize=11,Foreground=palette.Muted,VerticalAlignment=W.VerticalAlignment.Center });
        var amount=new C.TextBlock { Text=$"{window.Remaining:0}%",FontFamily=new M.FontFamily("Segoe UI"),FontSize=13,FontWeight=W.FontWeights.SemiBold,Foreground=palette.Text,TextAlignment=W.TextAlignment.Right,VerticalAlignment=W.VerticalAlignment.Center };
        amount.Typography.NumeralAlignment=W.FontNumeralAlignment.Tabular;
        C.Grid.SetColumn(amount,1); Children.Add(amount);
        var fill=window.Remaining<=15?Palette.Brush("#B77718"):palette.Usage;
        UsageBar=new C.ProgressBar { Minimum=0,Maximum=100,Value=Math.Clamp(window.Remaining,0,100),Height=2,VerticalAlignment=W.VerticalAlignment.Center,Foreground=fill,Background=palette.Track,BorderThickness=new W.Thickness(0),IsHitTestVisible=false,Focusable=false };
        var background=new W.FrameworkElementFactory(typeof(C.Border));
        background.SetValue(C.Border.CornerRadiusProperty,new W.CornerRadius(2)); background.SetValue(C.Border.BackgroundProperty,palette.Track);
        var track=new W.FrameworkElementFactory(typeof(C.Grid),"PART_Track");
        var indicator=new W.FrameworkElementFactory(typeof(C.Border),"PART_Indicator");
        indicator.SetValue(C.Border.CornerRadiusProperty,new W.CornerRadius(2)); indicator.SetValue(C.Border.BackgroundProperty,fill); indicator.SetValue(W.FrameworkElement.HorizontalAlignmentProperty,W.HorizontalAlignment.Left);
        track.AppendChild(indicator); background.AppendChild(track);
        UsageBar.Template=new C.ControlTemplate(typeof(C.ProgressBar)) { VisualTree=background };
        W.Automation.AutomationProperties.SetName(UsageBar,$"{name}剩余用量");
        C.Grid.SetRow(UsageBar,1); C.Grid.SetColumnSpan(UsageBar,4); Children.Add(UsageBar);
        var resetGroup=new C.StackPanel { Orientation=C.Orientation.Horizontal,HorizontalAlignment=W.HorizontalAlignment.Right,VerticalAlignment=W.VerticalAlignment.Center,ToolTip="距额度重置" };
        ResetText=new C.TextBlock { FontFamily=new M.FontFamily("Segoe UI"),FontSize=11,Foreground=palette.Muted,VerticalAlignment=W.VerticalAlignment.Center,TextAlignment=W.TextAlignment.Right };
        ResetText.Typography.NumeralAlignment=W.FontNumeralAlignment.Tabular;
        _resetIcon=new C.TextBlock { Text="\uE72C",FontFamily=new M.FontFamily("Segoe Fluent Icons"),FontSize=10,Width=11,Foreground=palette.Muted,Margin=new W.Thickness(3,0,0,0),VerticalAlignment=W.VerticalAlignment.Center };
        W.Automation.AutomationProperties.SetName(_resetIcon,"Quota reset");
        resetGroup.Children.Add(ResetText); resetGroup.Children.Add(_resetIcon);
        C.Grid.SetColumn(resetGroup,3); Children.Add(resetGroup);
        UpdateCountdown(now);
    }
    internal void UpdateCountdown(DateTimeOffset now)
    {
        ResetText.Text=UsageLabels.BarCountdown(Window.ResetsAt,Window.Duration,now);
        _resetIcon.Visibility=ResetText.Text=="—" ? W.Visibility.Hidden : W.Visibility.Visible;
    }
}
