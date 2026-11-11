using System.Runtime.InteropServices;

namespace WinDock.Providers;

public readonly record struct SystemProcessRow(int Pid, string Name, long CpuTicks, ulong WorkingSet);

/// <summary>
/// One system process snapshot shared by the process list and pin-running checks.
/// On 64-bit this walks <c>SystemProcessInformation</c> and does not open a handle per process.
/// </summary>
public static class SystemProcessTable
{
    private const int SystemProcessInformation = 5;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    private static readonly object Gate = new();
    private static long _readAt = long.MinValue;
    private static SystemProcessRow[] _cached = [];

    public static SystemProcessRow[] ReadRecent()
    {
        lock (Gate)
        {
            var now = Environment.TickCount64;
            if (now - _readAt < 1500 && _cached.Length > 0)
            {
                return _cached;
            }

            _cached = Capture();
            _readAt = now;
            return _cached;
        }
    }

    public static SystemProcessRow[] Capture()
    {
        if (!Environment.Is64BitProcess)
        {
            return CaptureManaged();
        }

        var size = 256 * 1024;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            while (true)
            {
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, size, out var needed);
                if (status == 0)
                {
                    return Parse(buffer, needed > 0 ? needed : size);
                }

                if (status != StatusInfoLengthMismatch || needed <= 0 || needed > 64 * 1024 * 1024)
                {
                    return [];
                }

                size = Math.Max(needed, size * 2);
                Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(size);
            }
        }
        catch (Exception)
        {
            return [];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static SystemProcessRow[] Parse(IntPtr buffer, int size)
    {
        var rows = new List<SystemProcessRow>(256);
        var entry = buffer;
        var end = buffer + size;
        while (entry >= buffer && entry + 152 <= end)
        {
            var name = ReadName(entry);
            var pid = (int)Marshal.ReadIntPtr(entry, 80);
            if (pid > 0 && !IsIdle(name))
            {
                var user = Marshal.ReadInt64(entry, 40);
                var kernel = Marshal.ReadInt64(entry, 48);
                var workingSet = (ulong)Marshal.ReadInt64(entry, 144);
                rows.Add(new SystemProcessRow(pid, name, user + kernel, workingSet));
            }

            var next = Marshal.ReadInt32(entry, 0);
            if (next <= 0)
            {
                break;
            }

            entry += next;
        }

        return rows.ToArray();
    }

    private static string ReadName(IntPtr entry)
    {
        var bytes = Marshal.ReadInt16(entry, 56);
        var buffer = Marshal.ReadIntPtr(entry, 64);
        if (buffer == IntPtr.Zero || bytes < 2)
        {
            return string.Empty;
        }

        var text = Marshal.PtrToStringUni(buffer, bytes / 2) ?? string.Empty;
        if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return text[..^4];
        }

        return text;
    }

    private static bool IsIdle(string name) =>
        name.Length == 0
        || name.Equals("Idle", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System Idle Process", StringComparison.OrdinalIgnoreCase);

    private static SystemProcessRow[] CaptureManaged()
    {
        System.Diagnostics.Process[] processes;
        try
        {
            processes = System.Diagnostics.Process.GetProcesses();
        }
        catch (Exception)
        {
            return [];
        }

        var rows = new List<SystemProcessRow>(processes.Length);
        foreach (var process in processes)
        {
            try
            {
                if (process.Id == 0 || IsIdle(process.ProcessName))
                {
                    continue;
                }

                rows.Add(new SystemProcessRow(
                    process.Id,
                    process.ProcessName,
                    process.TotalProcessorTime.Ticks,
                    (ulong)Math.Max(0, process.WorkingSet64)));
            }
            catch (Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return rows.ToArray();
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength, out int returnLength);
}

internal static class CpuTimes
{
    private const int SystemProcessorPerformanceInformation = 8;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int ProcessorStride = 48;

    public static bool TryReadTotal(out long idle, out long kernel, out long user)
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            idle = 0;
            kernel = 0;
            user = 0;
            return false;
        }

        idle = ToTicks(idleTime);
        kernel = ToTicks(kernelTime);
        user = ToTicks(userTime);
        return true;
    }

    public static bool TryReadCores(out long[] idle, out long[] kernel, out long[] user)
    {
        idle = [];
        kernel = [];
        user = [];
        var bytes = Math.Max(1, Environment.ProcessorCount) * ProcessorStride;
        var buffer = Marshal.AllocHGlobal(bytes);
        try
        {
            var status = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buffer, bytes, out var needed);
            if (status == StatusInfoLengthMismatch && needed > bytes && needed < 1024 * 1024)
            {
                Marshal.FreeHGlobal(buffer);
                bytes = needed;
                buffer = Marshal.AllocHGlobal(bytes);
                status = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buffer, bytes, out _);
            }

            if (status != 0)
            {
                return false;
            }

            var count = bytes / ProcessorStride;
            idle = new long[count];
            kernel = new long[count];
            user = new long[count];
            for (var i = 0; i < count; i++)
            {
                var at = buffer + (i * ProcessorStride);
                idle[i] = Marshal.ReadInt64(at, 0);
                kernel[i] = Marshal.ReadInt64(at, 8);
                user[i] = Marshal.ReadInt64(at, 16);
            }

            return count > 0;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static double BusyPercent(long idleDelta, long kernelDelta, long userDelta)
    {
        var total = kernelDelta + userDelta;
        if (total <= 0)
        {
            return 0;
        }

        var busy = total - idleDelta;
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }

    private static long ToTicks(FileTime value) => ((long)value.High << 32) | value.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength, out int returnLength);
}
