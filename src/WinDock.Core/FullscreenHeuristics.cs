namespace WinDock.Core;

public static class FullscreenHeuristics
{
    /// <summary>
    /// Fallback only: a near-monitor-sized window is treated as fullscreen.
    /// Maximized ordinary windows must not yield.
    /// </summary>
    public static bool YieldsForCoveringWindow(
        int windowWidth,
        int windowHeight,
        int monitorWidth,
        int monitorHeight,
        bool maximized)
    {
        if (maximized || monitorWidth <= 0 || monitorHeight <= 0)
        {
            return false;
        }

        return windowWidth >= monitorWidth * 0.97 && windowHeight >= monitorHeight * 0.97;
    }
}
