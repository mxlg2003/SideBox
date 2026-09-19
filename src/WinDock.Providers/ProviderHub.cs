using WinDock.Core;
using WinDock.Core.Permissions;

namespace WinDock.Providers;

public sealed class ProviderHub : IProviderHub
{
    private readonly GsmtcMediaProvider _media;
    private readonly NetworkInterfaceSampler _network;
    private readonly BingWallpaperProvider _bing;

    public ProviderHub(IPermissionGate permissions, INotesStore notes, string? bingCacheDirectory = null, string? iconCacheDirectory = null)
    {
        Permissions = permissions;
        Focus = new WinRtFocusProvider();
        _media = new GsmtcMediaProvider();
        Media = new GatedMediaProvider(permissions, _media);
        Perf = new PerformanceCounterSampler();
        _network = new NetworkInterfaceSampler();
        Network = new GatedNetworkProvider(permissions, _network);
        Power = new Win32PowerProvider();
        Clock = new LocalClockProvider();
        Notes = notes;
        Bing = _bing = new BingWallpaperProvider(bingCacheDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDock", "bing-wallpaper"));
        Shell = new Win32ShellLaunch(iconCacheDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDock", "icon-cache"));
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

    public IShellLaunch Shell { get; }

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
    }
}

public sealed class WinRtFocusProvider : IFocusProvider, IDisposable
{
    private readonly Windows.UI.Shell.FocusSessionManager? _manager;

    public WinRtFocusProvider()
    {
        try
        {
            IsSupported = Windows.UI.Shell.FocusSessionManager.IsSupported;
            if (!IsSupported)
            {
                return;
            }

            _manager = Windows.UI.Shell.FocusSessionManager.GetDefault();
            IsFocusActive = _manager.IsFocusActive;
            _manager.IsFocusActiveChanged += OnIsFocusActiveChanged;
        }
        catch (Exception)
        {
            IsSupported = false;
            IsFocusActive = false;
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
}
