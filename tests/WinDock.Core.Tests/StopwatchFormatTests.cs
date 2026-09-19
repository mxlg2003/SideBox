using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class StopwatchFormatTests
{
    [Fact]
    public void Time_uses_minutes_until_an_hour()
    {
        Assert.Equal("0:00", StopwatchFormat.Time(TimeSpan.Zero));
        Assert.Equal("0:03", StopwatchFormat.Time(TimeSpan.FromSeconds(3)));
        Assert.Equal("1:05", StopwatchFormat.Time(TimeSpan.FromSeconds(65)));
        Assert.Equal("1:00:00", StopwatchFormat.Time(TimeSpan.FromHours(1)));
        Assert.Equal("1:01:01", StopwatchFormat.Time(TimeSpan.FromSeconds(3661)));
    }

    [Fact]
    public void Status_and_primary_follow_ready_running_paused()
    {
        Assert.Equal(StopwatchFormat.Ready, StopwatchFormat.Status(false, TimeSpan.Zero));
        Assert.Equal(StopwatchFormat.Start, StopwatchFormat.PrimaryAction(false, TimeSpan.Zero));
        Assert.Equal(StopwatchFormat.Running, StopwatchFormat.Status(true, TimeSpan.FromSeconds(4)));
        Assert.Equal(StopwatchFormat.Pause, StopwatchFormat.PrimaryAction(true, TimeSpan.FromSeconds(4)));
        Assert.Equal(StopwatchFormat.Paused, StopwatchFormat.Status(false, TimeSpan.FromSeconds(3)));
        Assert.Equal(StopwatchFormat.Resume, StopwatchFormat.PrimaryAction(false, TimeSpan.FromSeconds(3)));
    }
}
