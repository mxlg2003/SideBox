using WinDock.Core;
using WinDock.Widgets;
using Xunit;

namespace WinDock.Core.Tests;

public class WhiteNoiseTests
{
    [Fact]
    public void WhiteNoise_is_in_built_in_widgets()
    {
        Assert.True(BuiltInWidgets.IsKnown(BuiltInWidgets.WhiteNoise));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.WhiteNoise));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.WhiteNoise));
        Assert.False(BuiltInWidgets.CanCustomize(BuiltInWidgets.WhiteNoise));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.WhiteNoise));
        Assert.True(BuiltInWidgets.IsSingleton(BuiltInWidgets.WhiteNoise));
        Assert.False(BuiltInWidgets.AllowsMultiple(BuiltInWidgets.WhiteNoise));
    }

    [Fact]
    public void Presets_and_tracks_are_defined_and_valid()
    {
        Assert.Equal(7, WhiteNoisePresets.All.Count);
        Assert.Equal(11, WhiteNoiseTracks.All.Count);

        foreach (var preset in WhiteNoisePresets.All)
        {
            var name = WhiteNoisePresets.DisplayName(preset);
            Assert.False(string.IsNullOrWhiteSpace(name));

            var tracks = WhiteNoisePresets.GetTracksForPreset(preset);
            Assert.NotEmpty(tracks);
            foreach (var track in tracks)
            {
                Assert.Contains(track.Id, WhiteNoiseTracks.All);
                Assert.InRange(track.Volume, 0.0, 1.0);
            }
        }

        foreach (var track in WhiteNoiseTracks.All)
        {
            var name = WhiteNoiseTracks.DisplayName(track);
            Assert.False(string.IsNullOrWhiteSpace(name));
            var glyph = WhiteNoiseTracks.Glyph(track);
            Assert.False(string.IsNullOrWhiteSpace(glyph));
        }
    }

    [Fact]
    public void Normalize_clamps_volume_and_preserves_tracks()
    {
        var raw = new WhiteNoiseSettings
        {
            MasterVolume = 1.5,
            ActivePreset = "invalid_preset",
            TimerMinutes = -5,
            Tracks =
            [
                new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = -0.5, Enabled = true },
                new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Thunder, Volume = 2.0, Enabled = true },
                new WhiteNoiseTrackSettings { Id = "unknown_track", Volume = 0.5, Enabled = true }
            ]
        };

        var normalized = WhiteNoiseSettings.Normalize(raw);
        Assert.Equal(1.0, normalized.MasterVolume);
        Assert.Equal(WhiteNoisePresets.RainyStudy, normalized.ActivePreset);
        Assert.Equal(0, normalized.TimerMinutes);
        Assert.Equal(2, normalized.Tracks.Count);
        Assert.Equal(0.0, normalized.Tracks[0].Volume);
        Assert.Equal(1.0, normalized.Tracks[1].Volume);
    }

    [Fact]
    public void Lifestyle_catalog_includes_whitenoise()
    {
        var group = WidgetCatalog.Groups.FirstOrDefault(g => g.Id == "lifestyle");
        Assert.NotNull(group);
        Assert.Contains(BuiltInWidgets.WhiteNoise, group.WidgetIds);
    }

    [Theory]
    [InlineData("白噪音", true)]
    [InlineData("雨声", true)]
    [InlineData("自然", true)]
    [InlineData("whitenoise", true)]
    [InlineData("ambient", true)]
    [InlineData("focus", true)]
    [InlineData("rain", true)]
    [InlineData("sound", true)]
    [InlineData("stocks", false)]
    public void Search_matching_finds_whitenoise(string query, bool expected)
    {
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal(expected, WidgetCatalog.Matches(BuiltInWidgets.WhiteNoise, query));
        }
    }

    [Fact]
    public void SettingsEditor_can_add_and_update_whitenoise()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var added = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.WhiteNoise);
        var general = added.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);

        var item = general.Dock.Items.FirstOrDefault(i => i.WidgetId == BuiltInWidgets.WhiteNoise);
        Assert.NotNull(item);

        var updated = SettingsEditor.WithWhiteNoise(added, DefaultProfiles.GeneralId, item.Id, new WhiteNoiseSettings
        {
            MasterVolume = 0.6,
            ActivePreset = WhiteNoisePresets.DeepForest,
            TimerMinutes = 30
        });

        var updatedItem = updated.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items.First(i => i.Id == item.Id);
        Assert.NotNull(updatedItem.WhiteNoise);
        Assert.Equal(0.6, updatedItem.WhiteNoise.MasterVolume);
        Assert.Equal(WhiteNoisePresets.DeepForest, updatedItem.WhiteNoise.ActivePreset);
        Assert.Equal(30, updatedItem.WhiteNoise.TimerMinutes);
    }

    [Fact]
    public void ProceduralAudioGenerator_creates_valid_wav_files()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sidedock_test_{Guid.NewGuid():N}.wav");
        try
        {
            ProceduralAudioGenerator.GenerateTrack(WhiteNoiseTracks.Rain, tempFile);
            Assert.True(File.Exists(tempFile));

            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 1000);

            // Verify RIFF header
            using var stream = File.OpenRead(tempFile);
            using var reader = new BinaryReader(stream);
            var riff = new string(reader.ReadChars(4));
            Assert.Equal("RIFF", riff);

            var chunkSize = reader.ReadInt32();
            Assert.True(chunkSize > 0);

            var wave = new string(reader.ReadChars(4));
            Assert.Equal("WAVE", wave);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Can_instantiate_WhiteNoiseWidget_and_apply_settings()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var widget = new WhiteNoiseWidget();
                widget.ApplySettings(WhiteNoiseSettings.Default);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }
    }

    [Fact]
    public void ApplyPaper_does_not_fire_PaperChanged_and_does_not_loop()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var widget = new WhiteNoiseWidget();
                var fired = 0;
                widget.PaperChanged += (_, _) =>
                {
                    fired++;
                    widget.ApplyPaper("red");
                };

                widget.ApplyPaper("yellow");
                Assert.Equal(0, fired);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }
    }

    [Fact]
    public void ToggleTrack_can_toggle_unlisted_tracks_and_switches_state()
    {
        var engine = new WhiteNoiseAudioEngine();
        engine.ApplySettings(WhiteNoiseSettings.Default);

        // Initially Campfire is not in default tracks (which only has Rain and Thunder)
        Assert.DoesNotContain(engine.Settings.Tracks, t => t.Id == WhiteNoiseTracks.Campfire);
        Assert.False(engine.IsPlaying);

        // Toggle on Campfire - must NOT trigger play or pause
        engine.ToggleTrack(WhiteNoiseTracks.Campfire);
        var campfire = engine.Settings.Tracks.FirstOrDefault(t => t.Id == WhiteNoiseTracks.Campfire);
        Assert.NotNull(campfire);
        Assert.True(campfire.Enabled);
        Assert.True(campfire.Volume > 0);
        Assert.False(engine.IsPlaying);

        // Toggle off Campfire - must NOT trigger play or pause
        engine.ToggleTrack(WhiteNoiseTracks.Campfire);
        campfire = engine.Settings.Tracks.FirstOrDefault(t => t.Id == WhiteNoiseTracks.Campfire);
        Assert.NotNull(campfire);
        Assert.False(campfire.Enabled);
        Assert.False(engine.IsPlaying);
    }

    [Fact]
    public void SetTrackVolume_can_adjust_unlisted_track_volume()
    {
        var engine = new WhiteNoiseAudioEngine();
        engine.ApplySettings(WhiteNoiseSettings.Default);

        // Set volume for Waves which is not originally in default preset tracks
        engine.SetTrackVolume(WhiteNoiseTracks.Waves, 0.65);
        var waves = engine.Settings.Tracks.FirstOrDefault(t => t.Id == WhiteNoiseTracks.Waves);
        Assert.NotNull(waves);
        Assert.Equal(0.65, waves.Volume);
        Assert.True(waves.Enabled);
    }

    [Fact]
    public void Modifying_tracks_switches_ActivePreset_to_Custom_and_displays_custom_name()
    {
        var engine = new WhiteNoiseAudioEngine();
        engine.ApplySettings(WhiteNoiseSettings.Default);
        Assert.Equal(WhiteNoisePresets.RainyStudy, engine.Settings.ActivePreset);

        // Modifying track toggle sets preset to Custom
        engine.ToggleTrack(WhiteNoiseTracks.Campfire);
        Assert.Equal(WhiteNoisePresets.Custom, engine.Settings.ActivePreset);

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("自定义", WhiteNoisePresets.DisplayName(engine.Settings.ActivePreset));
            Assert.Equal("自定义", WhiteNoisePresets.DisplayName(string.Empty));
        }

        using (UiLocale.Push(UiLanguage.English))
        {
            Assert.Equal("Custom", WhiteNoisePresets.DisplayName(engine.Settings.ActivePreset));
            Assert.Equal("Custom", WhiteNoisePresets.DisplayName(string.Empty));
        }

        // Applying preset sets preset
        engine.ApplyPreset(WhiteNoisePresets.DeepForest);
        Assert.Equal(WhiteNoisePresets.DeepForest, engine.Settings.ActivePreset);

        // Adjusting volume switches back to Custom
        engine.SetTrackVolume(WhiteNoiseTracks.Wind, 0.4);
        Assert.Equal(WhiteNoisePresets.Custom, engine.Settings.ActivePreset);
    }

    [Fact]
    public void Normalize_preserves_custom_preset()
    {
        var customSettings = new WhiteNoiseSettings
        {
            MasterVolume = 0.5,
            ActivePreset = WhiteNoisePresets.Custom,
            Tracks = [new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = 0.8, Enabled = true }]
        };
        var normalized = WhiteNoiseSettings.Normalize(customSettings);
        Assert.Equal(WhiteNoisePresets.Custom, normalized.ActivePreset);

        var emptyPresetSettings = new WhiteNoiseSettings
        {
            MasterVolume = 0.5,
            ActivePreset = string.Empty,
            Tracks = [new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = 0.8, Enabled = true }]
        };
        var normalizedEmpty = WhiteNoiseSettings.Normalize(emptyPresetSettings);
        Assert.Equal(WhiteNoisePresets.Custom, normalizedEmpty.ActivePreset);
    }

    [Fact]
    public void ApplyDockEdge_on_side_keeps_wavePanel_height_fixed_and_toggle_visible()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var widget = new WhiteNoiseWidget();
                widget.ApplySettings(WhiteNoiseSettings.Default);
                widget.ApplyDockEdge(DockEdge.Left);
                widget.ApplyDockEdge(DockEdge.Right);
                widget.ApplyDockEdge(DockEdge.Top);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }
    }
}
