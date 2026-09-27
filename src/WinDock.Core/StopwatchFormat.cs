namespace WinDock.Core;

/// <summary>Stopwatch copy and elapsed formatting for the dock pill and flyout.</summary>
public static class StopwatchFormat
{
    public static string Ready => UiLocale.T("就绪", "Ready");
    public static string Running => UiLocale.T("运行中", "Running");
    public static string Paused => UiLocale.T("已暂停", "Paused");
    public static string Start => UiLocale.T("开始", "Start");
    public static string Pause => UiLocale.T("暂停", "Pause");
    public static string Resume => UiLocale.T("继续", "Resume");
    public static string Reset => UiLocale.T("重置", "Reset");

    public static string Time(TimeSpan elapsed)
    {
        var total = Math.Max(0, (long)elapsed.TotalSeconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    public static string Status(bool running, TimeSpan elapsed) =>
        running ? Running : elapsed <= TimeSpan.Zero ? Ready : Paused;

    public static string PrimaryAction(bool running, TimeSpan elapsed) =>
        running ? Pause : elapsed <= TimeSpan.Zero ? Start : Resume;
}
