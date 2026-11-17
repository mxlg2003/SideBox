namespace WinDock.Core;

public sealed record DockQuickSettings
{
    public DockEdge Edge { get; init; } = DockEdge.Top;

    public bool AutoHide { get; init; }

    public bool AvoidFullscreen { get; init; } = true;

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public DockBackdrop Backdrop { get; init; } = DockBackdrop.Mica;

    public bool RunAtStartup { get; init; }
}
