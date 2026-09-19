using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class PinSwitchTests
{
    [Fact]
    public void Click_prefers_existing_window_for_apps_not_folders()
    {
        var app = DockPins.FromTarget(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "Windows PowerShell");
        var folder = DockPins.FromTarget(@"C:\Windows");
        var file = DockPins.FromTarget(@"C:\Notes\todo.txt");
        Assert.True(PinSwitch.PrefersExistingWindow(app));
        Assert.False(PinSwitch.PrefersExistingWindow(folder));
        Assert.False(PinSwitch.PrefersExistingWindow(file));
    }

    [Fact]
    public void Pick_window_activates_first_unless_already_on_that_app()
    {
        Assert.Equal(-1, PinSwitch.PickWindow(0, -1));
        Assert.Equal(0, PinSwitch.PickWindow(2, -1));
        Assert.Equal(1, PinSwitch.PickWindow(2, 0));
        Assert.Equal(0, PinSwitch.PickWindow(2, 1));
        Assert.Equal(0, PinSwitch.PickWindow(1, 0));
    }

    [Fact]
    public void Host_window_title_matches_exe_or_pin_title()
    {
        var pin = DockPins.FromTarget(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "Windows PowerShell");
        Assert.True(PinSwitch.TitleLooksLike("Windows PowerShell", pin));
        Assert.True(PinSwitch.TitleLooksLike(@"C:\src — powershell", pin));
        Assert.False(PinSwitch.TitleLooksLike("Ubuntu", pin));
        Assert.False(PinSwitch.TitleLooksLike("", pin));
    }

    [Fact]
    public void Console_host_names_include_windows_terminal()
    {
        Assert.True(PinRunning.IsConsoleHost(@"C:\Program Files\WindowsApps\WindowsTerminal.exe"));
        Assert.True(PinRunning.IsConsoleHost(@"C:\Windows\System32\conhost.exe"));
        Assert.True(PinRunning.IsConsoleHost(@"C:\Windows\System32\OpenConsole.exe"));
        Assert.False(PinRunning.IsConsoleHost(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"));
    }

    [Fact]
    public void App_frame_skips_electron_tray_overlays()
    {
        Assert.False(PinSwitch.IsAppFrame(160, 64));
        Assert.False(PinSwitch.IsAppFrame(275, 223));
        Assert.True(PinSwitch.IsAppFrame(1086, 1252));
        Assert.True(PinSwitch.IsAppFrame(800, 600));
    }
}
