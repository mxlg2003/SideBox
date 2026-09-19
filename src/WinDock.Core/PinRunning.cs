namespace WinDock.Core;

public static class PinRunning
{
    public static bool Tracks(DockItem item) =>
        item.Type == DockItemType.App
        || item.Type == DockItemType.File
           && item.Path is not null
           && item.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlySet<string> TrackedProcessNames(IEnumerable<DockItem> items)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!Tracks(item))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(item.Path);
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    public static HashSet<string> ActiveIds(IEnumerable<DockItem> items, IReadOnlySet<string> runningImages)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (IsActive(item, runningImages))
            {
                ids.Add(item.Id);
            }
        }

        return ids;
    }

    public static bool IsActive(DockItem item, IReadOnlySet<string> runningImages)
    {
        if (!Tracks(item))
        {
            return false;
        }

        var path = DockPins.NormalizePath(item.Path);
        if (path.Length == 0)
        {
            return false;
        }

        if (runningImages.Contains(path))
        {
            return true;
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        foreach (var running in runningImages)
        {
            if (running.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(running), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsConsoleHost(string? imagePath)
    {
        var name = Path.GetFileNameWithoutExtension(imagePath);
        return name is not null
               && (name.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("OpenConsole", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("conhost", StringComparison.OrdinalIgnoreCase));
    }
}
