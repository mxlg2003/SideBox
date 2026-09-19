using System.Diagnostics;
using WinDock.Core;

namespace WinDock.Providers;

internal static class Win32WindowSwitch
{
    public static bool TryActivate(DockItem item)
    {
        if (!PinSwitch.PrefersExistingWindow(item))
        {
            return false;
        }

        var pids = MatchProcessIds(item);
        if (pids.Count == 0)
        {
            return false;
        }

        var ownWindows = ListSwitchWindows(pids);
        var windows = ownWindows.Count > 0
            ? ownWindows
            : ListSwitchWindows(ConsoleHostParents(pids), item);
        if (windows.Count == 0)
        {
            return false;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var index = windows.FindIndex(hwnd => IsSameWindow(hwnd, foreground));
        var pick = PinSwitch.PickWindow(windows.Count, index);
        if (pick < 0)
        {
            return false;
        }

        FocusWindow(windows[pick]);
        return true;
    }

    private static HashSet<uint> MatchProcessIds(DockItem item)
    {
        var names = PinRunning.TrackedProcessNames([item]);
        var pids = new HashSet<uint>();
        if (names.Count == 0)
        {
            return pids;
        }

        Process[] processes;
        try
        {
            processes = names.SelectMany(name =>
            {
                try
                {
                    return Process.GetProcessesByName(name);
                }
                catch (Exception)
                {
                    return [];
                }
            }).ToArray();
        }
        catch (Exception)
        {
            return pids;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var image = NativeMethods.QueryProcessImage(process.Id);
                    if (string.IsNullOrWhiteSpace(image))
                    {
                        continue;
                    }

                    if (PinRunning.IsActive(item, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            DockPins.NormalizePath(image)
                        }))
                    {
                        pids.Add((uint)process.Id);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return pids;
    }

    private static HashSet<uint> ConsoleHostParents(HashSet<uint> pids)
    {
        var hosts = new HashSet<uint>();
        var parents = ParentProcessIds();
        foreach (var pid in pids)
        {
            var current = pid;
            for (var depth = 0; depth < 4; depth++)
            {
                if (!parents.TryGetValue(current, out var parent) || parent == 0 || parent == current)
                {
                    break;
                }

                current = parent;
                if (pids.Contains(parent) || hosts.Contains(parent))
                {
                    continue;
                }

                var image = NativeMethods.QueryProcessImage((int)parent);
                if (PinRunning.IsConsoleHost(image))
                {
                    hosts.Add(parent);
                }
            }
        }

        return hosts;
    }

    private static Dictionary<uint, uint> ParentProcessIds()
    {
        var map = new Dictionary<uint, uint>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32CsSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return map;
        }

        try
        {
            var entry = new NativeMethods.ProcessEntry32
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessEntry32>()
            };
            if (!NativeMethods.Process32First(snapshot, ref entry))
            {
                return map;
            }

            do
            {
                map[entry.th32ProcessID] = entry.th32ParentProcessID;
            }
            while (NativeMethods.Process32Next(snapshot, ref entry));
        }
        catch (Exception)
        {
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return map;
    }

    private static List<IntPtr> ListSwitchWindows(HashSet<uint> pids, DockItem? hostFor = null)
    {
        var visibleFrames = new List<IntPtr>();
        var hiddenFrames = new List<IntPtr>();
        var visibleTiny = new List<IntPtr>();
        if (pids.Count == 0)
        {
            return visibleFrames;
        }

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsCandidate(hwnd))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (!pids.Contains(pid))
            {
                return true;
            }

            if (hostFor is not null)
            {
                NativeMethods.TryReadWindowText(hwnd, out var title);
                if (!PinSwitch.TitleLooksLike(title, hostFor))
                {
                    return true;
                }
            }

            var visible = NativeMethods.IsWindowVisible(hwnd);
            var frame = IsAppFrame(hwnd);
            if (visible && frame)
            {
                visibleFrames.Add(hwnd);
            }
            else if (visible)
            {
                visibleTiny.Add(hwnd);
            }
            else if (frame && NativeMethods.TryReadWindowText(hwnd, out var hiddenTitle))
            {
                hiddenFrames.Add(hwnd);
            }

            return true;
        }, IntPtr.Zero);

        if (hostFor is not null
            && visibleFrames.Count == 0
            && hiddenFrames.Count == 0
            && visibleTiny.Count == 0)
        {
            return ListSwitchWindows(pids);
        }

        if (visibleFrames.Count > 0)
        {
            return visibleFrames;
        }

        if (hiddenFrames.Count > 0)
        {
            return hiddenFrames;
        }

        return visibleTiny;
    }

    private static bool IsCandidate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner) != IntPtr.Zero)
        {
            return false;
        }

        var ex = unchecked((uint)NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle));
        if ((ex & NativeMethods.WsExNoActivate) != 0)
        {
            return false;
        }

        if ((ex & NativeMethods.WsExToolWindow) != 0 && (ex & NativeMethods.WsExAppWindow) == 0)
        {
            return false;
        }

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaCloaked, out var cloaked, sizeof(int)) == 0
            && (cloaked & NativeMethods.DwmCloakedApp) != 0)
        {
            return false;
        }

        if (NativeMethods.TryReadClassName(hwnd, out var className) && IsSkippedClass(className))
        {
            return false;
        }

        return true;
    }

    private static bool IsSkippedClass(string className) =>
        className.Equals("Chrome_WidgetWin_0", StringComparison.Ordinal)
        || className.Equals("Chrome_SystemMessageWindow", StringComparison.Ordinal)
        || className.Equals("Electron_NotifyIconHostWindow", StringComparison.Ordinal)
        || className.Equals("IME", StringComparison.Ordinal)
        || className.Equals("MSCTFIME UI", StringComparison.Ordinal);

    private static bool IsAppFrame(IntPtr hwnd)
    {
        if (!NativeMethods.TryGetPlacement(hwnd, out var placement))
        {
            return false;
        }

        var width = placement.NormalPosition.Right - placement.NormalPosition.Left;
        var height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
        return PinSwitch.IsAppFrame(width, height);
    }

    private static bool IsSameWindow(IntPtr hwnd, IntPtr other)
    {
        if (hwnd == IntPtr.Zero || other == IntPtr.Zero)
        {
            return false;
        }

        if (hwnd == other)
        {
            return true;
        }

        var root = NativeMethods.GetAncestor(other, NativeMethods.GaRoot);
        if (hwnd == root)
        {
            return true;
        }

        var popup = NativeMethods.GetLastActivePopup(hwnd);
        return popup != IntPtr.Zero && popup == other;
    }

    private static IntPtr FocusTarget(IntPtr hwnd)
    {
        var popup = NativeMethods.GetLastActivePopup(hwnd);
        if (popup == IntPtr.Zero || popup == hwnd)
        {
            return hwnd;
        }

        return IsAppFrame(popup) ? popup : hwnd;
    }

    private static bool FocusWindow(IntPtr hwnd)
    {
        var target = FocusTarget(hwnd);
        try
        {
            NativeMethods.ShowWindow(target, NativeMethods.SwRestore);
            NativeMethods.SetWindowPos(
                target,
                NativeMethods.HwndTop,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);
            NativeMethods.BringWindowToTop(target);
            if (NativeMethods.SetForegroundWindow(target))
            {
                return true;
            }

            var current = NativeMethods.GetCurrentThreadId();
            var foreground = NativeMethods.GetForegroundWindow();
            var foregroundThread = foreground == IntPtr.Zero
                ? 0
                : NativeMethods.GetWindowThreadProcessId(foreground, out _);
            var targetThread = NativeMethods.GetWindowThreadProcessId(target, out _);
            var attachedFg = foregroundThread != 0 && foregroundThread != current
                             && NativeMethods.AttachThreadInput(current, foregroundThread, true);
            var attachedTarget = targetThread != 0 && targetThread != current
                                && NativeMethods.AttachThreadInput(current, targetThread, true);
            try
            {
                NativeMethods.BringWindowToTop(target);
                if (NativeMethods.SetForegroundWindow(target))
                {
                    return true;
                }

                NativeMethods.keybd_event(NativeMethods.VkMenu, 0, 0, UIntPtr.Zero);
                NativeMethods.SetForegroundWindow(target);
                NativeMethods.keybd_event(NativeMethods.VkMenu, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
                return NativeMethods.GetForegroundWindow() == target;
            }
            finally
            {
                if (attachedTarget)
                {
                    NativeMethods.AttachThreadInput(current, targetThread, false);
                }

                if (attachedFg)
                {
                    NativeMethods.AttachThreadInput(current, foregroundThread, false);
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}
