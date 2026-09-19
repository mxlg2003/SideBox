namespace WinDock.Core;

public readonly struct RectLogical
{
    public RectLogical(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public sealed record MonitorInfo
{
    public required string Id { get; init; }

    public required string DevicePath { get; init; }

    public required bool IsPrimary { get; init; }

    public required RectLogical Bounds { get; init; }

    public required RectLogical WorkArea { get; init; }

    /// <summary>Work area in device pixels. Used for the auto-hide edge hotzone.</summary>
    public RectLogical PixelWorkArea { get; init; }

    public required double DpiScale { get; init; }
}

public sealed record DisplayTopologySnapshot
{
    public required IReadOnlyList<MonitorInfo> Monitors { get; init; }

    public MonitorInfo Primary =>
        Monitors.FirstOrDefault(m => m.IsPrimary)
        ?? Monitors[0];

    public string TopologyKey
    {
        get
        {
            var paths = Monitors
                .Select(m => m.DevicePath)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            return $"{Monitors.Count}|{Primary.DevicePath}|{string.Join(',', paths)}";
        }
    }
}

public sealed record ContextSnapshot
{
    public required DateTimeOffset UtcNow { get; init; }

    public required DateTime LocalNow { get; init; }

    public required bool FocusSupported { get; init; }

    public required bool FocusActive { get; init; }

    public required DisplayTopologySnapshot Topology { get; init; }

    public required bool SafeMode { get; init; }

    /// <summary>When false, the engine stays on general and ignores triggers / manual override.</summary>
    public bool ProfilesEnabled { get; init; } = true;

    /// <summary>When false, skip Focus / topology / schedule matching; keep the user's manual profile.</summary>
    public bool AutoProfileTriggersEnabled { get; init; } = true;

    public string? ManualOverrideProfileId { get; init; }

    public ManualOverrideUntil ManualOverrideUntil { get; init; } = ManualOverrideUntil.NextFocusOrTopology;

    public bool? ManualOverrideFocusActive { get; init; }

    public string? ManualOverrideTopologyKey { get; init; }
}

public sealed record ProfileDiff
{
    public required string ProfileId { get; init; }

    public required string ProfileName { get; init; }

    public required TriggerReason Reason { get; init; }

    public required DockLayout Dock { get; init; }

    public required IReadOnlyList<WidgetPlacement> Widgets { get; init; }

    public required ThemePreference Theme { get; init; }

    public required DockMode DockMode { get; init; }

    public required IReadOnlyList<ProfileAction> ActionsToRun { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }
}

public sealed record ProfileSwitchRecord
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string? FromProfileId { get; init; }

    public required string ToProfileId { get; init; }

    public required TriggerReason Reason { get; init; }

    public required ProfileSwitchResult Result { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record DockHostContext
{
    public bool SafeMode { get; init; }

    public DockMode DockMode { get; init; } = DockMode.Overlay;

    public bool AppBarHealthy { get; init; }

    public bool AutoHide { get; init; }

    public bool IsIdle { get; init; }

    public bool PointerInHotzone { get; init; }

    /// <summary>When false, the edge hotzone does not peek an auto-hidden dock.</summary>
    public bool PeekOnPointer { get; init; } = true;

    public bool PointerInDock { get; init; }

    /// <summary>Settings, a widget flyout, or another blocking UI is open.</summary>
    public bool KeepVisible { get; init; }

    public bool IsPresentationOrFullscreen { get; init; }
}
