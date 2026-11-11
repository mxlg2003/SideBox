namespace WinDock.Core;

/// <summary>Temporary product switches. Flip these rather than deleting the underlying profile engine.</summary>
public static class ProductFeatures
{
    /// <summary>
    /// When false, the dock stays on the general layout: no settings page, tray/hotkey switching,
    /// or Focus / topology / schedule automatic switches.
    /// </summary>
    public static readonly bool ProfilesEnabled = false;

    /// <summary>
    /// When false, Focus / display topology / schedule never auto-switch the dock.
    /// Manual tray, hotkey, and settings switching still work when <see cref="ProfilesEnabled"/> is true.
    /// </summary>
    public static readonly bool AutoProfileTriggersEnabled = false;

    /// <summary>
    /// When false, Now Playing stays out of the module catalog, the dock, and Privacy.
    /// Chips already saved on a profile stay in settings and return when this is turned back on.
    /// </summary>
    public static readonly bool NowPlayingEnabled = false;

    /// <summary>
    /// When false, the process list stays out of the module catalog and the dock.
    /// It uses an undocumented process table and is an experiment switch, default off.
    /// Chips already saved on a profile stay in settings and return when this is turned back on.
    /// </summary>
    public static readonly bool ProcessesEnabled = false;
}
