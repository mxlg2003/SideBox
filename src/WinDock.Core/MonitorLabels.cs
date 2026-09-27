namespace WinDock.Core;

public static class MonitorLabels
{
    public static string FollowPrimary => UiText.FollowPrimary;

    public static string For(MonitorInfo monitor, int index)
    {
        var width = Math.Max(1, (int)Math.Round(monitor.Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(monitor.Bounds.Height));
        var size = $"{width}×{height}";
        return monitor.IsPrimary ? UiText.PrimaryMonitor(size) : UiText.OtherMonitor(index, size);
    }
}
