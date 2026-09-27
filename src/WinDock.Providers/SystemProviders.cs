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
    private int _busy;
    private int _disposed;
    private int _started;
    private long _burstUntilTicks;

    public Win32PowerProvider()
    {
        Current = Combine(ReadSystem(), []);
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Stop();
        _timer.Dispose();
    }

    public static PowerSnapshot Read() => Combine(ReadSystem(), []);

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
            var snapshot = Combine(ReadSystem(), PowerDeviceEnumerator.TryRead());
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

    private static PowerSnapshot ReadSystem()
    {
        try
        {
            if (!NativeMethods.GetSystemPowerStatus(out var status))
            {
                return Empty();
            }

            var noBattery = (status.BatteryFlag & 128) != 0 || status.BatteryLifePercent == 255;
            return new PowerSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                HasBattery = !noBattery,
                OnAcPower = status.ACLineStatus == 1,
                PercentRemaining = noBattery ? null : status.BatteryLifePercent
            };
        }
        catch (Exception)
        {
            return Empty();
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
    private System.Diagnostics.PerformanceCounter? _cpu;
    private bool _cpuReady;
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

        _timer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
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
        _cpu?.Dispose();
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
        double? cpu = null;
        PhysicalMemory? phys = null;
        try
        {
            if (_cpuReady && _cpu is not null)
            {
                cpu = Math.Clamp(_cpu.NextValue(), 0, 100);
            }
        }
        catch (Exception)
        {
            cpu = null;
        }

        phys = TryReadPhysical();
        var memory = phys?.Percent;
        Current = PerfHistory.Push(
            Current,
            DateTimeOffset.UtcNow,
            cpu,
            memory,
            phys?.Used ?? 0,
            phys?.Total ?? 0);
        Updated?.Invoke(this, Current);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void EnsureCounters()
    {
        if (_cpu is not null || _cpuReady)
        {
            return;
        }

        var cpu = TryCreate("Processor", "% Processor Time", "_Total");
        if (cpu is not null)
        {
            try
            {
                _ = cpu.NextValue();
                _cpu = cpu;
                _cpuReady = true;
            }
            catch (Exception)
            {
                cpu.Dispose();
            }
        }
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
