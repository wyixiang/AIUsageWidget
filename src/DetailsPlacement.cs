using System.Drawing;
namespace CodexTaskbar;

internal static class DetailsPlacement
{
    // All inputs are physical pixels on the anchor monitor, including negative coordinates.
    internal static Rectangle Above(Rectangle anchor, Rectangle workArea, Size size, int gap, int margin)
    {
        var width = Math.Clamp(size.Width, 1, Math.Max(1, workArea.Width - 2 * margin));
        var height = Math.Clamp(size.Height, 1, Math.Max(1, workArea.Height - 2 * margin));
        var left = Math.Clamp(anchor.Left, workArea.Left + margin, Math.Max(workArea.Left + margin, workArea.Right - margin - width));
        var top = Math.Clamp(anchor.Top - gap - height, workArea.Top + margin, Math.Max(workArea.Top + margin, workArea.Bottom - margin - height));
        return new Rectangle(left, top, width, height);
    }
}
