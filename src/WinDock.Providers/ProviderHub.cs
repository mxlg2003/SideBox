using WinDock.Core;
using WinDock.Core.Permissions;

namespace WinDock.Providers;

public sealed class ProviderHub : IProviderHub
{
    private readonly GsmtcMediaProvider _media;
    private readonly NetworkInterfaceSampler _network;
    private readonly BingWallpaperProvider _bing;
    private readonly WallpaperService _wallpaper;
    private readonly WeatherProvider _weather;
    private readonly PerformanceCounterSampler _perf;
    private readonly DiskSampler _disk;
    private readonly GpuSampler _gpu;
    private readonly ProcessListSampler _processes;
    private readonly StockProvider _stocks;

    public ProviderHub(IPermissionGate permissions, INotesStore notes, string? bingCacheDirectory = null, string? iconCacheDirectory = null, string? wallpaperCacheDirectory = null)
    {
        Permissions = permissions;
        Focus = new WinRtFocusProvider();
        _media = new GsmtcMediaProvider();
        Media = new GatedMediaProvider(permissions, _media);
        Perf = _perf = new PerformanceCounterSampler();
        _network = new NetworkInterfaceSampler();
        Network = new GatedNetworkProvider(permissions, _network);
        Power = new Win32PowerProvider();
        Clock = new LocalClockProvider();
        Notes = notes;
        Bing = _bing = new BingWallpaperProvider(bingCacheDirectory
            ?? ProductInfo.LocalData("bing-wallpaper"));
        Wallpaper = _wallpaper = new WallpaperService(wallpaperCacheDirectory
            ?? ProductInfo.LocalData("wallpapers"), _bing);
        Weather = _weather = new WeatherProvider();
        _disk = new DiskSampler();
        Disk = _disk;
        _gpu = new GpuSampler();
        Gpu = _gpu;
        _processes = new ProcessListSampler(iconCacheDirectory
            ?? ProductInfo.LocalData("icon-cache"));
        Processes = new GatedProcessListProvider(permissions, _processes);
        Stocks = _stocks = new StockProvider();
        Shell = new Win32ShellLaunch(iconCacheDirectory
            ?? ProductInfo.LocalData("icon-cache"));
    }

    public IPermissionGate Permissions { get; }

    public IFocusProvider Focus { get; }

    public IMediaProvider Media { get; }

    public IPerfSampler Perf { get; }

    public INetworkProvider Network { get; }

    public IPowerProvider Power { get; }

    public IClockProvider Clock { get; }

    public INotesStore Notes { get; }

    public IBingWallpaperProvider Bing { get; }

    public IWallpaperProvider Wallpaper { get; }

    public IWeatherProvider Weather { get; }

    public IDiskProvider Disk { get; }

    public IGpuProvider Gpu { get; }

    public IProcessListProvider Processes { get; }

    public IStockProvider Stocks { get; }

    public IShellLaunch Shell { get; }

    public void SetQuietSampling(bool quiet)
    {
        _perf.SetQuiet(quiet);
        _network.SetQuiet(quiet);
        _disk.SetQuiet(quiet);
        _gpu.SetQuiet(quiet);
        _processes.SetQuiet(quiet);
        _stocks.SetQuiet(quiet);
    }

    public void Dispose()
    {
        if (Focus is IDisposable focus)
        {
            focus.Dispose();
        }

        if (Perf is IDisposable perf)
        {
            perf.Dispose();
        }

        if (Power is IDisposable power)
        {
            power.Dispose();
        }

        _media.Dispose();
        _network.Dispose();
        _bing.Dispose();
        _wallpaper.Dispose();
        _weather.Dispose();
        _disk.Dispose();
        _gpu.Dispose();
        _processes.Dispose();
        _stocks.Dispose();
    }
}

public sealed class WinRtFocusProvider : IFocusProvider, IDisposable
{
    private readonly Windows.UI.Shell.FocusSessionManager? _manager;

    public WinRtFocusProvider()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            IsSupported = false;
            IsFocusActive = false;
            return;
        }

        try
        {
            InitFocusSession(out _manager, out var isSupported, out var isFocusActive);
            IsSupported = isSupported;
            IsFocusActive = isFocusActive;
        }
        catch (Exception)
        {
            _manager = null;
            IsSupported = false;
            IsFocusActive = false;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void InitFocusSession(
        out Windows.UI.Shell.FocusSessionManager? manager,
        out bool isSupported,
        out bool isFocusActive)
    {
        manager = null;
        isSupported = Windows.UI.Shell.FocusSessionManager.IsSupported;
        if (!isSupported)
        {
            isFocusActive = false;
            return;
        }

        manager = Windows.UI.Shell.FocusSessionManager.GetDefault();
        isFocusActive = manager?.IsFocusActive ?? false;
        if (manager is not null)
        {
            manager.IsFocusActiveChanged += OnIsFocusActiveChanged;
        }
    }

    public bool IsSupported { get; }

    public bool IsFocusActive { get; private set; }

    public event EventHandler? IsFocusActiveChanged;

    public void Dispose()
    {
        if (_manager is not null)
        {
            _manager.IsFocusActiveChanged -= OnIsFocusActiveChanged;
        }
    }

    private void OnIsFocusActiveChanged(Windows.UI.Shell.FocusSessionManager sender, object args)
    {
        IsFocusActive = sender.IsFocusActive;
        IsFocusActiveChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class LocalClockProvider : IClockProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTime LocalNow => DateTime.Now;
}

public sealed class MemoryNotesStore : INotesStore
{
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
    private string? _legacy;

    public string Load(string key) =>
        _texts.TryGetValue(key, out var text) ? text : string.Empty;

    public void Save(string key, string text)
    {
        if (NotesText.IsBlank(text))
        {
            _texts.Remove(key);
            return;
        }

        _texts[key] = text;
    }

    public void Delete(string key) => _texts.Remove(key);

    public void Sweep(IEnumerable<string> liveKeys)
    {
        var live = (liveKeys ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _texts.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            _texts.Remove(key);
        }
    }

    public void TryAdoptLegacy(string key)
    {
        if (_legacy is null)
        {
            return;
        }

        if (!_texts.ContainsKey(key))
        {
            _texts[key] = _legacy;
        }

        _legacy = null;
    }

    public void SeedLegacy(string text) => _legacy = text;
}

public sealed class StubMediaProvider : IMediaProvider
{
    public MediaSessionSnapshot? Current => null;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public bool CaptureMetadata { get; set; }

    public Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public sealed class StubPerfSampler : IPerfSampler
{
    public PerfSnapshot Current { get; } = new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        CpuPercent = 0,
        MemoryPercent = 0
    };

    public event EventHandler<PerfSnapshot>? Updated
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void SetOptions(PerfSampleOptions options)
    {
    }
}

public sealed class StubNetworkProvider : INetworkProvider
{
    public NetworkSnapshot Current { get; } = new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        IsConnected = true
    };

    public event EventHandler<NetworkSnapshot>? Updated
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public bool CollectDetails { get; set; }

    public bool CollectWifi { get; set; }
}

public sealed class StubPowerProvider : IPowerProvider
{
    public PowerSnapshot Current { get; } = new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        HasBattery = false,
        OnAcPower = true
    };

    public event EventHandler<PowerSnapshot>? Updated
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Refresh()
    {
    }

    public void SetOptions(PowerSampleOptions options)
    {
    }
}
