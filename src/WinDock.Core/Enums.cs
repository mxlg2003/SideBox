namespace WinDock.Core;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum DockBackdrop
{
    Mica,
    Frosted,
    Glass
}

public enum DockMode
{
    Overlay,
    AppBar
}

public enum DockEdge
{
    Top,
    Left,
    Right,
    Bottom
}

public enum DockItemType
{
    App,
    File,
    Folder,
    // Retired: still deserializes from old settings.json; load drops the item.
    Url,
    // Retired: still deserializes from old settings.json; load drops the item.
    Separator,
    WidgetChip
}

public enum PinPickKind
{
    File,
    Folder,
    InstalledApp
}

public enum WidgetDensity
{
    Compact,
    Expanded
}

public enum TriggerType
{
    Manual,
    Focus,
    DisplayTopology,
    Schedule
}

public enum ActionKind
{
    Notify,
    Launch
}

public enum ActionHook
{
    Activate,
    Deactivate
}

public enum ManualOverrideUntil
{
    NextFocusOrTopology,
    UntilManual,
    Session
}

public enum ProfileSwitchResult
{
    Succeeded,
    Failed
}

public enum TriggerReason
{
    Manual,
    Focus,
    DisplayTopology,
    Schedule,
    Fallback,
    SafeMode
}

public enum DockZorderState
{
    OverlayTopmost,
    AutoHidden,
    Peeking,
    FullscreenYield,
    AppBarReserved
}
