using CodexTaskbar;

internal static class TaskbarLayoutChecks
{
    internal static void Run()
    {
        var both = TaskbarLayout.Create(true, true, false, 2);
        Check(both.HasContent && both.IsHorizontal && !both.Credit && both.Width == 466, "horizontal layout reserves two Codex meters and one Cursor meter");
        var credit = TaskbarLayout.Create(true, true, true, 2);
        Check(credit.HorizontalWidth - both.HorizontalWidth == TaskbarLayout.CreditWidth + TaskbarLayout.SectionGap && !credit.IsHorizontal && credit.Width == 425,
            "credit exceeding the horizontal limit switches to the compact stacked layout");
        var shortCredit = TaskbarLayout.Create(true, true, true, 1);
        Check(shortCredit.IsHorizontal && shortCredit.Width - TaskbarLayout.Create(true, true, false, 1).Width == 116,
            "folding credit removes its column and divider when horizontal content fits");
        Check(TaskbarLayout.Create(true, true, true, 2, 582).IsHorizontal && !TaskbarLayout.Create(true, true, true, 2, 581).IsHorizontal,
            "automatic layout changes only when width exceeds the configured limit");
        Check(!TaskbarLayout.Create(true, true, false, 2, 440).IsHorizontal && TaskbarLayout.Create(true, true, true, 2, 640).IsHorizontal,
            "compact and roomy presets apply the same automatic width rule");
        var codex = TaskbarLayout.Create(true, false, true, 2);
        Check(!codex.Credit && codex.Width == 22 + codex.CodexWidth, "hiding Cursor also hides its credit and divider");
        var cursor = TaskbarLayout.Create(false, true, false, 2);
        Check(cursor.Width == 186 && cursor.Width < both.Width, "single-platform display shrinks to its own content");
        Check(TaskbarLayout.Create(false, true, true, 2).IsHorizontal && TaskbarLayout.Create(false, true, true, 2).Width == 302,
            "hiding a platform returns a previously wide card to horizontal layout");
        var empty = TaskbarLayout.Create(false, false, true, 2);
        Check(!empty.HasContent && !empty.Credit && empty.Width == 0, "hiding both providers leaves no empty taskbar card");
        Check(TaskbarLayout.Create(true, true, false, 1).Width == 372, "missing five-hour window removes the unused meter slot");
        Check(TaskbarLayout.Create(true, false, false, 0).CodexMeters == 1 && TaskbarLayout.Create(true, false, false, 8).CodexMeters == 2,
            "loading reserves one meter while extra windows cannot overflow the card");
        var snapshot = new CodexBoardWidget.UsageSnapshot([], "super", "$36.70", DateTimeOffset.Now);
        Check(!UsageLabels.Summary("Cursor", snapshot, includeCredit: false).Contains("Credit") && UsageLabels.Summary("Cursor", snapshot).Contains("$36.70"),
            "folded-credit summary hides the balance without changing stored data");
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
