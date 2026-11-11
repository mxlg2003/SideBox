namespace WinDock.Core;

public sealed record GpuAdapterInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

public sealed record GpuSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public bool Supported { get; init; }

    public string? AdapterId { get; init; }

    public string? AdapterName { get; init; }

    public double Percent { get; init; }

    public bool PercentAvailable { get; init; }

    public ulong VramUsedBytes { get; init; }

    public ulong VramTotalBytes { get; init; }

    public bool VramAvailable { get; init; }

    public double? Engine3D { get; init; }

    public double? EngineCopy { get; init; }

    public double? EngineVideo { get; init; }

    public double? EngineCompute { get; init; }

    public IReadOnlyList<double> History { get; init; } = [];

    public IReadOnlyList<GpuAdapterInfo> Adapters { get; init; } = [];
}

public sealed record GpuSampleOptions
{
    public bool Vram { get; init; }

    public bool Engines { get; init; }

    public string? AdapterId { get; init; }

    public static GpuSampleOptions Default { get; } = new();
}

public sealed record GpuWidgetSettings
{
    public string? AdapterId { get; init; }

    public bool ShowVram { get; init; }

    public bool ShowEngines { get; init; }

    public bool ChartInDock { get; init; }

    public static GpuWidgetSettings Default { get; } = new();

    public static GpuWidgetSettings Normalize(GpuWidgetSettings? settings)
    {
        var value = settings ?? Default;
        var adapter = string.IsNullOrWhiteSpace(value.AdapterId) ? null : value.AdapterId.Trim();
        return value with { AdapterId = adapter };
    }
}

public static class GpuFormat
{
    public static string Percent(double percent, bool available) =>
        SysActivityFormat.Percent(percent, available);

    public static string Vram(ulong used, ulong total, bool available)
    {
        if (!available || total == 0)
        {
            return "—";
        }

        return DiskFormat.Capacity(new DiskVolumeSnapshot
        {
            Id = "vram",
            Name = "vram",
            Label = "",
            TotalBytes = total,
            FreeBytes = total > used ? total - used : 0
        });
    }

    public static string Engine(double? percent) =>
        percent is double value ? SysActivityFormat.Percent(value, true) : "—";
}
