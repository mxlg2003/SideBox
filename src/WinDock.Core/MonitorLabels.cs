namespace WinDock.Core;

public static class MonitorLabels
{
    public const string FollowPrimary = "始终跟着 Windows 主屏";

    public static string For(MonitorInfo monitor, int index)
    {
        var width = Math.Max(1, (int)Math.Round(monitor.Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(monitor.Bounds.Height));
        var size = $"{width}×{height}";
        return monitor.IsPrimary ? $"主屏 · {size}" : $"屏幕 {index} · {size}";
    }
}
