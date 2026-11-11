using System.Runtime.InteropServices;
using System.Text;
using WinDock.Core;

namespace WinDock.Providers;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    internal static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    internal const uint WmAppcommand = 0x0319;
    internal const int AppcommandMediaNextTrack = 11;
    internal const int AppcommandMediaPreviousTrack = 12;
    internal const int AppcommandMediaPlayPause = 14;
    internal const int AppcommandMediaPlay = 46;
    internal const int AppcommandMediaPause = 47;

    internal const uint KeyeventfKeyup = 0x0002;
    internal const byte VkMediaNextTrack = 0xB0;
    internal const byte VkMediaPreviousTrack = 0xB1;
    internal const byte VkMediaPlayPause = 0xB3;

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    internal const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        int dwFlags,
        StringBuilder exeName,
        ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(int access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);
}

public sealed class Win32PowerProvider : IPowerProvider, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private BluetoothPresenceWatcher? _bluetooth;
    private PowerSampleOptions _options = PowerSampleOptions.Default;
    private int _busy;
    private int _disposed;
    private int _started;
    private long _burstUntilTicks;

    public Win32PowerProvider()
    {
        Current = Combine(ReadSystem(PowerSampleOptions.Default), []);
        _timer = new System.Threading.Timer(_ => Pulse(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public PowerSnapshot Current { get; private set; }

    public event EventHandler<PowerSnapshot>? Updated;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        _bluetooth = new BluetoothPresenceWatcher(RequestBurst);
        ThreadPool.QueueUserWorkItem(_ => Pulse());
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        ChangeTimer(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _bluetooth?.Dispose();
        _bluetooth = null;
    }

    public void Refresh()
    {
        Start();
        RequestBurst();
        ThreadPool.QueueUserWorkItem(_ => Pulse());
    }

    public void SetOptions(PowerSampleOptions options)
    {
        _options = options ?? PowerSampleOptions.Default;
        if (Volatile.Read(ref _started) == 1)
        {
            ThreadPool.QueueUserWorkItem(_ => Pulse());
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Stop();
        _timer.Dispose();
    }

    public static PowerSnapshot Read() => Combine(ReadSystem(PowerSampleOptions.Default), []);

    private void RequestBurst()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        Volatile.Write(ref _burstUntilTicks, Environment.TickCount64 + (long)PowerPoll.BurstFor.TotalMilliseconds);
        ChangeTimer(TimeSpan.Zero, PowerPoll.Burst);
    }

    private void Pulse()
    {
        if (Volatile.Read(ref _disposed) == 1 || Volatile.Read(ref _started) == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = Combine(ReadSystem(_options), PowerDeviceEnumerator.TryRead());
            if (PowerPoll.PresenceChanged(Current.Devices, snapshot.Devices))
            {
                Volatile.Write(ref _burstUntilTicks, Environment.TickCount64 + (long)PowerPoll.BurstFor.TotalMilliseconds);
            }

            var changed = !PowerPoll.SameReading(Current, snapshot);
            Current = snapshot;
            if (changed)
            {
                Updated?.Invoke(this, snapshot);
            }
        }
        catch (Exception)
        {
            // Keep the last snapshot.
        }
        finally
        {
            if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
            {
                ChangeTimer(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            else
            {
                var burst = Environment.TickCount64 < Volatile.Read(ref _burstUntilTicks);
                ChangeTimer(burst ? PowerPoll.Burst : PowerPoll.Idle, burst ? PowerPoll.Burst : PowerPoll.Idle);
            }

            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void ChangeTimer(TimeSpan due, TimeSpan period)
    {
        try
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _timer.Change(due, period);
            }
        }
        catch (ObjectDisposedException)
        {
            // Provider is shutting down.
        }
    }

    internal static PowerSnapshot Combine(PowerSnapshot system, IReadOnlyList<PowerDeviceSnapshot> peripherals)
    {
        var devices = new List<PowerDeviceSnapshot>();
        if (system.HasBattery && system.PercentRemaining is >= 0 and <= 100)
        {
            devices.Add(new PowerDeviceSnapshot
            {
                Id = PowerFormat.SystemId,
                Name = MachineName(),
                Kind = PowerDeviceKind.Laptop,
                Percent = system.PercentRemaining,
                Charging = system.OnAcPower
            });
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PowerFormat.SystemId };
        foreach (var device in peripherals)
        {
            if (string.IsNullOrWhiteSpace(device.Id) || !seen.Add(device.Id))
            {
                continue;
            }

            devices.Add(device);
        }

        return system with { Devices = devices };
    }

    private static PowerSnapshot ReadSystem(PowerSampleOptions options)
    {
        try
        {
            if (!NativeMethods.GetSystemPowerStatus(out var status))
            {
                return Empty();
            }

            var noBattery = (status.BatteryFlag & 128) != 0 || status.BatteryLifePercent == 255;
            var remaining = options.Remaining && !noBattery && status.ACLineStatus != 1 && status.BatteryLifeTime >= 0
                ? status.BatteryLifeTime
                : (int?)null;
            var snapshot = new PowerSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                HasBattery = !noBattery,
                OnAcPower = status.ACLineStatus == 1,
                PercentRemaining = noBattery ? null : status.BatteryLifePercent,
                RemainingSeconds = remaining
            };
            return options.Watts || options.Health ? MergeBatteryReport(snapshot, options) : snapshot;
        }
        catch (Exception)
        {
            return Empty();
        }
    }

    private static PowerSnapshot MergeBatteryReport(PowerSnapshot system, PowerSampleOptions options)
    {
        try
        {
            var report = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
            int? watts = null;
            int? health = null;
            if (options.Watts && report.ChargeRateInMilliwatts is int rate)
            {
                watts = rate;
            }

            if (options.Health
                && report.FullChargeCapacityInMilliwattHours is int full
                && report.DesignCapacityInMilliwattHours is int design
                && design > 0)
            {
                health = (int)Math.Clamp(Math.Round(full * 100.0 / design), 0, 100);
            }

            return system with
            {
                ChargeRateMilliwatts = watts,
                HealthPercent = health
            };
        }
        catch (Exception)
        {
            return system;
        }
    }

    private static PowerSnapshot Empty() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        HasBattery = false,
        OnAcPower = true
    };

    private static string MachineName()
    {
        try
        {
            var name = Environment.MachineName;
            return string.IsNullOrWhiteSpace(name) ? UiText.ThisPc : name;
        }
        catch (Exception)
        {
            return UiText.ThisPc;
        }
    }
}

public sealed class PerformanceCounterSampler : IPerfSampler, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly object _optionsGate = new();
    private PerfSampleOptions _options = PerfSampleOptions.Default;
    private System.Diagnostics.PerformanceCounter? _frequency;
    private System.Diagnostics.PerformanceCounter? _commitUsed;
    private System.Diagnostics.PerformanceCounter? _commitLimit;
    private long _cpuIdle;
    private long _cpuKernel;
    private long _cpuUser;
    private bool _cpuPrimed;
    private long[] _coreIdle = [];
    private long[] _coreTotal = [];
    private bool _coresPrimed;
    private bool _frequencyReady;
    private bool _commitReady;
    private TimeSpan _period = TimeSpan.FromSeconds(2);
    private int _started;
    private int _busy;
    private int _disposed;

    public PerformanceCounterSampler()
    {
        Current = new PerfSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            CpuAvailable = false,
            MemoryAvailable = false
        };
        _timer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public PerfSnapshot Current { get; private set; }

    public event EventHandler<PerfSnapshot>? Updated;

    public void Start()
    {
        if (Volatile.Read(ref _disposed) == 1 || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            EnsureCounters();
            Sample();
        });

        Arm();
    }

    public void SetQuiet(bool quiet)
    {
        lock (_optionsGate)
        {
            _period = quiet ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2);
        }

        Arm();
    }

    private void Arm()
    {
        if (Volatile.Read(ref _started) == 0 || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        TimeSpan period;
        lock (_optionsGate)
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _timer.Dispose();
        _frequency?.Dispose();
        _commitUsed?.Dispose();
        _commitLimit?.Dispose();
    }

    public void SetOptions(PerfSampleOptions options)
    {
        lock (_optionsGate)
        {
            _options = options ?? PerfSampleOptions.Default;
        }

        if (Volatile.Read(ref _started) == 1)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                EnsureCounters();
                Sample();
            });
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
        PerfSampleOptions options;
        lock (_optionsGate)
        {
            options = _options;
        }

        var cpu = TryReadCpu();
        var phys = TryReadPhysical();
        var memory = phys?.Percent;
        var frequency = options.Frequency ? TryReadFrequency() : null;
        if (!options.Frequency)
        {
            DisposeFrequency();
        }

        var cores = options.PerCore ? TryReadCores() : [];
        if (!options.PerCore)
        {
            DisposeCores();
        }

        ulong commitUsed = 0, commitLimit = 0;
        var commit = false;
        if (options.Commit)
        {
            commit = TryReadCommit(out commitUsed, out commitLimit);
        }
        else
        {
            DisposeCommit();
        }

        Current = PerfHistory.Push(
            Current,
            DateTimeOffset.UtcNow,
            cpu,
            memory,
            phys?.Used ?? 0,
            phys?.Total ?? 0,
            frequency,
            cores,
            commitUsed,
            commitLimit,
            commit);
        Updated?.Invoke(this, Current);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private double? TryReadCpu()
    {
        if (!CpuTimes.TryReadTotal(out var idle, out var kernel, out var user))
        {
            return null;
        }

        if (!_cpuPrimed)
        {
            _cpuIdle = idle;
            _cpuKernel = kernel;
            _cpuUser = user;
            _cpuPrimed = true;
            return null;
        }

        var percent = CpuTimes.BusyPercent(idle - _cpuIdle, kernel - _cpuKernel, user - _cpuUser);
        _cpuIdle = idle;
        _cpuKernel = kernel;
        _cpuUser = user;
        return percent;
    }

    private void EnsureCounters()
    {
        PerfSampleOptions options;
        lock (_optionsGate)
        {
            options = _options;
        }

        if (options.Frequency)
        {
            EnsureFrequency();
        }

        if (options.Commit)
        {
            EnsureCommit();
        }
    }

    private void EnsureFrequency()
    {
        if (_frequencyReady)
        {
            return;
        }

        var counter = TryCreate("Processor Information", "Processor Frequency", "_Total")
                      ?? TryCreate("Processor Information", "Processor Frequency", "0,0");
        if (counter is null)
        {
            return;
        }

        try
        {
            _ = counter.NextValue();
            _frequency = counter;
            _frequencyReady = true;
        }
        catch (Exception)
        {
            counter.Dispose();
        }
    }

    private IReadOnlyList<double> TryReadCores()
    {
        if (!CpuTimes.TryReadCores(out var idle, out var kernel, out var user) || idle.Length == 0)
        {
            return [];
        }

        var totals = new long[idle.Length];
        for (var i = 0; i < idle.Length; i++)
        {
            totals[i] = kernel[i] + user[i];
        }

        if (!_coresPrimed || _coreIdle.Length != idle.Length)
        {
            _coreIdle = idle;
            _coreTotal = totals;
            _coresPrimed = true;
            return [];
        }

        var values = new double[idle.Length];
        for (var i = 0; i < idle.Length; i++)
        {
            values[i] = CpuTimes.BusyPercent(idle[i] - _coreIdle[i], totals[i] - _coreTotal[i], 0);
        }

        _coreIdle = idle;
        _coreTotal = totals;
        return values;
    }

    private void EnsureCommit()
    {
        if (_commitReady)
        {
            return;
        }

        var used = TryCreate("Memory", "Committed Bytes", null);
        var limit = TryCreate("Memory", "Commit Limit", null);
        if (used is null || limit is null)
        {
            used?.Dispose();
            limit?.Dispose();
            return;
        }

        try
        {
            _ = used.NextValue();
            _ = limit.NextValue();
            _commitUsed = used;
            _commitLimit = limit;
            _commitReady = true;
        }
        catch (Exception)
        {
            used.Dispose();
            limit.Dispose();
        }
    }

    private double? TryReadFrequency()
    {
        EnsureFrequency();
        if (!_frequencyReady || _frequency is null)
        {
            return null;
        }

        try
        {
            var mhz = _frequency.NextValue();
            return mhz > 0 ? mhz : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool TryReadCommit(out ulong used, out ulong limit)
    {
        used = 0;
        limit = 0;
        EnsureCommit();
        if (!_commitReady || _commitUsed is null || _commitLimit is null)
        {
            return false;
        }

        try
        {
            used = (ulong)Math.Max(0, _commitUsed.NextValue());
            limit = (ulong)Math.Max(0, _commitLimit.NextValue());
            return limit > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void DisposeFrequency()
    {
        _frequency?.Dispose();
        _frequency = null;
        _frequencyReady = false;
    }

    private void DisposeCores()
    {
        _coreIdle = [];
        _coreTotal = [];
        _coresPrimed = false;
    }

    private void DisposeCommit()
    {
        _commitUsed?.Dispose();
        _commitLimit?.Dispose();
        _commitUsed = null;
        _commitLimit = null;
        _commitReady = false;
    }

    private readonly record struct PhysicalMemory(double Percent, ulong Used, ulong Total);

    private static PhysicalMemory? TryReadPhysical()
    {
        try
        {
            var status = new NativeMethods.MemoryStatusEx
            {
                dwLength = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
            };
            if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
            {
                return null;
            }

            var used = status.ullTotalPhys - status.ullAvailPhys;
            var percent = Math.Clamp(used * 100.0 / status.ullTotalPhys, 0, 100);
            return new PhysicalMemory(percent, used, status.ullTotalPhys);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static System.Diagnostics.PerformanceCounter? TryCreate(string category, string counter, string? instance)
    {
        try
        {
            return string.IsNullOrEmpty(instance)
                ? new System.Diagnostics.PerformanceCounter(category, counter, true)
                : new System.Diagnostics.PerformanceCounter(category, counter, instance, true);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
