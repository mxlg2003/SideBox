using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class FocusTimerEngineTests
{
    private static readonly DateTimeOffset Zero = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Completes_four_focus_sessions_then_long_break()
    {
        var engine = new FocusTimerEngine();
        Assert.Equal("25:00", FocusTimerFormat.Time(engine.Remaining(Zero)));
        Assert.Equal("专注 · 第 1 / 4 段", FocusTimerFormat.SessionLine(engine.Phase, engine.Session, engine.Settings.LongBreakEvery));

        FinishFocus(engine, Zero, sessionAfter: 1, FocusTimerPhase.ShortBreak);
        FinishBreak(engine, Zero.AddMinutes(25), sessionAfter: 2);
        FinishFocus(engine, Zero.AddMinutes(30), sessionAfter: 2, FocusTimerPhase.ShortBreak);
        FinishBreak(engine, Zero.AddMinutes(55), sessionAfter: 3);
        FinishFocus(engine, Zero.AddMinutes(80), sessionAfter: 3, FocusTimerPhase.ShortBreak);
        FinishBreak(engine, Zero.AddMinutes(105), sessionAfter: 4);
        FinishFocus(engine, Zero.AddMinutes(130), sessionAfter: 4, FocusTimerPhase.LongBreak);

        engine.Start(Zero.AddMinutes(155));
        Assert.True(engine.Tick(Zero.AddMinutes(170)));
        Assert.Equal(FocusTimerPhase.Focus, engine.Phase);
        Assert.Equal(1, engine.Session);
    }

    [Fact]
    public void ApplySettings_while_running_keeps_the_clock_going()
    {
        var engine = new FocusTimerEngine();
        engine.Start(Zero);
        engine.ApplySettings(engine.Settings with { FocusMinutes = 45 }, Zero.AddMinutes(3));
        Assert.True(engine.Running);
        Assert.Equal(FocusTimerPhase.Focus, engine.Phase);
        Assert.True(engine.Remaining(Zero.AddMinutes(3)) > TimeSpan.Zero);
    }

    [Fact]
    public void Reset_menu_restores_period_or_session_one()
    {
        var engine = new FocusTimerEngine();
        engine.Start(Zero);
        engine.Pause(Zero.AddMinutes(3));
        engine.ResetPeriod();
        Assert.False(engine.Running);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining(Zero));

        engine.Start(Zero);
        Assert.True(engine.Tick(Zero.AddMinutes(25)));
        Assert.Equal(FocusTimerPhase.ShortBreak, engine.Phase);
        engine.ResetToSession1();
        Assert.Equal(FocusTimerPhase.Focus, engine.Phase);
        Assert.Equal(1, engine.Session);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining(Zero));
    }

    [Fact]
    public void Remaining_fraction_counts_down()
    {
        var engine = new FocusTimerEngine();
        engine.Start(Zero);
        Assert.Equal(1, engine.RemainingFraction(Zero), 3);
        Assert.Equal(0.5, engine.RemainingFraction(Zero.AddMinutes(12.5)), 3);
    }

    [Fact]
    public void Time_and_status_copy()
    {
        Assert.Equal("4:43", FocusTimerFormat.Time(TimeSpan.FromSeconds(4 * 60 + 43)));
        Assert.Equal("5:00", FocusTimerFormat.Time(TimeSpan.FromMinutes(5)));
        Assert.Equal("已暂停", FocusTimerFormat.TileStatus(false, true, FocusTimerPhase.ShortBreak));
        Assert.Equal("休息", FocusTimerFormat.TileStatus(true, false, FocusTimerPhase.ShortBreak));
        Assert.Equal(FocusTimerColors.Orange, FocusTimerColors.Normalize("nope"));
    }

    private static void FinishFocus(FocusTimerEngine engine, DateTimeOffset start, int sessionAfter, FocusTimerPhase next)
    {
        engine.Start(start);
        Assert.True(engine.Tick(start + engine.Duration));
        Assert.Equal(next, engine.Phase);
        Assert.Equal(sessionAfter, engine.Session);
        Assert.False(engine.Running);
    }

    private static void FinishBreak(FocusTimerEngine engine, DateTimeOffset start, int sessionAfter)
    {
        engine.Start(start);
        Assert.True(engine.Tick(start + engine.Duration));
        Assert.Equal(FocusTimerPhase.Focus, engine.Phase);
        Assert.Equal(sessionAfter, engine.Session);
    }
}
