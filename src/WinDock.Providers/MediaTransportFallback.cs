using System.Diagnostics;
using System.Text;
using WinDock.Core;

namespace WinDock.Providers;

internal enum MediaTransportCommand
{
    PlayPause,
    Next,
    Previous
}

/// <summary>
/// Official transport when GSMTC has no usable session: player window titles,
/// PotPlayer command-line switches, WM_APPCOMMAND, then keyboard media keys.
/// </summary>
internal static class MediaTransportFallback
{
    public static MediaSessionSnapshot? TrySnapshot(bool? isPlaying = null)
    {
        MediaSessionSnapshot? untitled = null;
        foreach (var player in KnownLocalPlayers.All)
        {
            if (!IsRunning(player.ProcessName))
            {
                continue;
            }

            var windowTitle = TryReadTitleForProcess(player.ProcessName);
            var snapshot = CreateSnapshot(player, windowTitle, isPlaying);
            if (PlayerWindowTitle.LooksLikeMediaTitle(windowTitle, player.DisplayName))
            {
                return snapshot;
            }

            untitled ??= snapshot;
        }

        return untitled;
    }

    public static string? TryReadTitle(string? sourceAppId)
    {
        foreach (var name in TitleCandidates(sourceAppId))
        {
            var title = TryReadTitleForProcess(name);
            if (title is not null)
            {
                return title;
            }
        }

        return null;
    }

