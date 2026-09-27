using System.Runtime.InteropServices;
using WinDock.Core;

namespace WinDock.Shell;

public sealed record NativeMonitorEntry(
    nint Handle,
    string DeviceName,
    bool IsPrimary,
    int PixelLeft,
    int PixelTop,
    int PixelWidth,
    int PixelHeight,
    int WorkLeft,
    int WorkTop,
    int WorkWidth,
    int WorkHeight,
    double Dpi);

public static class NativeMonitors
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint MonitorInfoFPrimary = 0x00000001;

    public static IReadOnlyList<NativeMonitorEntry> Enumerate()
    {
        var list = new List<NativeMonitorEntry>();
        EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, _, _, _) =>
        {
            var info = new MonitorInfoExNative { Size = Marshal.SizeOf<MonitorInfoExNative>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var device = info.DeviceName.TrimEnd('\0');
                var isPrimary = (info.Flags & MonitorInfoFPrimary) != 0;
                var dpi = QueryDpi(hMonitor);

                var pixelWidth = info.Monitor.Right - info.Monitor.Left;
                var pixelHeight = info.Monitor.Bottom - info.Monitor.Top;
                var workWidth = info.Work.Right - info.Work.Left;
                var workHeight = info.Work.Bottom - info.Work.Top;

                list.Add(new NativeMonitorEntry(
                    hMonitor,
                    device,
                    isPrimary,
                    info.Monitor.Left,
                    info.Monitor.Top,
                    pixelWidth,
                    pixelHeight,
                    info.Work.Left,
                    info.Work.Top,
                    workWidth,
                    workHeight,
                    dpi));
            }

            return true;
        }, nint.Zero);

        return list;
    }

    public static NativeMonitorEntry? FromPoint(int x, int y)
    {
        var point = new NativePoint { X = x, Y = y };
        var handle = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (handle == nint.Zero)
        {
            return null;
        }

        var info = new MonitorInfoExNative { Size = Marshal.SizeOf<MonitorInfoExNative>() };
        if (!GetMonitorInfo(handle, ref info))
        {
            return null;
        }

        var device = info.DeviceName.TrimEnd('\0');
        var isPrimary = (info.Flags & MonitorInfoFPrimary) != 0;
        var dpi = QueryDpi(handle);

        return new NativeMonitorEntry(
            handle,
            device,
            isPrimary,
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right - info.Monitor.Left,
            info.Monitor.Bottom - info.Monitor.Top,
            info.Work.Left,
            info.Work.Top,
            info.Work.Right - info.Work.Left,
            info.Work.Bottom - info.Work.Top,
            dpi);
    }

    public static string GetCurrentCursorMonitorId()
    {
        if (GetCursorPos(out var pt))
        {
            var entry = FromPoint(pt.X, pt.Y);
            if (entry is not null)
            {
                return entry.IsPrimary
                    ? "primary"
                    : DisplayConfig.DevicePathFor(entry.DeviceName) ?? entry.DeviceName;
            }
        }

        return "primary";
    }

    private static double QueryDpi(nint hMonitor)
    {
        try
        {
            if (GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX;
            }
        }
        catch (Exception)
        {
        }

        return DisplayMetrics.DefaultDpi;
    }

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoExNative lpmi);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint pt, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    public struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoExNative
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
