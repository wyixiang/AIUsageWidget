namespace CodexTaskbar;

internal sealed record TaskbarLayout(bool Codex, bool Cursor, bool Credit, int CodexMeters, int HorizontalLimit)
{
    internal const double ContentInset = 11; // Outer edge to content, including the surface border.
    internal const double BrandWidth = 78;
    internal const double MeterWidth = 86;
    internal const double MeterGap = 8;
    internal const double SectionGap = 22;
    internal const double CreditWidth = 94;
    internal const double CompactBrandWidth = 107;
    internal const double CompactMeterWidth = 143;
    internal const double CompactMeterGap = 10;
    internal bool HasContent => Codex || Cursor;
    internal double CodexWidth => BrandWidth + CodexMeters * MeterWidth + (CodexMeters - 1) * MeterGap;
    internal double CursorWidth => BrandWidth + MeterWidth;
    internal double HorizontalWidth => HasContent ? 2 * ContentInset + (Codex ? CodexWidth : 0) + (Cursor ? CursorWidth : 0) +
        (Credit ? CreditWidth : 0) + (Sections - 1) * SectionGap : 0;
    internal bool IsHorizontal => HorizontalWidth <= HorizontalLimit;
    internal int StackedColumns => Math.Max(Codex ? CodexMeters : 0, Cursor ? Credit ? 2 : 1 : 0);
    internal double Width => !HasContent ? 0 : IsHorizontal ? HorizontalWidth : 2 * ContentInset + CompactBrandWidth +
        StackedColumns * CompactMeterWidth + (StackedColumns - 1) * CompactMeterGap;
    private int Sections => (Codex ? 1 : 0) + (Cursor ? 1 : 0) + (Credit ? 1 : 0);
    internal static TaskbarLayout Create(bool codex, bool cursor, bool credit, int codexWindows, int horizontalLimit = 520)
        => new(codex, cursor, cursor && credit, Math.Clamp(codexWindows, 1, 2), Math.Clamp(horizontalLimit, 300, 900));
}
