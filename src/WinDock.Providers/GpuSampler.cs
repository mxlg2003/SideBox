using WinDock.Core;

namespace WinDock.Providers;

public sealed class GpuSampler : IGpuProvider, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private GpuSampleOptions _options = GpuSampleOptions.Default;
    private GpuEngineQuery? _engines;
    private System.Diagnostics.PerformanceCounter? _vramUsed;
    private System.Diagnostics.PerformanceCounter? _vramLimit;
    private string? _boundAdapter;
    private long _unsupportedUntil;
    private IReadOnlyList<double> _history = [];
    private TimeSpan _period = TimeSpan.FromSeconds(2);
    private int _started;
    private int _busy;
    private int _disposed;

    public GpuSampler()
    {
        Current = new GpuSnapshot { Timestamp = DateTimeOffset.UtcNow };
        _timer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public GpuSnapshot Current { get; private set; }

    public event EventHandler<GpuSnapshot>? Updated;

    public void Start()
    {
        if (Volatile.Read(ref _disposed) == 1 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ => Sample());
        Arm();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        try
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }

        CloseQuery();
    }

    public void SetQuiet(bool quiet)
    {
        lock (_gate)
        {
            _period = quiet ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2);
        }

        Arm();
    }

    public void SetOptions(GpuSampleOptions options)
    {
        lock (_gate)
        {
            var next = options ?? GpuSampleOptions.Default;
            if (!string.Equals(_options.AdapterId, next.AdapterId, StringComparison.OrdinalIgnoreCase))
            {
                _boundAdapter = null;
            }

            _options = next;
        }

        if (Volatile.Read(ref _started) == 1)
        {
            ThreadPool.QueueUserWorkItem(_ => Sample());
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _timer.Dispose();
        CloseQuery();
    }

    private void Arm()
    {
        if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        TimeSpan period;
        lock (_gate)
        {
            period = _period;
        }

        try
        {
            _timer.Change(period, period);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Sample()
    {
        if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _busy, 1) != 0)
        {
            return;
        }

        try
        {
            Current = Capture();
            Updated?.Invoke(this, Current);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private GpuSnapshot Capture()
    {
        var adapters = DxgiAdapters.TryRead();
        GpuSampleOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var selected = Pick(adapters, options.AdapterId);
        double? percent = null;
        double? engine3d = null;
        double? engineCopy = null;
        double? engineVideo = null;
        double? engineCompute = null;
        var supported = false;
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) == 0 && EnsureQuery())
            {
                supported = true;
                if (selected is not null
                    && _engines is not null
                    && _engines.TryCollect(selected.Id, options.Engines, out var reading))
                {
                    percent = reading.Percent;
                    engine3d = reading.ThreeD;
                    engineCopy = reading.Copy;
                    engineVideo = reading.Video;
                    engineCompute = reading.Compute;
                }
            }
        }

        if (!supported)
        {
            return new GpuSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Supported = false,
                Adapters = adapters
            };
        }

        ulong vramUsed = 0, vramTotal = 0;
        var vram = false;
        if (options.Vram && selected is not null)
        {
            vram = TryReadVram(selected.Id, out vramUsed, out vramTotal);
        }
        else
        {
            DisposeVram();
        }

        if (percent is double gpu)
        {
            _history = NetworkHistory.Push(_history, gpu);
        }

        return new GpuSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Supported = true,
            AdapterId = selected?.Id,
            AdapterName = selected?.Name,
            Percent = percent ?? 0,
            PercentAvailable = percent is not null,
            VramUsedBytes = vramUsed,
            VramTotalBytes = vramTotal,
            VramAvailable = vram,
            Engine3D = engine3d,
            EngineCopy = engineCopy,
            EngineVideo = engineVideo,
            EngineCompute = engineCompute,
            History = _history,
            Adapters = adapters
        };
    }

    private bool EnsureQuery()
    {
        if (_engines is { IsOpen: true })
        {
            return true;
        }

        if (Environment.TickCount64 < _unsupportedUntil)
        {
            return false;
        }

        _engines?.Dispose();
        _engines = new GpuEngineQuery();
        if (_engines.TryOpen())
        {
            return true;
        }

        _engines.Dispose();
        _engines = null;
        _unsupportedUntil = Environment.TickCount64 + 60_000;
        return false;
    }

    private bool TryReadVram(string luid, out ulong used, out ulong total)
    {
        used = 0;
        total = 0;
        lock (_gate)
        {
            if (!string.Equals(_boundAdapter, luid, StringComparison.OrdinalIgnoreCase) || _vramUsed is null)
            {
                BindVram(luid);
            }
        }

        try
        {
            if (_vramUsed is null || _vramLimit is null)
            {
                return false;
            }

            used = (ulong)Math.Max(0, _vramUsed.NextValue());
            total = (ulong)Math.Max(0, _vramLimit.NextValue());
            return total > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void BindVram(string luid)
    {
        DisposeVramLocked();
        _boundAdapter = luid;
        try
        {
            var category = new System.Diagnostics.PerformanceCounterCategory("GPU Adapter Memory");
            var instance = category.GetInstanceNames()
                .FirstOrDefault(name => name.IndexOf(luid, StringComparison.OrdinalIgnoreCase) >= 0);
            if (instance is null)
            {
                return;
            }

            _vramUsed = TryCreate("GPU Adapter Memory", "Dedicated Usage", instance);
            _vramLimit = TryCreate("GPU Adapter Memory", "Dedicated Limit", instance);
            if (_vramUsed is null || _vramLimit is null)
            {
                DisposeVramLocked();
                _boundAdapter = luid;
            }
        }
        catch (Exception)
        {
            DisposeVramLocked();
            _boundAdapter = luid;
        }
    }

    private static System.Diagnostics.PerformanceCounter? TryCreate(string category, string counter, string instance)
    {
        System.Diagnostics.PerformanceCounter? created = null;
        try
        {
            created = new System.Diagnostics.PerformanceCounter(category, counter, instance, true);
            _ = created.NextValue();
            return created;
        }
        catch (Exception)
        {
            created?.Dispose();
            return null;
        }
    }

    private static GpuAdapterInfo? Pick(IReadOnlyList<GpuAdapterInfo> adapters, string? requested)
    {
        if (adapters.Count == 0)
        {
            return requested is { Length: > 0 }
                ? new GpuAdapterInfo { Id = requested, Name = requested }
                : null;
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = adapters.FirstOrDefault(adapter =>
                string.Equals(adapter.Id, requested, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return adapters[0];
    }

    private void DisposeVram()
    {
        lock (_gate)
        {
            DisposeVramLocked();
            _boundAdapter = null;
        }
    }

    private void DisposeVramLocked()
    {
        _vramUsed?.Dispose();
        _vramLimit?.Dispose();
        _vramUsed = null;
        _vramLimit = null;
    }

    private void CloseQuery()
    {
        lock (_gate)
        {
            _engines?.Dispose();
            _engines = null;
            _unsupportedUntil = 0;
            DisposeVramLocked();
            _boundAdapter = null;
        }
    }
}
