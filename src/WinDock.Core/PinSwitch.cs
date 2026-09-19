namespace WinDock.Core;

public static class PinSwitch
{
    public static bool PrefersExistingWindow(DockItem item) => PinRunning.Tracks(item);

    public static int PickWindow(int count, int foregroundIndex)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (foregroundIndex < 0)
        {
            return 0;
        }

        return (foregroundIndex + 1) % count;
    }

    public static bool TitleLooksLike(string? windowTitle, DockItem item)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        var exe = Path.GetFileNameWithoutExtension(item.Path);
        if (!string.IsNullOrWhiteSpace(exe)
            && windowTitle.Contains(exe, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(item.Title)
               && windowTitle.Contains(item.Title, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAppFrame(int width, int height) =>
        width >= 280 && height >= 180 && (long)width * height >= 120_000;
}
