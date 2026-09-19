using WinDock.Core;
using WinDock.Providers;
using Xunit;

namespace WinDock.Core.Tests;

/// <summary>
/// Geometry, hotkeys, and pin helpers. These are not working-set / PDH measurements.
/// </summary>
public sealed class HostPerfTests
{
    [Fact]
    public void Pixels_to_dip_scales_by_dpi()
    {
        Assert.Equal(96, DisplayMetrics.PixelsToDip(144, 144));
        Assert.Equal(1.5, DisplayMetrics.ScaleFromDpi(144));
        var rect = DisplayMetrics.PixelRectToDip(1920, 0, 1920, 1080, 144);
        Assert.Equal(1280, rect.X);
        Assert.Equal(1280, rect.Width);
    }

    [Fact]
    public void Icon_alpha_ignores_near_transparent_padding()
    {
        const int width = 4;
        const int height = 4;
        var bgra = new byte[width * height * 4];
        SetPixel(bgra, width, 1, 1, 255);
        SetPixel(bgra, width, 2, 2, 255);
        var box = IconAlpha.OpaqueBounds(bgra, width, height, width * 4);
        Assert.Equal((1, 1, 2, 2), box);
    }

    [Fact]
    public void Top_hotzone_is_an_eight_pixel_strip()
    {
        var work = new RectLogical(0, 0, 1920, 1080);
        Assert.Equal(8, DockHostGeometry.HotzonePixels);
        Assert.True(DockHostGeometry.InEdgeHotzone(100, 1, work, DockEdge.Top));
        Assert.True(DockHostGeometry.InEdgeHotzone(100, 7, work, DockEdge.Top));
        Assert.False(DockHostGeometry.InEdgeHotzone(100, 8, work, DockEdge.Top));
        Assert.True(DockHostGeometry.InEdgeHotzone(1919, 500, work, DockEdge.Right));
        var island = new RectLogical(700, 10, 400, 60);
        Assert.True(DockHostGeometry.InIslandApproachHotzone(800, 1, work, DockEdge.Top, island));
        Assert.False(DockHostGeometry.InIslandApproachHotzone(100, 1, work, DockEdge.Top, island));
        Assert.False(DockHostGeometry.IsPointerOnIsland(overWindow: true, stripMode: true, overFlyout: false));
        Assert.True(DockHostGeometry.IsPointerOnIsland(overWindow: true, stripMode: false, overFlyout: false));
        Assert.True(DockHostGeometry.IsPointerOnIsland(overWindow: false, stripMode: true, overFlyout: true));
    }

    [Fact]
    public void Hotkey_parser_reads_win_shift_chords()
    {
        Assert.True(HotkeyParser.TryParse("Win+Shift+D", out var modifiers, out var vk));
        Assert.Equal(HotkeyParser.ModWin | HotkeyParser.ModShift, modifiers);
        Assert.Equal('D', vk);
        Assert.True(HotkeyParser.TryParse("Win+Shift+Z", out var undoModifiers, out var undoVk));
        Assert.Equal(HotkeyParser.ModWin | HotkeyParser.ModShift, undoModifiers);
        Assert.Equal('Z', undoVk);
        Assert.True(HotkeyParser.TryParse("Win+Shift+A", out var wakeModifiers, out var wakeVk));
        Assert.Equal(HotkeyParser.ModWin | HotkeyParser.ModShift, wakeModifiers);
        Assert.Equal('A', wakeVk);
        Assert.Equal("Win+Shift+A", HotkeyParser.Format(wakeModifiers, wakeVk));
        Assert.False(HotkeyParser.TryParse("A", out _, out _));
        Assert.False(HotkeyParser.TryParse("D", out _, out _));
    }

    [Fact]
    public void Tracked_process_names_use_exe_file_name()
    {
        var names = PinRunning.TrackedProcessNames(
        [
            DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo"),
            DockPins.FromTarget(@"C:\Notes\todo.txt")
        ]);
        Assert.Equal("app", Assert.Single(names));
    }

    [Fact]
    public void Shortcut_path_is_kept_on_resolved_exe_pins()
    {
        var pin = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo", shortcutPath: @"C:\Menu\Demo.lnk");
        Assert.Equal(DockItemType.App, pin.Type);
        Assert.Equal(@"C:\Menu\Demo.lnk", pin.ShortcutPath);
        var normalized = SettingsEditor.AddPin(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            pin);
        var stored = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Title == "Demo");
        Assert.Equal(@"C:\Menu\Demo.lnk", stored.ShortcutPath);
        Assert.Equal(@"C:\Apps\Demo\app.exe", stored.Path, ignoreCase: true);
    }

    [Fact]
    public void CreatePin_from_lnk_keeps_shortcut_and_resolved_exe()
    {
        var temp = Path.Combine(Path.GetTempPath(), "WinDockLnk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var lnk = Path.Combine(temp, "Demo.lnk");
            var exe = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            CreateShortcut(lnk, exe, "/a");
            var pin = new Win32ShellLaunch(Path.Combine(temp, "icons")).CreatePin(lnk);
            Assert.Equal(DockItemType.App, pin.Type);
            Assert.Equal(exe, pin.Path, ignoreCase: true);
            Assert.Equal(lnk, pin.ShortcutPath, ignoreCase: true);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void CreateShortcut(string lnkPath, string target, string arguments)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")
                   ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(type)
                        ?? throw new InvalidOperationException("WScript.Shell could not start.");
        var shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath = target;
        shortcut.Arguments = arguments;
        shortcut.Save();
    }

    private static void SetPixel(byte[] bgra, int width, int x, int y, byte alpha)
    {
        var i = ((y * width) + x) * 4;
        bgra[i + 3] = alpha;
    }
}
