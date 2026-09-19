namespace WinDock.Core;

public interface ISettingsStore
{
    AppSettings Load();

    string? LastLoadMessage { get; }

    /// <summary>True when the on-disk file is a newer schema; Save must not overwrite it.</summary>
    bool IsImmutable { get; }

    void Save(AppSettings settings);
}

[Obsolete("Independent widget windows are not shipped; modules stay on the island.")]
public interface IWindowFactory
{
    IDockSurface CreateDock();

    IWidgetSurface? CreateWidget(string widgetId);
}

public interface IDockSurface : IDisposable
{
    void ApplyLayout(RectLogical bounds, DockEdge edge);

    void ApplyZorder(DockZorderState state);

    void ApplyItems(IReadOnlyList<DockItem> items);

    void ApplyTheme(bool isLight, DockBackdrop backdrop, int islandTransparency);

    void ApplyProfileMenu(IReadOnlyList<Profile> profiles, string activeProfileId);

    void Show();

    void Hide();

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    event EventHandler<string>? ProfileSwitchRequested;
}

public sealed class WidgetDensityRequest
{
    public required string ItemId { get; init; }

    public required WidgetDensity Density { get; init; }
}

public interface IWidgetSurface : IDisposable
{
    string WidgetId { get; }

    bool IsVisible { get; }

    void ApplyPlacement(MonitorInfo monitor, WidgetPlacement placement);

    void ApplyTheme(bool isLight);

    void Show();

    void Hide();
}

public interface IDockLayoutService
{
    RectLogical GetEdgeBounds(MonitorInfo monitor, DockEdge edge, double thicknessLogical);

    RectLogical GetIslandBounds(
        MonitorInfo monitor,
        DockEdge edge,
        double thicknessLogical,
        double contentWidth,
        double contentHeight,
        double? preferredLength = null);

    MonitorInfo ResolveMonitor(DisplayTopologySnapshot topology, string? monitorRef);
}

public interface IAppBarService
{
    bool IsRegistered { get; }

    AppBarRegistrationResult TryRegister(AppBarRequest request);

    void Remove();
}

public sealed class AppBarRequest
{
    public required nint WindowHandle { get; init; }

    public required DockEdge Edge { get; init; }

    public required RectLogical Proposed { get; init; }
}

public sealed class AppBarRegistrationResult
{
    public required bool Succeeded { get; init; }

    public string? FailureReason { get; init; }

    public RectLogical? Approved { get; init; }

    public static AppBarRegistrationResult Fail(string reason) => new()
    {
        Succeeded = false,
        FailureReason = reason
    };
}

public interface IDisplayService : IDisposable
{
    DisplayTopologySnapshot Current { get; }

    event EventHandler<DisplayTopologySnapshot>? Changed;

    void RequestRefresh();
}

public interface IStartupOptions
{
    bool SafeMode { get; }
}
