namespace WinDock.Core;

public static class DockPins
{
    public static bool IsPin(DockItemType type) =>
        type is DockItemType.App or DockItemType.File or DockItemType.Folder;

    public static DockItem FromTarget(string target, string? title = null, string? iconPath = null, string? shortcutPath = null)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("A pin needs a path.");
        }

        var trimmed = target.Trim().Trim('"');
        var shortcut = string.IsNullOrWhiteSpace(shortcutPath) ? null : NormalizePath(shortcutPath);
        if (TryUrl(trimmed, out _))
        {
            throw new InvalidOperationException("Web URL pins are not supported.");
        }

        var path = NormalizePath(trimmed);
        var type = TypeForPath(path);
        var name = string.IsNullOrWhiteSpace(title) ? DisplayName(path, type) : title.Trim();
        return new DockItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Path = path,
            Title = name,
            IconPath = iconPath,
            ShortcutPath = shortcut
        };
    }

    public static bool SameTarget(DockItem item, DockItem other)
    {
        if (!IsPin(item.Type) || !IsPin(other.Type))
        {
            return false;
        }

        return string.Equals(NormalizePath(item.Path), NormalizePath(other.Path), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().Trim('"');
        try
        {
            var full = Path.GetFullPath(trimmed);
            if (full.Length > 3)
            {
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return full;
        }
        catch (Exception)
        {
            return trimmed;
        }
    }

    public static bool TryUrl(string value, out string url)
    {
        url = value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        url = uri.AbsoluteUri;
        return true;
    }

    public static string FolderBadge(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? "?" : title.Trim();
    }

    private static DockItemType TypeForPath(string path)
    {
        if (Directory.Exists(path)
            || (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            && !Path.HasExtension(path))
        {
            return DockItemType.Folder;
        }

        return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
            ? DockItemType.App
            : DockItemType.File;
    }

    private static string DisplayName(string path, DockItemType type)
    {
        if (type == DockItemType.Folder)
        {
            var name = new DirectoryInfo(path).Name;
            return string.IsNullOrWhiteSpace(name) ? path : name;
        }

        var file = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(file) ? path : file;
    }
}