    public static string? TryReadTitleForProcess(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            string? best = null;
            foreach (var process in processes)
            {
                try
                {
                    var title = BestWindowTitle((uint)process.Id, process.MainWindowTitle);
                    title = PlayerWindowTitle.Sanitize(title);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    if (best is null || title.Length > best.Length)
                    {
                        best = title;
                    }
                }
                catch (Exception)
                {
                    // Keep looking.
                }
            }

            return best;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public static bool TrySend(MediaTransportCommand command, string? sourceAppId, bool isPlaying)
    {
        var targets = KnownLocalPlayers.ProcessNamesForSource(sourceAppId);
        if (ShouldUsePotPlayerCli(sourceAppId, targets) && SendPotPlayerCommand(command, isPlaying))
        {
            return true;
        }

        var appCommand = command switch
        {
            MediaTransportCommand.Next => NativeMethods.AppcommandMediaNextTrack,
            MediaTransportCommand.Previous => NativeMethods.AppcommandMediaPreviousTrack,
            MediaTransportCommand.PlayPause when isPlaying => NativeMethods.AppcommandMediaPause,
            _ => NativeMethods.AppcommandMediaPlay
        };

        if (targets.Count > 0)
        {
            foreach (var name in targets)
            {
                if (SendAppCommand(name, appCommand))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private static MediaSessionSnapshot CreateSnapshot(KnownLocalPlayer player, string? windowTitle, bool? isPlaying)
    {
        var hasTitle = PlayerWindowTitle.LooksLikeMediaTitle(windowTitle, player.DisplayName);
        return new MediaSessionSnapshot
        {
            SourceAppId = KnownLocalPlayers.SourceId(player.ProcessName),
            IsPlaying = isPlaying ?? true,
            CanPlayPause = true,
            CanSkipNext = true,
            CanSkipPrevious = true,
            Title = hasTitle ? windowTitle : player.DisplayName,
            Artist = hasTitle ? player.DisplayName : "未接入系统媒体，可用按钮控制",
            UsesFallbackTransport = true
        };
    }

    private static IEnumerable<string> TitleCandidates(string? sourceAppId)
    {
        var names = KnownLocalPlayers.ProcessNamesForSource(sourceAppId);
        if (names.Count > 0)
        {
            return names;
        }

        if (string.IsNullOrWhiteSpace(sourceAppId) || KnownLocalPlayers.IsPotPlayerSource(sourceAppId))
        {
            return KnownLocalPlayers.All
                .Where(player => KnownLocalPlayers.IsPotPlayerProcess(player.ProcessName))
                .Select(player => player.ProcessName);
        }

        return [];
    }

    private static bool ShouldUsePotPlayerCli(string? sourceAppId, IReadOnlyList<string> targets)
    {
        if (KnownLocalPlayers.IsPotPlayerSource(sourceAppId) || KnownLocalPlayers.IsPotPlayerProcess(sourceAppId))
        {
            return true;
        }

        foreach (var name in targets)
        {
            if (KnownLocalPlayers.IsPotPlayerProcess(name))
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(sourceAppId) || targets.Count == 0)
        {
            return AnyPotPlayerRunning();
        }

        return false;
    }

    public static string? TryRunningPotPlayerProcessName()
    {
        foreach (var player in KnownLocalPlayers.All)
        {
            if (KnownLocalPlayers.IsPotPlayerProcess(player.ProcessName) && IsRunning(player.ProcessName))
            {
                return player.ProcessName;
            }
        }

        return null;
    }

    private static bool AnyPotPlayerRunning() => TryRunningPotPlayerProcessName() is not null;

    private static bool SendPotPlayerCommand(MediaTransportCommand command, bool isPlaying)
    {
        var argument = command switch
        {
            MediaTransportCommand.Next => "/next",
            MediaTransportCommand.Previous => "/prev",
            MediaTransportCommand.PlayPause when isPlaying => "/pause",
            _ => "/play"
        };

        foreach (var player in KnownLocalPlayers.All)
        {
            if (KnownLocalPlayers.IsPotPlayerProcess(player.ProcessName) && LaunchExisting(player.ProcessName, argument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LaunchExisting(string processName, string arguments)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                var path = TryGetProcessPath(process);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.Dispose();
                    return true;
                }
                catch (Exception)
                {
                    // Try the next instance.
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

        return false;
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch (Exception)
        {
            // Fall through to QueryFullProcessImageName.
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (NativeMethods.QueryFullProcessImageName(process.Handle, 0, buffer, ref size))
            {
                return buffer.ToString();
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static string? BestWindowTitle(uint processId, string? mainWindowTitle)
    {
        var best = mainWindowTitle ?? string.Empty;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != processId)
            {
                return true;
            }

            var text = ReadWindowText(hwnd);
            if (!string.IsNullOrWhiteSpace(text) && text.Length > best.Length)
            {
                best = text;
            }

            return true;
        }, IntPtr.Zero);

        return string.IsNullOrWhiteSpace(best) ? null : best;
    }

    private static string? ReadWindowText(IntPtr hwnd)
    {
        var buffer = new StringBuilder(512);
        var length = NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
        return length <= 0 ? null : buffer.ToString();
    }

    private static bool IsRunning(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Keep looking.
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

        return false;
    }

    private static bool SendAppCommand(string processName, int appCommand)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        hwnd = FirstWindow((uint)process.Id, visibleOnly: true);
                    }

                    if (hwnd == IntPtr.Zero)
                    {
                        hwnd = FirstWindow((uint)process.Id, visibleOnly: false);
                    }

                    if (hwnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    NativeMethods.SendMessage(
                        hwnd,
                        NativeMethods.WmAppcommand,
                        hwnd,
                        (IntPtr)(appCommand << 16));
                    return true;
                }
                catch (Exception)
                {
                    // Try the next process instance.
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

        return false;
    }

    private static IntPtr FirstWindow(uint processId, bool visibleOnly)
    {
        var found = IntPtr.Zero;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != processId)
            {
                return true;
            }

            if (visibleOnly && NativeMethods.IsWindowVisible(hwnd) is false)
            {
                return true;
            }

            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static bool SendMediaKey(MediaTransportCommand command)
    {
        var vk = command switch
        {
            MediaTransportCommand.Next => NativeMethods.VkMediaNextTrack,
            MediaTransportCommand.Previous => NativeMethods.VkMediaPreviousTrack,
            _ => NativeMethods.VkMediaPlayPause
        };

        try
        {
            NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(vk, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
