using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class AlarmEngineTests
{
    private static readonly DateTimeOffset Zero = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void Default_is_eight_idle()
    {
        var engine = new AlarmEngine(zone: Utc);
        Assert.Equal(8, engine.Settings.Hour);
        Assert.Equal(0, engine.Settings.Minute);
        Assert.False(engine.Armed);
        Assert.Equal("08:00", AlarmFormat.Clock(engine.Settings.Hour, engine.Settings.Minute));
        Assert.Equal(AlarmFormat.IdleTitle, BuiltInWidgets.DisplayName(BuiltInWidgets.Alarm));
        Assert.Equal(AlarmDay.Tomorrow, engine.Day(Zero));
        Assert.Equal("明天 08:00", AlarmFormat.When(AlarmDay.Tomorrow, 8, 0));
    }

    [Fact]
    public void Arm_today_when_time_is_ahead()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(14, 30);
        engine.SetLabel("Call Alex");
        engine.Arm(Zero);
        Assert.True(engine.Armed);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 14, 30, 0, TimeSpan.Zero), engine.Settings.RingsAt);
        Assert.Equal(TimeSpan.FromHours(4.5), engine.Remaining(Zero));
        Assert.Equal(AlarmDay.Today, engine.Day(Zero));
        Assert.Equal("今天 · 还剩 4:30:00", AlarmFormat.ArmedStatus(AlarmDay.Today, TimeSpan.FromHours(4.5)));
        Assert.Equal("4h30m", AlarmFormat.CompactRemaining(TimeSpan.FromHours(4.5)));
    }

    [Fact]
    public void Arm_tomorrow_when_time_already_passed()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(8, 0);
        engine.Arm(Zero);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero), engine.Settings.RingsAt);
        Assert.Equal(AlarmDay.Tomorrow, engine.Day(Zero));
        Assert.Equal("明天 08:00", AlarmFormat.When(AlarmDay.Tomorrow, 8, 0));
        Assert.Equal("22h00m", AlarmFormat.CompactRemaining(engine.Remaining(Zero)));
    }

    [Fact]
    public void Tick_fires_at_rings_at_and_disarms()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(10, 1);
        engine.SetLabel("茶");
        engine.Arm(Zero);
        Assert.False(engine.Tick(Zero.AddSeconds(59)));
        Assert.True(engine.Armed);
        Assert.True(engine.Tick(Zero.AddMinutes(1)));
        Assert.False(engine.Armed);
        Assert.Null(engine.Settings.RingsAt);
        Assert.Equal("茶", engine.Settings.Label);
        Assert.Equal("茶", AlarmFormat.FinishedMessage(engine.Settings.Label));
        Assert.Equal(AlarmFormat.Finished, AlarmFormat.FinishedMessage(" "));
    }

    [Fact]
    public void Overdue_within_window_still_fires()
    {
        var engine = new AlarmEngine(new AlarmSettings
        {
            Hour = 9,
            Minute = 55,
            Armed = true,
            RingsAt = Zero.AddMinutes(-5)
        }, Utc);
        Assert.True(engine.Tick(Zero));
        Assert.False(engine.Armed);
    }

    [Fact]
    public void Stale_alarm_is_dropped_without_firing()
    {
        var ringsAt = Zero.AddHours(-40);
        var engine = new AlarmEngine(new AlarmSettings
        {
            Hour = 10,
            Armed = true,
            RingsAt = ringsAt
        }, Utc);
        engine.ApplySettings(engine.Settings, Zero);
        Assert.False(engine.Armed);
        Assert.False(engine.Tick(Zero));
    }

    [Fact]
    public void ApplySettings_keeps_a_live_ring()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(14, 30);
        engine.Arm(Zero);
        engine.ApplySettings(engine.Settings with { Label = "Call Alex" }, Zero.AddHours(1));
        Assert.True(engine.Armed);
        Assert.Equal("Call Alex", engine.Settings.Label);
        Assert.Equal(new DateTimeOffset(2026, 9, 19, 14, 30, 0, TimeSpan.Zero), engine.Settings.RingsAt);
        Assert.Equal(TimeSpan.FromHours(3.5), engine.Remaining(Zero.AddHours(1)));
    }

    [Fact]
    public void SetTime_is_ignored_while_armed()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(14, 30);
        engine.Arm(Zero);
        engine.SetTime(9, 0);
        Assert.Equal(14, engine.Settings.Hour);
        Assert.Equal(30, engine.Settings.Minute);
    }

    [Fact]
    public void Cancel_clears_the_ring_and_keeps_the_clock()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(14, 30);
        engine.SetLabel("Call Alex");
        engine.Arm(Zero);
        engine.Cancel();
        Assert.False(engine.Armed);
        Assert.Null(engine.Settings.RingsAt);
        Assert.Equal(14, engine.Settings.Hour);
        Assert.Equal("Call Alex", engine.Settings.Label);
    }

    [Fact]
    public void Normalize_clamps_and_drops_ring_when_idle()
    {
        var settings = AlarmSettings.Normalize(new AlarmSettings
        {
            Hour = 30,
            Minute = 90,
            Label = new string('a', 80),
            Armed = false,
            RingsAt = Zero
        });
        Assert.Equal(23, settings.Hour);
        Assert.Equal(59, settings.Minute);
        Assert.Equal(40, settings.Label.Length);
        Assert.Null(settings.RingsAt);
        Assert.Equal(0, AlarmSettings.Wrap(24, 23));
        Assert.Equal(23, AlarmSettings.Wrap(-1, 23));
        Assert.Equal(0, AlarmSettings.Wrap(60, 59));
    }

    [Fact]
    public void Exact_clock_time_rolls_to_tomorrow()
    {
        var engine = new AlarmEngine(zone: Utc);
        engine.SetTime(10, 0);
        engine.Arm(Zero);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), engine.Settings.RingsAt);
        Assert.Equal(AlarmDay.Tomorrow, engine.Day(Zero));
    }

    [Fact]
    public void Remaining_copy()
    {
        Assert.Equal("16h54m", AlarmFormat.CompactRemaining(new TimeSpan(16, 54, 8)));
        Assert.Equal("5m", AlarmFormat.CompactRemaining(TimeSpan.FromMinutes(5)));
        Assert.Equal("9s", AlarmFormat.CompactRemaining(TimeSpan.FromSeconds(9)));
        Assert.Equal("16:54:08", AlarmFormat.Remaining(new TimeSpan(16, 54, 8)));
        Assert.Equal("0:05:00", AlarmFormat.Remaining(TimeSpan.FromMinutes(5)));
    }
}
