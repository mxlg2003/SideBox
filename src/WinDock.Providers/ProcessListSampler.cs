using WinDock.Core;

namespace WinDock.Providers;

public sealed class ProcessListSampler : IProcessListProvider, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Dictionary<int, long> _cpu = [];
    private readonly string _iconCache;
    private readonly object _pace = new();
    private long _lastTicks;
    private bool _quiet;
    private bool _live;
    private int _started;
    private int _busy;
    private int _disposed;

    public ProcessListSampler(string? iconCacheDirectory = null)
    {
        _iconCache = iconCacheDirectory ?? ProductInfo.LocalData("process-icons");
        Current = new ProcessListSnapshot { Timestamp = DateTimeOffset.UtcNow };
        _timer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public ProcessListSnapshot Current { get; private set; }

    public event EventHandler<ProcessListSnapshot>? Updated;

    public void Start()
    {
        if (Volatile.Read(ref _disposed) == 1 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _lastTicks = 0;
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
    }

    public void SetLive(bool live)
    {
        lock (_pace)
        {
            _live = live;
        }

        Arm();
    }

    public void SetQuiet(bool quiet)
    {
        lock (_pace)
        {
            _quiet = quiet;
        }

        Arm();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _timer.Dispose();
    }

    private void Arm()
    {
        if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        TimeSpan period;
        lock (_pace)
        {
            period = _quiet
                ? TimeSpan.FromSeconds(10)
                : _live
                    ? TimeSpan.FromSeconds(2)
                    : TimeSpan.FromSeconds(5);
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

    private ProcessListSnapshot Capture()
    {
        var now = Environment.TickCount64;
        var elapsed = _lastTicks == 0 ? 0 : Math.Max(0.2, (now - _lastTicks) / 1000.0);
        _lastTicks = now;
        var cores = Math.Max(1, Environment.ProcessorCount);
        var processes = SystemProcessTable.ReadRecent();
        var nextCpu = new Dictionary<int, long>(processes.Length);
        var rows = new List<ProcessRow>(processes.Length);
        foreach (var process in processes)
        {
            nextCpu[process.Pid] = process.CpuTicks;
            var percent = 0.0;
            if (elapsed > 0 && _cpu.TryGetValue(process.Pid, out var previous) && process.CpuTicks >= previous)
            {
                percent = Math.Clamp(
                    (process.CpuTicks - previous) / 10_000_000.0 / elapsed / cores * 100.0,
                    0,
                    100.0 * cores);
            }

            rows.Add(new ProcessRow
            {
                Pid = process.Pid,
                Name = process.Name,
                CpuPercent = percent,
                WorkingSetBytes = process.WorkingSet
            });
        }

        _cpu.Clear();
        foreach (var (pid, cpu) in nextCpu)
        {
            _cpu[pid] = cpu;
        }

        var top = ProcessFormat.Rank(rows, ProcessSorts.Cpu)
            .Concat(ProcessFormat.Rank(rows, ProcessSorts.Memory))
            .DistinctBy(row => row.Pid)
            .ToArray();
        var paths = ResolvePaths(top);
        var filled = top.Select(row =>
        {
            if (!paths.TryGetValue(row.Pid, out var path) || string.IsNullOrWhiteSpace(path))
            {
                return row;
            }

            return row with
            {
                ImagePath = path,
                IconPath = ProcessIconCache.TrySave(path, _iconCache)
            };
        }).ToArray();

        return new ProcessListSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Allowed = true,
            Rows = filled
        };
    }

    private static Dictionary<int, string> ResolvePaths(IReadOnlyList<ProcessRow> rows)
    {
        var map = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            var path = NativeMethods.QueryProcessImage(row.Pid);
            if (!string.IsNullOrWhiteSpace(path))
            {
                map[row.Pid] = path;
            }
        }

        return map;
    }
}
