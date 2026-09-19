using System.Runtime.InteropServices;
using WinDock.Core;

namespace WinDock.Shell;

public static class UserNotificationState
{
    private const int QunsD3dFullscreen = 3;
    private const int QunsPresentation = 4;
    private const int GwlStyle = -16;
    private const uint WsMaximize = 0x01000000;

    public static bool IsPresentationOrFullscreen(nint selfHwnd = 0)
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) == 0)
            {
                if (state is QunsD3dFullscreen or QunsPresentation)
                {
                    return true;
                }

                return false;
            }
        }
        catch (Exception)
        {
        }

        return IsForegroundCoveringMonitor(selfHwnd);
    }

    private static bool IsForegroundCoveringMonitor(nint selfHwnd)
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == nint.Zero || hwnd == selfHwnd)
            {
                return false;
            }

            var className = new char[64];
            _ = GetClassName(hwnd, className, className.Length);
            var name = new string(className).TrimEnd('\0');
            if (name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "NotifyIconOverflowWindow")
            {
                return false;
            }

            if (!GetWindowRect(hwnd, out var window) || !GetMonitorInfoFor(hwnd, out var monitor))
            {
                return false;
            }

            var style = (uint)(long)GetWindowLongPtr(hwnd, GwlStyle);
            var maximized = (style & WsMaximize) != 0;
            return FullscreenHeuristics.YieldsForCoveringWindow(
                window.Right - window.Left,
                window.Bottom - window.Top,
                monitor.Right - monitor.Left,
                monitor.Bottom - monitor.Top,
                maximized);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool GetMonitorInfoFor(nint hwnd, out NativeRect monitor)
    {
        monitor = default;
        var handle = MonitorFromWindow(hwnd, 2);
        var info = new MonitorInfoNative { Size = Marshal.SizeOf<MonitorInfoNative>() };
        if (handle == nint.Zero || !GetMonitorInfo(handle, ref info))
        {
            return false;
        }

        monitor = info.Monitor;
        return true;
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoNative lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoNative
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
