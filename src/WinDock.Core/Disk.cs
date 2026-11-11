namespace WinDock.Core;

public static class DiskMains
{
    public const string UsedPercent = "usedPercent";
    public const string Free = "free";

    public static readonly IReadOnlyList<string> All = [UsedPercent, Free];

    public static string Normalize(string? main) => main switch
    {
        Free => Free,
        _ => UsedPercent
    };
}

public sealed record DiskVolumeSnapshot
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Label { get; init; }

    public ulong TotalBytes { get; init; }

    public ulong FreeBytes { get; init; }

    public ulong UsedBytes => TotalBytes > FreeBytes ? TotalBytes - FreeBytes : 0;

    public double UsedPercent => TotalBytes == 0 ? 0 : Math.Clamp(UsedBytes * 100.0 / TotalBytes, 0, 100);
}

public sealed record DiskSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public IReadOnlyList<DiskVolumeSnapshot> Volumes { get; init; } = [];

    public IReadOnlyList<double> ActivityHistory { get; init; } = [];

    public bool ActivityAvailable { get; init; }
}

public sealed record DiskSampleOptions
{
    public bool Activity { get; init; }

    public static DiskSampleOptions Default { get; } = new();
}

public sealed record DiskWidgetSettings
{
    public string Main { get; init; } = DiskMains.UsedPercent;

    public IReadOnlyList<string> HiddenVolumeIds { get; init; } = [];

    public static DiskWidgetSettings Default { get; } = new();

    public static DiskWidgetSettings Normalize(DiskWidgetSettings? settings)
    {
        var value = settings ?? Default;
        var hidden = value.HiddenVolumeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return value with
        {
            Main = DiskMains.Normalize(value.Main),
            HiddenVolumeIds = hidden
        };
    }

    public static IReadOnlyList<DiskVolumeSnapshot> Visible(
        IReadOnlyList<DiskVolumeSnapshot> volumes,
        DiskWidgetSettings? settings)
    {
        var hidden = Normalize(settings).HiddenVolumeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (hidden.Count == 0)
        {
            return volumes;
        }

        return volumes.Where(volume => !hidden.Contains(volume.Id)).ToArray();
    }

    public bool IsVisible(string volumeId) =>
        !HiddenVolumeIds.Contains(volumeId, StringComparer.OrdinalIgnoreCase);
}

public static class DiskFormat
{
    private const double Gb = 1024.0 * 1024.0 * 1024.0;

    public static string MainTitle(string main) => DiskMains.Normalize(main) switch
    {
        DiskMains.Free => UiText.DiskFree,
        _ => UiText.DiskUsedPercent
    };

    public static string DockValue(IReadOnlyList<DiskVolumeSnapshot> volumes, DiskWidgetSettings settings)
    {
        if (volumes.Count == 0)
        {
            return "—";
        }

        var total = volumes.Aggregate(0UL, (sum, volume) => sum + volume.TotalBytes);
        var free = volumes.Aggregate(0UL, (sum, volume) => sum + volume.FreeBytes);
        if (total == 0)
        {
            return "—";
        }

        return DiskMains.Normalize(settings.Main) == DiskMains.Free
            ? Size(free)
            : SysActivityFormat.Percent((total - free) * 100.0 / total, true);
    }

    public static string Size(ulong bytes)
    {
        var value = bytes / Gb;
        return Math.Abs(value - Math.Round(value)) < 0.05
            ? $"{Math.Round(value):0} GB"
            : $"{value:0.0} GB";
    }

    public static string Capacity(DiskVolumeSnapshot volume)
    {
        if (volume.TotalBytes == 0)
        {
            return "—";
        }

        return $"{Size(volume.UsedBytes)} / {Size(volume.TotalBytes)}";
    }

    public static string VolumeTitle(DiskVolumeSnapshot volume)
    {
        var letter = volume.Name.TrimEnd('\\', '/');
        return string.IsNullOrWhiteSpace(volume.Label)
            ? letter
            : $"{letter} · {volume.Label}";
    }
}
