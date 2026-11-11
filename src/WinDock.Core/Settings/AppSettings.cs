namespace WinDock.Core;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = SettingsSchema.CurrentVersion;

    public UiSettings Ui { get; init; } = new();

    public PrivacySettings Privacy { get; init; } = new();

    public LicenseSettings License { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public ManualOverrideSettings ManualOverride { get; init; } = new();

    public string ActiveProfileId { get; init; } = DefaultProfiles.GeneralId;

    public string? LastProfileId { get; init; }

    public string? ManualOverrideProfileId { get; init; }

    public bool? ManualOverrideFocusActive { get; init; }

    public string? ManualOverrideTopologyKey { get; init; }

    public IReadOnlyList<Profile> Profiles { get; init; } = [];

    public IReadOnlyList<ProfileSwitchRecord> SwitchHistory { get; init; } = [];
}

public sealed record UiSettings
{
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public UiLanguage Language { get; init; } = UiLanguage.System;

    public DockBackdrop Backdrop { get; init; } = DockBackdrop.Mica;

    public int IslandTransparency { get; init; } = DockLayoutConstants.DefaultIslandTransparency;

    public DockMode DockMode { get; init; } = DockMode.Overlay;
}

public sealed record LicenseSettings
{
    public string ApiBase { get; init; } = "https://sidebox.microsoftware.app";

    public string DeviceId { get; init; } = "";

    public string? Code { get; init; }

    public string Edition { get; init; } = "free";

    public bool Permanent { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Email { get; init; }

    public int DeviceLimit { get; init; }

    public int DeviceCount { get; init; }
}

public sealed record PrivacySettings
{
    public bool MediaRead { get; init; }

    public bool NetworkDetails { get; init; }

    public bool ProcessNames { get; init; }

    public bool CrashReports { get; init; }

    public bool PreciseLocation { get; init; }

    public bool BingDownload { get; init; }
}

public sealed record HotkeySettings
{
    public string? NextProfile { get; init; }

    public string? PreviousProfile { get; init; }

    public string? UndoProfile { get; init; }

    public string? RevealDock { get; init; } = "Win+Shift+A";
}

public sealed record ManualOverrideSettings
{
    public ManualOverrideUntil Until { get; init; } = ManualOverrideUntil.UntilManual;
}

public static class SettingsSchema
{
    public const int CurrentVersion = 1;
}
