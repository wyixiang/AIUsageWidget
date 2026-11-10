using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
namespace CodexTaskbar;

internal static class GaugeIcon
{
    internal static BitmapSource Render(int pixels, bool dark)
        => Render(pixels, dark ? Color.FromRgb(244,246,248) : Color.FromRgb(24,36,46));

    internal static BitmapSource Render(int pixels, Color ink)
    {
        var visual = ProductIcons.Gauge(new SolidColorBrush(ink), pixels);
        visual.Measure(new System.Windows.Size(pixels, pixels));
        visual.Arrange(new System.Windows.Rect(0, 0, pixels, pixels));
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    internal static Icon Create(int pixels, bool dark)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(pixels, dark)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var bitmap = new Bitmap(stream);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
