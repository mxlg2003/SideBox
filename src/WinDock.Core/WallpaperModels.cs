namespace WinDock.Core;

public enum WallpaperSource
{
    Curated = 0,
    Local = 1,
    Unsplash = 2,
    Bing = 3
}

public enum WallpaperRotation
{
    Daily = 0,
    Interval1Hour = 1,
    Interval4Hours = 2,
    Interval30Min = 3,
    Interval15Min = 4,
    Startup = 5,
    Manual = 6
}

public sealed record WallpaperItem
{
    public required string Id { get; init; }

    public required WallpaperSource Source { get; init; }

    public required string Url { get; init; }

    public string? LocalPath { get; init; }

    public string Title { get; init; } = "";

    public string Author { get; init; } = "";

    public string AuthorUrl { get; init; } = "";

    public string PageUrl { get; init; } = "";

    public string DominantColor { get; init; } = "#1e293b";

    public string? DownloadLocation { get; init; }
}

public sealed record WallpaperSettings
{
    public WallpaperSource Source { get; init; } = WallpaperSource.Curated;

    public WallpaperRotation Rotation { get; init; } = WallpaperRotation.Daily;

    public bool AutoApplyDesktop { get; init; }

    public string LocalFolder { get; init; } = "";

    public bool LocalRecursive { get; init; } = true;

    public string LocalCurrent { get; init; } = "";

    public IReadOnlyList<string> LocalSeen { get; init; } = [];

    public string LocalChosenOn { get; init; } = "";

    public string UnsplashAccessKey { get; init; } = "";

    public string UnsplashQuery { get; init; } = "";

    public string? CurrentId { get; init; }

    public string? ChosenOn { get; init; }

    public string? LastAppliedKey { get; init; }

    public static WallpaperSettings Default { get; } = new();

    public static WallpaperSettings Normalize(WallpaperSettings? settings) =>
        settings ?? Default;
}

public sealed record WallpaperSnapshot
{
    public WallpaperItem? Current { get; init; }

    public IReadOnlyList<WallpaperItem> Recent { get; init; } = [];

    public int SelectedIndex { get; init; }

    public bool IsBusy { get; init; }

    public string? Error { get; init; }

    public string? AppliedKey { get; init; }

    public bool AutoApplyDesktop { get; init; }

    public WallpaperSettings Settings { get; init; } = WallpaperSettings.Default;
}

/// <summary>
/// Cycle-based shuffle picker algorithm (ported from qiaomu-home).
/// Guarantees each item in a pool is visited once per cycle without repeat,
/// and avoids consecutive duplicate when rolling over to a new cycle.
/// </summary>
public static class LocalShufflePicker
{
    public static (string Current, List<string> Seen) PickNext(
        IReadOnlyList<string> paths,
        string current,
        IEnumerable<string> seen,
        Random? random = null)
    {
        if (paths.Count == 0)
        {
            return ("", []);
        }

        if (paths.Count == 1)
        {
            return (paths[0], [paths[0]]);
        }

        random ??= Random.Shared;
        var available = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        var visited = seen.Where(available.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var pool = paths.Where(p => !visited.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();

        if (pool.Count == 0)
        {
            visited.Clear();
            pool = paths.ToList();
        }

        if (pool.Count > 1 && !string.IsNullOrEmpty(current))
        {
            pool.RemoveAll(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase));
        }

        var next = pool[random.Next(pool.Count)];
        visited.Add(next);
        return (next, visited);
    }
}

/// <summary>
/// Helper for step-wise screen-adaptive image resolutions to optimize caching.
/// </summary>
public static class WallpaperResolutionHelper
{
    public static int CalculateTargetWidth(double screenWidth, double dpiScale)
    {
        var needed = Math.Clamp(screenWidth * Math.Max(dpiScale, 1.0), 1280, 3840);
        return (int)(Math.Ceiling(needed / 640.0) * 640);
    }
}
