using WinDock.Core;

namespace WinDock.Providers;

public sealed class DiskSampler : IDiskProvider, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private DiskSampleOptions _options = DiskSampleOptions.Default;
    private System.Diagnostics.PerformanceCounter? _activity;
    private bool _activityReady;
    private IReadOnlyList<double> _history = [];
    private TimeSpan _period = TimeSpan.FromSeconds(2);
    private int _started;
    private int _busy;
    private int _disposed;

    public DiskSampler()
    {
        Current = new DiskSnapshot { Timestamp = DateTimeOffset.UtcNow };
        _timer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public DiskSnapshot Current { get; private set; }

    public event EventHandler<DiskSnapshot>? Updated;

    public void Start()
    {
        if (Volatile.Read(ref _disposed) == 1 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            EnsureActivity();
            Sample();
        });
        Arm();
    }

    public void SetQuiet(bool quiet)
    {
        _period = quiet ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2);
        Arm();
    }

    private void Arm()
    {
        if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            _timer.Change(_period, _period);
        }
        catch (ObjectDisposedException)
        {
        }
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
    }

    public void SetOptions(DiskSampleOptions options)
    {
        _options = options ?? DiskSampleOptions.Default;
        if (Volatile.Read(ref _started) == 1)
        {
            EnsureActivity();
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
        _activity?.Dispose();
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
            var volumes = ReadVolumes();
            var activity = TryReadActivity();
            if (activity is double value)
            {
                _history = NetworkHistory.Push(_history, value);
            }
            else if (!_options.Activity)
            {
                _history = [];
            }

            Current = new DiskSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Volumes = volumes,
                ActivityHistory = _history,
                ActivityAvailable = activity is not null
            };
            Updated?.Invoke(this, Current);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    internal static IReadOnlyList<DiskVolumeSnapshot> ReadVolumes()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(IsListed)
                .Select(TryVolume)
                .OfType<DiskVolumeSnapshot>()
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool IsListed(DriveInfo drive)
    {
        // Removable volumes call IsReady on the sample timer. An empty card reader can block that thread for seconds.
        return drive.DriveType == DriveType.Fixed;
    }

    private static DiskVolumeSnapshot? TryVolume(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady)
            {
                return null;
            }

            var id = drive.Name;
            var label = drive.VolumeLabel ?? string.Empty;
            return new DiskVolumeSnapshot
            {
                Id = id,
                Name = id,
                Label = label,
                TotalBytes = (ulong)Math.Max(0, drive.TotalSize),
                FreeBytes = (ulong)Math.Max(0, drive.TotalFreeSpace)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private double? TryReadActivity()
    {
        if (!_options.Activity)
        {
            DisposeActivity();
            return null;
        }

        EnsureActivity();
        if (!_activityReady || _activity is null)
        {
            return null;
        }

        try
        {
            return Math.Max(0, _activity.NextValue());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void EnsureActivity()
    {
        if (!_options.Activity || _activityReady)
        {
            return;
        }

        try
        {
            var counter = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", "_Total", true);
            try
            {
                _ = counter.NextValue();
            }
            catch (Exception)
            {
                counter.Dispose();
                throw;
            }

            _activity = counter;
            _activityReady = true;
        }
        catch (Exception)
        {
            DisposeActivity();
        }
    }

    private void DisposeActivity()
    {
        _activity?.Dispose();
        _activity = null;
        _activityReady = false;
    }
}
