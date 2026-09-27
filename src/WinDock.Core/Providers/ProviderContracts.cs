using WinDock.Core.Permissions;

namespace WinDock.Core;

public interface IProviderHub : IDisposable
{
    IPermissionGate Permissions { get; }

    IFocusProvider Focus { get; }

    IMediaProvider Media { get; }

    IPerfSampler Perf { get; }

    INetworkProvider Network { get; }

    IPowerProvider Power { get; }

    IClockProvider Clock { get; }

    INotesStore Notes { get; }

    IBingWallpaperProvider Bing { get; }

    IWeatherProvider Weather { get; }

    IShellLaunch Shell { get; }
}

public interface IFocusProvider
{
    bool IsSupported { get; }

    bool IsFocusActive { get; }

    event EventHandler? IsFocusActiveChanged;
}

public interface IMediaProvider
{
    MediaSessionSnapshot? Current { get; }

    event EventHandler? Changed;

    void Start();

    void Stop();

    bool CaptureMetadata { get; set; }

    Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default);
}

public sealed class MediaSessionSnapshot
{
    public required string SourceAppId { get; init; }

    public required bool IsPlaying { get; init; }

    public bool CanPlayPause { get; init; }

    public bool CanSkipNext { get; init; }

    public bool CanSkipPrevious { get; init; }

    public string? Title { get; init; }

    public string? Artist { get; init; }

    public string? ThumbnailPath { get; init; }

    public TimeSpan Position { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>
    /// True when this snapshot comes from a local player process rather than GSMTC,
    /// so transport commands use APPCOMMAND / media keys and playback state may be unknown.
    /// </summary>
    public bool UsesFallbackTransport { get; init; }

    public MediaSessionSnapshot WithIsPlaying(bool isPlaying) => new()
    {
        SourceAppId = SourceAppId,
        IsPlaying = isPlaying,
        CanPlayPause = CanPlayPause,
        CanSkipNext = CanSkipNext,
        CanSkipPrevious = CanSkipPrevious,
        Title = Title,
        Artist = Artist,
        ThumbnailPath = ThumbnailPath,
        Position = Position,
        Duration = Duration,
        UsesFallbackTransport = UsesFallbackTransport
    };
}

public interface IPerfSampler
{
    PerfSnapshot Current { get; }

    event EventHandler<PerfSnapshot>? Updated;

    void Start();

    void Stop();
}

public sealed class PerfSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public double CpuPercent { get; init; }

    public bool CpuAvailable { get; init; } = true;

    public double MemoryPercent { get; init; }

    public bool MemoryAvailable { get; init; } = true;

    public IReadOnlyList<double> CpuHistory { get; init; } = [];

    public IReadOnlyList<double> MemoryHistory { get; init; } = [];

    public ulong MemoryUsedBytes { get; init; }

    public ulong MemoryTotalBytes { get; init; }
}

public interface INetworkProvider
{
    NetworkSnapshot Current { get; }

    event EventHandler<NetworkSnapshot>? Updated;

    void Start();

    void Stop();

    /// <summary>When false, sample connectivity only — do not enumerate adapters or rates.</summary>
    bool CollectDetails { get; set; }
}

public sealed class NetworkSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public bool IsConnected { get; init; }

    public string? AdapterName { get; init; }

    public double ReceiveBytesPerSecond { get; init; }

    public double SendBytesPerSecond { get; init; }

    public IReadOnlyList<double> ReceiveHistory { get; init; } = [];

    public IReadOnlyList<double> SendHistory { get; init; } = [];
}

public interface IPowerProvider
{
    PowerSnapshot Current { get; }

    event EventHandler<PowerSnapshot>? Updated;

    void Start();

    void Stop();

    void Refresh();
}

public sealed record PowerSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public bool HasBattery { get; init; }

    public bool OnAcPower { get; init; }

    public int? PercentRemaining { get; init; }

    public IReadOnlyList<PowerDeviceSnapshot> Devices { get; init; } = [];
}

public interface IClockProvider
{
    DateTimeOffset UtcNow { get; }

    DateTime LocalNow { get; }
}

public interface INotesStore
{
    string Load(string key);

    void Save(string key, string text);

    void Delete(string key);

    /// <summary>Drop files whose keys are not in <paramref name="liveKeys"/>.</summary>
    void Sweep(IEnumerable<string> liveKeys);

    /// <summary>Copy the legacy single-file notes into <paramref name="key"/> once, if that key has no file yet.</summary>
    void TryAdoptLegacy(string key);
}

public interface IBingWallpaperProvider
{
    BingWallpaperSnapshot Current { get; }

    event EventHandler? Changed;

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task SelectRelativeAsync(int delta, CancellationToken cancellationToken = default);

    Task<bool> TryApplyCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> TryApplyTodayAsync(CancellationToken cancellationToken = default);

    bool CanRestorePreviousWallpaper { get; }

    Task<bool> TryRestorePreviousAsync(CancellationToken cancellationToken = default);
}

public sealed class BingWallpaperImage
{
    public required string StartDate { get; init; }

    public required string Title { get; init; }

    public required string Copyright { get; init; }

    public required string ImageUrl { get; init; }

    public required string WallpaperUrl { get; init; }

    public required string Key { get; init; }
}

public sealed record BingWallpaperSnapshot
{
    public IReadOnlyList<BingWallpaperImage> Images { get; init; } = [];

    public int SelectedIndex { get; init; }

    public string? LocalPath { get; init; }

    public string? AppliedKey { get; init; }

    public bool IsBusy { get; init; }

    public string? Error { get; init; }

    public BingWallpaperImage? Selected =>
        SelectedIndex >= 0 && SelectedIndex < Images.Count ? Images[SelectedIndex] : null;
}

public sealed record DockWindowEntry(nint Hwnd, string Title, bool IsActive);

public interface IShellLaunch
{
    DockItem CreatePin(string pathOrUrl);

    IReadOnlyList<DockItem> CreatePins(IEnumerable<string> paths);

    bool TryLaunch(DockItem item);

    bool TryActivate(DockItem item);

    IReadOnlyList<DockWindowEntry> GetWindows(DockItem item);

    bool ActivateWindow(nint hwnd);

    bool CloseWindow(nint hwnd);

    string? GetIconFile(DockItem item);

    string? GetStockIconFile(DockPinStackKind kind);

    IReadOnlySet<string> GetRunningImagePaths(IReadOnlySet<string> processNames);

    IReadOnlyList<StartMenuApp> ListStartMenuApps();
}

public interface IWeatherProvider
{
    WeatherSnapshot? Current { get; }

    event EventHandler? Changed;

    bool AllowAutoLocate { get; set; }

    void Start();

    void Stop();

    Task<WeatherSnapshot?> RefreshAsync(WeatherSettings settings, bool force = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeatherCityItem>> SearchCitiesAsync(string query, CancellationToken cancellationToken = default);
}
