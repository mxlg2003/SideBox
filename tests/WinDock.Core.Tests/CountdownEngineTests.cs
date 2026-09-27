using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class CountdownEngineTests
{
    private static readonly DateTimeOffset Zero = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_duration_is_five_minutes()
    {
        var engine = new CountdownEngine();
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Duration);
        Assert.Equal("5:00", CountdownFormat.Time(engine.Remaining(Zero)));
        Assert.Equal(CountdownFormat.Ready, CountdownFormat.TileStatus(false, false));
        Assert.True(engine.CanStart(Zero));
    }

    [Fact]
    public void Start_pause_and_tick_to_zero()
    {
        var engine = new CountdownEngine();
        engine.SetDuration(0, 1, 0, Zero);
        engine.Start(Zero);
        Assert.True(engine.Running);
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Remaining(Zero.AddSeconds(30)));

        engine.Pause(Zero.AddSeconds(30));
        Assert.True(engine.IsPaused(Zero.AddSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Remaining(Zero.AddSeconds(90)));
        engine.SetDuration(0, 1, 0, Zero.AddSeconds(90));
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Remaining(Zero.AddSeconds(90)));

        engine.Start(Zero.AddSeconds(90));
        Assert.False(engine.Tick(Zero.AddSeconds(119)));
        Assert.True(engine.Tick(Zero.AddSeconds(120)));
        Assert.False(engine.Running);
        Assert.Equal(TimeSpan.FromMinutes(1), engine.Remaining(Zero.AddSeconds(120)));
    }

    [Fact]
    public void Zero_duration_cannot_start()
    {
        var engine = new CountdownEngine();
        engine.SetDuration(0, 0, 0, Zero);
        engine.Start(Zero);
        Assert.False(engine.Running);
        Assert.False(engine.CanStart(Zero));
    }

    [Fact]
    public void Upsert_updates_same_name_and_caps_list()
    {
        var engine = new CountdownEngine();
        engine.SetDuration(0, 5, 0, Zero);
        engine.SetName("茶");
        Assert.True(engine.UpsertPreset());
        engine.SetDuration(0, 8, 0, Zero);
        Assert.True(engine.UpsertPreset());
        Assert.Single(engine.Settings.Presets);
        Assert.Equal(8, engine.Settings.Presets[0].Minutes);

        for (var i = 0; i < CountdownSettings.MaxPresets; i++)
        {
            engine.SetName($"p{i}");
            engine.SetDuration(0, i + 1, 0, Zero);
            Assert.True(engine.UpsertPreset());
        }

        Assert.Equal(CountdownSettings.MaxPresets, engine.Settings.Presets.Count);
        Assert.Equal("p0", engine.Settings.Presets[0].Name);
        engine.SetName("overflow");
        engine.SetDuration(0, 3, 0, Zero);
        Assert.True(engine.UpsertPreset());
        Assert.Equal(CountdownSettings.MaxPresets, engine.Settings.Presets.Count);
        Assert.Equal("p1", engine.Settings.Presets[0].Name);
        Assert.Equal("overflow", engine.Settings.Presets[^1].Name);
    }

    [Fact]
    public void Apply_and_remove_preset()
    {
        var engine = new CountdownEngine();
        engine.SetName("d111");
        engine.SetDuration(0, 5, 0, Zero);
        engine.UpsertPreset();
        engine.SetName("2222");
        engine.SetDuration(0, 8, 0, Zero);
        engine.UpsertPreset();

        var first = engine.Settings.Presets[0];
        Assert.True(engine.ApplyPreset(first.Id, Zero));
        Assert.Equal("d111", engine.Settings.Name);
        Assert.Equal(5, engine.Settings.Minutes);
        Assert.True(engine.RemovePreset(first.Id));
        Assert.Single(engine.Settings.Presets);
        Assert.Equal("2222", engine.Settings.Presets[0].Name);
    }

    [Fact]
    public void Normalize_clamps_duration_and_drops_empty_presets()
    {
        var settings = CountdownSettings.Normalize(new CountdownSettings
        {
            Hours = 120,
            Minutes = 90,
            Seconds = 90,
            Name = new string('a', 80),
            Presets =
            [
                new CountdownPreset { Name = "  ", Minutes = 5 },
                new CountdownPreset { Name = "ok", Hours = 1, Minutes = 2, Seconds = 3 },
                new CountdownPreset { Name = "ok", Minutes = 9 },
                new CountdownPreset { Name = "zero" }
            ]
        });
        Assert.Equal(99, settings.Hours);
        Assert.Equal(59, settings.Minutes);
        Assert.Equal(59, settings.Seconds);
        Assert.Equal(40, settings.Name.Length);
        Assert.Single(settings.Presets);
        Assert.Equal("ok", settings.Presets[0].Name);
        Assert.Equal(1, settings.Presets[0].Hours);
    }

    [Fact]
    public void ApplySettings_while_running_keeps_the_clock_going()
    {
        var engine = new CountdownEngine();
        engine.Start(Zero);
        engine.ApplySettings(engine.Settings with
        {
            Name = "茶",
            Presets = [new CountdownPreset { Name = "茶", Minutes = 5 }]
        }, Zero.AddMinutes(1));
        Assert.True(engine.Running);
        Assert.Equal(TimeSpan.FromMinutes(4), engine.Remaining(Zero.AddMinutes(1)));
        Assert.Equal("茶", engine.Settings.Name);
    }

    [Fact]
    public void Time_and_status_copy()
    {
        Assert.Equal("5:00", CountdownFormat.Time(TimeSpan.FromMinutes(5)));
        Assert.Equal("1:01:01", CountdownFormat.Time(TimeSpan.FromSeconds(3661)));
        Assert.Equal(CountdownFormat.Ready, CountdownFormat.TileStatus(true, false));
        Assert.Equal(CountdownFormat.Paused, CountdownFormat.TileStatus(false, true));
        Assert.Equal("茶", CountdownFormat.FinishedMessage("茶"));
        Assert.Equal(CountdownFormat.Finished, CountdownFormat.FinishedMessage(" "));
        Assert.False(new CountdownEngine().UpsertPreset());
    }

    [Fact]
    public void Wrap_rolls_hours_minutes_and_seconds()
    {
        Assert.Equal(0, CountdownSettings.Wrap(60, 59));
        Assert.Equal(59, CountdownSettings.Wrap(-1, 59));
        Assert.Equal(0, CountdownSettings.Wrap(100, CountdownSettings.MaxHours));
        Assert.Equal(99, CountdownSettings.Wrap(-1, CountdownSettings.MaxHours));
        Assert.Equal(8, CountdownSettings.Wrap(8, 59));
    }
}
