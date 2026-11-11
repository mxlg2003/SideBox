namespace WinDock.Core;

public sealed record Profile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<ProfileTrigger> Triggers { get; init; } = [];

    public DockLayout Dock { get; init; } = new();

    public IReadOnlyList<WidgetPlacement> Widgets { get; init; } = [];

    /// <summary>Notes chips taken off the island; same ids still own their files.</summary>
    public IReadOnlyList<DockItem> HiddenWidgets { get; init; } = [];

    public IReadOnlyList<ProfileAction> Actions { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed record ProfileTrigger
{
    public required TriggerType Type { get; init; }

    public bool? Active { get; init; }

    public int? MonitorCount { get; init; }

    public string? PrimaryDevicePath { get; init; }

    public IReadOnlyList<string>? RequireDevicePaths { get; init; }

    public IReadOnlyList<DayOfWeek>? Days { get; init; }

    public string? Start { get; init; }

    public string? End { get; init; }
}

public sealed record DockLayout
{
    public DockEdge Edge { get; init; } = DockEdge.Top;

    public string Monitor { get; init; } = "primary";

    public bool AutoHide { get; init; }

    /// <summary>When auto-hidden, moving the pointer into the edge hotzone peeks the island.</summary>
    public bool PeekOnPointer { get; init; } = true;

    /// <summary>When auto-hidden, the global <see cref="HotkeySettings.RevealDock"/> chord wakes the island.</summary>
    public bool RevealHotkey { get; init; }

    /// <summary>Show one stack icon for app/file pins; click expands them beside the island.</summary>
    public bool CollapseApps { get; init; }

    /// <summary>Show one stack icon for folder pins; click expands them beside the island.</summary>
    public bool CollapseFolders { get; init; }

    public IReadOnlyList<DockItem> Items { get; init; } = [];

    /// <summary>Long-axis length of the island in logical pixels. Null sizes to content.</summary>
    public double? IslandLength { get; init; }
}

public sealed record DockItem
{
    public string Id { get; init; } = "";

    public required DockItemType Type { get; init; }

    public string? Path { get; init; }

    public string? Url { get; init; }

    public string? WidgetId { get; init; }

    public string? Title { get; init; }

    public string? IconPath { get; init; }

    /// <summary>Original Start Menu / Explorer <c>.lnk</c>. Launch uses this so arguments stay intact.</summary>
    public string? ShortcutPath { get; init; }

    public WidgetDensity Density { get; init; } = WidgetDensity.Expanded;

    /// <summary>Sticky-note paper color id. Null uses the default yellow.</summary>
    public string? PaperColor { get; init; }

    /// <summary>Focus timer options. Null uses 25/5/15 minutes and orange.</summary>
    public FocusTimerSettings? FocusTimer { get; init; }

    /// <summary>Countdown name, last duration, and saved presets. Null uses 5 minutes and no presets.</summary>
    public CountdownSettings? Countdown { get; init; }

    /// <summary>Alarm clock time, label, and armed instant. Null is 08:00 idle.</summary>
    public AlarmSettings? Alarm { get; init; }

    /// <summary>Power module layout and which devices appear on the dock. Null uses status rings and all devices.</summary>
    public PowerWidgetSettings? Power { get; init; }

    /// <summary>Time progress period and layout. Null uses year + bars.</summary>
    public TimeProgressSettings? TimeProgress { get; init; }

    /// <summary>System activity dock layout. Null uses numbers.</summary>
    public SysActivitySettings? SysActivity { get; init; }

    /// <summary>Network show filter and whether the sparkline appears on the dock or flyout.</summary>
    public NetworkWidgetSettings? Network { get; init; }

    /// <summary>Optional second clock timezone id (Windows or IANA). Null shows local time only.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Weather widget city, units, and refresh interval.</summary>
    public WeatherSettings? Weather { get; init; }

    /// <summary>Schedule widget events and countdown items.</summary>
    public ScheduleSettings? Schedule { get; init; }

    /// <summary>Disk volumes, island metric, and optional activity sparkline.</summary>
    public DiskWidgetSettings? Disk { get; init; }

    /// <summary>GPU adapter, VRAM, engines, and island sparkline.</summary>
    public GpuWidgetSettings? Gpu { get; init; }

    /// <summary>High-usage process ranking.</summary>
    public ProcessWidgetSettings? Processes { get; init; }

    /// <summary>The one symbol this chip follows, plus the chart range.</summary>
    public StockWidgetSettings? Stocks { get; init; }

    /// <summary>White noise volume, active preset, sleep timer, and track mixers.</summary>
    public WhiteNoiseSettings? WhiteNoise { get; init; }

    /// <summary>Bing wallpaper settings: auto-switch daily and last applied key.</summary>
    public BingWallpaperSettings? BingWallpaper { get; init; }

    /// <summary>Wallpaper hub settings: source, rotation, auto-apply desktop, local folder, unsplash query.</summary>
    public WallpaperSettings? Wallpaper { get; init; }
}

public sealed record WidgetPlacement
{
    public required string Id { get; init; }

    public string Monitor { get; init; } = "primary";

    public double X { get; init; }

    public double Y { get; init; }

    public bool Visible { get; init; } = true;
}

public sealed record ProfileAction
{
    public required ActionHook On { get; init; }

    public required ActionKind Kind { get; init; }

    public string? Message { get; init; }

    public string? Target { get; init; }
}
