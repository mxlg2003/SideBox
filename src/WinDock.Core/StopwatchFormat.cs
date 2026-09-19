namespace WinDock.Core;

/// <summary>Stopwatch copy and elapsed formatting for the dock pill and flyout.</summary>
public static class StopwatchFormat
{
    public const string Ready = "就绪";
    public const string Running = "运行中";
    public const string Paused = "已暂停";
    public const string Start = "开始";
    public const string Pause = "暂停";
    public const string Resume = "继续";
    public const string Reset = "复位";

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
