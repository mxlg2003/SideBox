using WinDock.Core;
using WinDock.Core.Permissions;
using WinDock.Infrastructure;
using Xunit;

namespace WinDock.Core.Tests;

public class SettingsEditorTests
{
    [Fact]
    public void Widget_visibility_round_trip_keeps_general()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var hidden = SettingsEditor.WithWidgetVisible(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Clock, false);
        hidden = SettingsEditor.WithDockEdge(hidden, DefaultProfiles.GeneralId, DockEdge.Left);
        var normalized = SettingsValidator.EnsureInvariants(hidden);

        Assert.Contains(normalized.Profiles, p => p.Id == DefaultProfiles.GeneralId);
        var general = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.False(SettingsEditor.IsWidgetVisible(general, BuiltInWidgets.Clock));
        Assert.Equal(DockEdge.Left, general.Dock.Edge);
        Assert.DoesNotContain(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Clock);
    }

    [Fact]
    public void Factory_general_only_pins_clock_and_notes()
    {
        var general = DefaultProfiles.CreateGeneral();
        Assert.Equal(
            new[] { BuiltInWidgets.Clock, BuiltInWidgets.Notes },
            general.Dock.Items.Select(item => item.WidgetId).ToArray());
        Assert.True(general.Dock.AutoHide);
    }

    [Fact]
    public void EnsureInvariants_does_not_reseed_network_from_visible_placement()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var stuffed = general with
        {
            Widgets = general.Widgets.Append(BuiltInWidgets.DefaultPlacement(BuiltInWidgets.Network)).ToArray()
        };
        var next = SettingsValidator.EnsureInvariants(settings with
        {
            Profiles = settings.Profiles.Select(profile =>
                profile.Id == DefaultProfiles.GeneralId ? stuffed : profile).ToArray()
        });
        var items = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.DoesNotContain(items, item => item.WidgetId == BuiltInWidgets.Network);
    }

    [Fact]
    public void Removed_network_chip_is_not_seeded_back()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Network);
        var itemId = ItemId(settings, BuiltInWidgets.Network);
        var removed = SettingsEditor.RemoveDockItem(settings, DefaultProfiles.GeneralId, itemId);
        var next = SettingsValidator.EnsureInvariants(removed);
        var general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Network);
    }

    [Fact]
    public void Enabling_media_privacy_can_grant_profile_permission()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        Assert.DoesNotContain(settings.Profiles.First().Permissions, p => p == PermissionIds.MediaRead);

        var granted = SettingsEditor.GrantPermission(settings, PermissionIds.MediaRead, DefaultProfiles.GeneralId);
        Assert.Contains(PermissionIds.MediaRead, granted.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Permissions);
        Assert.DoesNotContain(granted.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Permissions, p => p == PermissionIds.MediaRead);
    }

    [Fact]
    public void Enabling_widget_adds_chip_and_placement()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var updated = SettingsEditor.WithWidgetVisible(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Notes, true);
        var general = updated.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.True(SettingsEditor.IsWidgetVisible(general, BuiltInWidgets.Notes));
        Assert.Contains(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Notes);
    }

    [Fact]
    public void Adding_widget_again_appends_another_chip()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var once = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Clock);
        var twice = SettingsEditor.AddWidget(once, DefaultProfiles.GeneralId, BuiltInWidgets.Clock);
        var clocks = twice.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Where(item => item.WidgetId == BuiltInWidgets.Clock)
            .ToArray();
        Assert.Equal(3, clocks.Length);
        Assert.Equal(clocks.Length, clocks.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(SettingsEditor.IsWidgetVisible(twice.Profiles.First(p => p.Id == DefaultProfiles.GeneralId), BuiltInWidgets.Clock));
    }

    [Fact]
    public void Removing_one_chip_leaves_other_copies()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Clock);
        var clocks = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Where(item => item.WidgetId == BuiltInWidgets.Clock)
            .ToArray();
        Assert.Equal(2, clocks.Length);

        var next = SettingsEditor.RemoveDockItem(settings, DefaultProfiles.GeneralId, clocks[0].Id);
        var general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Single(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Clock);
        Assert.Equal(clocks[1].Id, general.Dock.Items.Single(item => item.WidgetId == BuiltInWidgets.Clock).Id);
        Assert.True(SettingsEditor.IsWidgetVisible(general, BuiltInWidgets.Clock));
    }

    [Fact]
    public void Density_applies_to_one_instance()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Clock);
        var clocks = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Where(item => item.WidgetId == BuiltInWidgets.Clock)
            .ToArray();
        var compact = SettingsEditor.WithWidgetDensity(settings, DefaultProfiles.GeneralId, clocks[0].Id, WidgetDensity.Compact);
        var items = compact.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Where(item => item.WidgetId == BuiltInWidgets.Clock)
            .ToArray();
        Assert.Equal(WidgetDensity.Compact, items.First(item => item.Id == clocks[0].Id).Density);
        Assert.Equal(WidgetDensity.Expanded, items.First(item => item.Id == clocks[1].Id).Density);
    }

    [Fact]
    public void Island_length_round_trips_and_clears()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var sized = SettingsEditor.WithIslandLength(settings, DefaultProfiles.GeneralId, 640);
        var general = sized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Equal(640, general.Dock.IslandLength);

        var cleared = SettingsEditor.WithIslandLength(sized, DefaultProfiles.GeneralId, null);
        Assert.Null(cleared.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.IslandLength);
    }

    [Fact]
    public void Dock_items_can_be_reordered()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var first = general.Dock.Items[0].WidgetId;
        var second = general.Dock.Items[1].WidgetId;
        var moved = SettingsEditor.MoveDockItem(settings, DefaultProfiles.GeneralId, 0, 1);
        var items = moved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.Equal(second, items[0].WidgetId);
        Assert.Equal(first, items[1].WidgetId);
    }

    [Fact]
    public void Widget_density_round_trips()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var clockId = ItemId(settings, BuiltInWidgets.Clock);
        var expanded = SettingsEditor.WithWidgetDensity(settings, DefaultProfiles.GeneralId, clockId, WidgetDensity.Expanded);
        var clock = expanded.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == clockId);
        Assert.Equal(WidgetDensity.Expanded, clock.Density);
        var compact = SettingsEditor.WithWidgetDensity(expanded, DefaultProfiles.GeneralId, clockId, WidgetDensity.Compact);
        Assert.Equal(WidgetDensity.Compact, compact.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == clockId).Density);
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Notes));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Clock));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Stopwatch));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Countdown));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.FocusTimer));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Power));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.TimeProgress));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.SysActivity));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Network));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Clock));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.TimeProgress));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Network));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.BingWallpaper));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.SysActivity));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Notes));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Stopwatch));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Countdown));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.FocusTimer));
        Assert.False(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Power));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.Notes));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.FocusTimer));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.Power));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.TimeProgress));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.SysActivity));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.Network));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.Clock));
        Assert.False(BuiltInWidgets.CanCustomize(BuiltInWidgets.Stopwatch));
        Assert.False(BuiltInWidgets.CanCustomize(BuiltInWidgets.Countdown));
    }

    [Fact]
    public void Notes_paper_color_round_trips_and_normalizes()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var visible = SettingsEditor.WithWidgetVisible(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Notes, true);
        var notesId = ItemId(visible, BuiltInWidgets.Notes);
        var blue = SettingsEditor.WithWidgetPaperColor(visible, DefaultProfiles.GeneralId, notesId, "blue");
        Assert.Equal(NotePapers.Blue, blue.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == notesId).PaperColor);
        var fallback = SettingsEditor.WithWidgetPaperColor(blue, DefaultProfiles.GeneralId, notesId, "nope");
        Assert.Equal(NotePapers.Default, fallback.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == notesId).PaperColor);
        Assert.Equal(NotePapers.Yellow, NotePapers.Normalize(null));
    }

    [Fact]
    public void Retired_focus_widget_is_dropped()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var withFocus = settings with
        {
            Profiles = settings.Profiles.Select(profile =>
                profile.Id != DefaultProfiles.GeneralId
                    ? profile
                    : profile with
                    {
                        Widgets = profile.Widgets.Append(new WidgetPlacement { Id = "focus", Visible = true }).ToArray(),
                        Dock = profile.Dock with
                        {
                            Items = profile.Dock.Items.Append(new DockItem
                            {
                                Type = DockItemType.WidgetChip,
                                WidgetId = "focus",
                                Title = "专注"
                            }).ToArray()
                        }
                    }).ToArray()
        };

        var normalized = SettingsValidator.EnsureInvariants(withFocus);
        var next = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(next.Widgets, widget => widget.Id == "focus");
        Assert.DoesNotContain(next.Dock.Items, item => item.WidgetId == "focus");
        Assert.Equal(general.Dock.Items.Count, next.Dock.Items.Count);
    }

    [Fact]
    public void Retired_battery_widget_becomes_power_or_is_dropped()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var withoutPower = general with
        {
            Widgets = general.Widgets.Where(widget => widget.Id != BuiltInWidgets.Power).ToArray(),
            Dock = general.Dock with
            {
                Items = general.Dock.Items
                    .Where(item => item.WidgetId != BuiltInWidgets.Power)
                    .Append(new DockItem
                    {
                        Type = DockItemType.WidgetChip,
                        WidgetId = "battery",
                        Title = "电池"
                    })
                    .ToArray()
            }
        };
        var withBattery = settings with
        {
            Profiles = settings.Profiles.Select(profile =>
                profile.Id != DefaultProfiles.GeneralId ? profile : withoutPower).ToArray()
        };

        var normalized = SettingsValidator.EnsureInvariants(withBattery);
        var next = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(next.Widgets, widget => widget.Id == "battery");
        Assert.DoesNotContain(next.Dock.Items, item => item.WidgetId == "battery");
        Assert.Contains(next.Dock.Items, item => item.WidgetId == BuiltInWidgets.Power);
    }

    [Fact]
    public void Focus_timer_settings_round_trip_and_normalize()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var added = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.FocusTimer);
        var itemId = ItemId(added, BuiltInWidgets.FocusTimer);
        var saved = SettingsEditor.WithFocusTimer(added, DefaultProfiles.GeneralId, itemId, new FocusTimerSettings
        {
            Color = "teal",
            FocusMinutes = 45,
            ShortBreakMinutes = 8,
            LongBreakMinutes = 20,
            LongBreakEvery = 3,
            NotifyWhenFinished = false
        });
        var timer = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).FocusTimer;
        Assert.Equal(FocusTimerColors.Teal, timer?.Color);
        Assert.Equal(45, timer?.FocusMinutes);
        Assert.False(timer?.NotifyWhenFinished);

        var clamped = SettingsEditor.WithFocusTimer(saved, DefaultProfiles.GeneralId, itemId, new FocusTimerSettings
        {
            Color = "nope",
            FocusMinutes = 0,
            LongBreakEvery = 99
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).FocusTimer;
        Assert.Equal(FocusTimerColors.Default, next?.Color);
        Assert.Equal(1, next?.FocusMinutes);
        Assert.Equal(12, next?.LongBreakEvery);
    }

    [Fact]
    public void Countdown_settings_round_trip_and_normalize()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var added = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Countdown);
        var itemId = ItemId(added, BuiltInWidgets.Countdown);
        var saved = SettingsEditor.WithCountdown(added, DefaultProfiles.GeneralId, itemId, new CountdownSettings
        {
            Hours = 1,
            Minutes = 8,
            Seconds = 30,
            Name = "2222",
            Presets =
            [
                new CountdownPreset { Name = "d111", Minutes = 5 },
                new CountdownPreset { Name = "2222", Minutes = 8 }
            ]
        });
        var countdown = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Countdown;
        Assert.Equal(1, countdown?.Hours);
        Assert.Equal(8, countdown?.Minutes);
        Assert.Equal("2222", countdown?.Name);
        Assert.Equal(2, countdown?.Presets.Count);

        var clamped = SettingsEditor.WithCountdown(saved, DefaultProfiles.GeneralId, itemId, new CountdownSettings
        {
            Hours = 200,
            Minutes = 90,
            Seconds = 90,
            Name = new string('x', 80),
            Presets = [new CountdownPreset { Name = " ", Minutes = 3 }]
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Countdown;
        Assert.Equal(99, next?.Hours);
        Assert.Equal(59, next?.Minutes);
        Assert.Equal(59, next?.Seconds);
        Assert.Equal(40, next?.Name.Length);
        Assert.Empty(next?.Presets ?? []);
    }

    [Fact]
    public void Power_widget_settings_round_trip_and_normalize()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var added = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Power);
        var itemId = ItemId(added, BuiltInWidgets.Power);
        var saved = SettingsEditor.WithPowerWidget(added, DefaultProfiles.GeneralId, itemId, new PowerWidgetSettings
        {
            Layout = PowerLayouts.IconOnly,
            HiddenDeviceIds = ["case", "case", ""]
        });
        var power = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Power;
        Assert.Equal(PowerLayouts.IconOnly, power?.Layout);
        Assert.Equal(["case"], power?.HiddenDeviceIds);

        var clamped = SettingsEditor.WithPowerWidget(saved, DefaultProfiles.GeneralId, itemId, new PowerWidgetSettings
        {
            Layout = "nope"
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Power;
        Assert.Equal(PowerLayouts.Status, next?.Layout);
        Assert.Empty(next?.HiddenDeviceIds ?? ["x"]);
    }

    [Fact]
    public void Time_progress_settings_round_trip_and_normalize()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var added = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.TimeProgress);
        var itemId = ItemId(added, BuiltInWidgets.TimeProgress);
        var saved = SettingsEditor.WithTimeProgress(added, DefaultProfiles.GeneralId, itemId, new TimeProgressSettings
        {
            Period = TimeProgressPeriods.Day,
            Layout = TimeProgressLayouts.Ring
        });
        var progress = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).TimeProgress;
        Assert.Equal(TimeProgressPeriods.Day, progress?.Period);
        Assert.Equal(TimeProgressLayouts.Ring, progress?.Layout);

        var clamped = SettingsEditor.WithTimeProgress(saved, DefaultProfiles.GeneralId, itemId, new TimeProgressSettings
        {
            Period = "week",
            Layout = "wave"
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).TimeProgress;
        Assert.Equal(TimeProgressPeriods.Year, next?.Period);
        Assert.Equal(TimeProgressLayouts.Bars, next?.Layout);
    }

    [Fact]
    public void Sys_activity_settings_round_trip_and_normalize()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.SysActivity);
        var itemId = ItemId(settings, BuiltInWidgets.SysActivity);
        var saved = SettingsEditor.WithSysActivity(settings, DefaultProfiles.GeneralId, itemId, new SysActivitySettings
        {
            Layout = SysActivityLayouts.Bars
        });
        var activity = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).SysActivity;
        Assert.Equal(SysActivityLayouts.Bars, activity?.Layout);

        var clamped = SettingsEditor.WithSysActivity(saved, DefaultProfiles.GeneralId, itemId, new SysActivitySettings
        {
            Layout = "wave"
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).SysActivity;
        Assert.Equal(SysActivityLayouts.Numbers, next?.Layout);
    }

    [Fact]
    public void Network_widget_settings_round_trip_and_normalize()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Network);
        var itemId = ItemId(settings, BuiltInWidgets.Network);
        var saved = SettingsEditor.WithNetworkWidget(settings, DefaultProfiles.GeneralId, itemId, new NetworkWidgetSettings
        {
            Show = NetworkShows.Upload,
            ChartInDock = false,
            ChartInPopup = false
        });
        var network = saved.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Network;
        Assert.Equal(NetworkShows.Upload, network?.Show);
        Assert.False(network?.ChartInDock);
        Assert.False(network?.ChartInPopup);

        var clamped = SettingsEditor.WithNetworkWidget(saved, DefaultProfiles.GeneralId, itemId, new NetworkWidgetSettings
        {
            Show = "wifi"
        });
        var next = clamped.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.Id == itemId).Network;
        Assert.Equal(NetworkShows.Both, next?.Show);
        Assert.True(next?.ChartInDock);
        Assert.True(next?.ChartInPopup);
    }

    [Fact]
    public void Retired_cpu_and_memory_widgets_become_one_sysactivity()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var withoutSys = general with
        {
            Widgets = general.Widgets.Where(widget => widget.Id != BuiltInWidgets.SysActivity).ToArray(),
            Dock = general.Dock with
            {
                Items = general.Dock.Items
                    .Where(item => item.WidgetId != BuiltInWidgets.SysActivity)
                    .Concat(
                    [
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = "cpu",
                            Title = "CPU"
                        },
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = "memory",
                            Title = "内存"
                        }
                    ])
                    .ToArray()
            }
        };
        var legacy = settings with
        {
            Profiles = settings.Profiles.Select(profile =>
                profile.Id != DefaultProfiles.GeneralId ? profile : withoutSys).ToArray()
        };

        var normalized = SettingsValidator.EnsureInvariants(legacy);
        var next = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(next.Widgets, widget => widget.Id is "cpu" or "memory");
        Assert.DoesNotContain(next.Dock.Items, item => item.WidgetId is "cpu" or "memory");
        Assert.Single(next.Dock.Items, item => item.WidgetId == BuiltInWidgets.SysActivity);
    }

    [Fact]
    public void Backdrop_round_trips()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        Assert.Equal(DockBackdrop.Mica, settings.Ui.Backdrop);

        var glass = SettingsEditor.WithBackdrop(settings, DockBackdrop.Glass);
        Assert.Equal(DockBackdrop.Glass, glass.Ui.Backdrop);
        Assert.Equal(DockTokens.GlassIslandTransparency, glass.Ui.IslandTransparency);
        Assert.Equal(settings.Ui.Theme, glass.Ui.Theme);

        var frosted = SettingsEditor.WithBackdrop(glass, DockBackdrop.Frosted);
        Assert.Equal(DockBackdrop.Frosted, frosted.Ui.Backdrop);
        Assert.Equal(DockTokens.FrostedIslandTransparency, frosted.Ui.IslandTransparency);

        var mica = SettingsEditor.WithBackdrop(frosted, DockBackdrop.Mica);
        Assert.Equal(DockBackdrop.Mica, mica.Ui.Backdrop);
        Assert.Equal(DockTokens.DefaultIslandTransparency, mica.Ui.IslandTransparency);
    }

    [Fact]
    public void Island_transparency_clamps_to_percent()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        Assert.Equal(DockTokens.DefaultIslandTransparency, settings.Ui.IslandTransparency);
        Assert.Equal(0, SettingsEditor.WithIslandTransparency(settings, -20).Ui.IslandTransparency);
        Assert.Equal(100, SettingsEditor.WithIslandTransparency(settings, 140).Ui.IslandTransparency);
        Assert.Equal(35, SettingsEditor.WithIslandTransparency(settings, 35).Ui.IslandTransparency);
    }

    [Fact]
    public void Glass_and_frosted_island_fills_are_more_transparent_than_mica()
    {
        Assert.True(DockTokens.EffectiveTransparency(DockBackdrop.Mica, 80)
                    < DockTokens.EffectiveTransparency(DockBackdrop.Frosted, 80));
        Assert.True(DockTokens.EffectiveTransparency(DockBackdrop.Frosted, 80)
                    < DockTokens.EffectiveTransparency(DockBackdrop.Glass, 80));
        Assert.True((DockTokens.ResolveIslandFill(true, true, DockBackdrop.Glass, 80) >> 24)
                    < (DockTokens.ResolveIslandFill(true, true, DockBackdrop.Mica, 80) >> 24));
        Assert.Equal(DockTokens.Light.TileGlass, DockTokens.ResolveTileFill(true, DockBackdrop.Glass));
    }

    [Fact]
    public void Menu_fill_is_opaque()
    {
        Assert.Equal(255u, DockTokens.ResolveMenuFill(true, true, DockBackdrop.Glass) >> 24);
        Assert.Equal(255u, DockTokens.ResolveMenuFill(false, true, DockBackdrop.Frosted) >> 24);
        Assert.Equal(255u, DockTokens.ResolveMenuFill(true, true, DockBackdrop.Mica) >> 24);
    }

    [Fact]
    public void Bottom_edge_is_rejected()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        Assert.Throws<InvalidOperationException>(() =>
            SettingsEditor.WithDockEdge(settings, DefaultProfiles.GeneralId, DockEdge.Bottom));
    }

    [Fact]
    public void General_profile_cannot_be_removed()
    {
        var settings = DefaultProfiles.CreateFactorySettings() with
        {
            Profiles = [DefaultProfiles.CreateDeepWork()]
        };

        var normalized = SettingsValidator.EnsureInvariants(settings);
        Assert.Contains(normalized.Profiles, p => p.Id == DefaultProfiles.GeneralId);
    }

    private static string ItemId(AppSettings settings, string widgetId) =>
        settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == widgetId).Id;
}

public class HiddenNotesTests
{
    [Fact]
    public void Hide_notes_keeps_id_off_the_island()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var first = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Notes);
        var second = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Last(item => item.WidgetId == BuiltInWidgets.Notes).Id;

        var hiddenOnce = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, first);
        var hiddenBoth = SettingsEditor.HideDockItem(hiddenOnce, DefaultProfiles.GeneralId, second);
        var general = hiddenBoth.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Notes);
        Assert.Equal(new[] { first, second }, SettingsEditor.HiddenNotes(general).Select(item => item.Id));

        var added = SettingsEditor.AddWidget(hiddenBoth, DefaultProfiles.GeneralId, BuiltInWidgets.Notes);
        general = added.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var visible = general.Dock.Items.Where(item => item.WidgetId == BuiltInWidgets.Notes).ToArray();
        Assert.Single(visible);
        Assert.DoesNotContain(new[] { first, second }, id => id == visible[0].Id);
        Assert.Equal(new[] { first, second }, SettingsEditor.HiddenNotes(general).Select(item => item.Id));

        var shown = SettingsEditor.ShowHiddenWidget(added, DefaultProfiles.GeneralId, first);
        general = shown.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Contains(general.Dock.Items, item => item.Id == first);
        Assert.DoesNotContain(SettingsEditor.HiddenNotes(general), item => item.Id == first);
        Assert.Contains(SettingsEditor.LiveNoteKeys(shown), key => key == first);
        Assert.Contains(SettingsEditor.LiveNoteKeys(shown), key => key == second);
        Assert.Contains(SettingsEditor.LiveNoteKeys(shown), key => key == visible[0].Id);
    }

    [Fact]
    public void Removing_an_empty_notes_chip_does_not_stash_it()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var id = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        var removed = SettingsEditor.RemoveDockItem(settings, DefaultProfiles.GeneralId, id);
        var general = removed.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(general.Dock.Items, item => item.Id == id);
        Assert.Empty(SettingsEditor.HiddenNotes(general));
    }

    [Fact]
    public void Hide_clock_does_not_enter_hidden_notes()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var clockId = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Clock).Id;
        var next = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, clockId);
        var general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.DoesNotContain(general.Dock.Items, item => item.Id == clockId);
        Assert.Empty(SettingsEditor.HiddenNotes(general));
    }

    [Fact]
    public void Hidden_notes_stay_on_their_profile()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Notes);
        var id = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        var hidden = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, id);
        var deep = hidden.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId);
        Assert.DoesNotContain(SettingsEditor.HiddenNotes(deep), item => item.Id == id);
        Assert.Contains(deep.Dock.Items, item => item.WidgetId == BuiltInWidgets.Notes);
    }

    [Fact]
    public void Discard_hidden_note_drops_the_id()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Notes);
        var id = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        var hidden = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, id);
        var discarded = SettingsEditor.DiscardHiddenWidget(hidden, DefaultProfiles.GeneralId, id);
        var general = discarded.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Empty(SettingsEditor.HiddenNotes(general));
        Assert.DoesNotContain(SettingsEditor.LiveNoteKeys(discarded), key => key == id);
    }

    [Fact]
    public void EnsureInvariants_keeps_hidden_notes()
    {
        var settings = SettingsEditor.AddWidget(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.GeneralId,
            BuiltInWidgets.Notes);
        var id = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        var hidden = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, id);
        var next = SettingsValidator.EnsureInvariants(hidden);
        var general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Equal(id, Assert.Single(SettingsEditor.HiddenNotes(general)).Id);
    }

    [Fact]
    public void Replace_empty_current_drops_it_and_keeps_slot()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var hiddenId = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        settings = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, hiddenId);
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Notes);
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var current = general.Dock.Items.Single(item => item.WidgetId == BuiltInWidgets.Notes);
        var at = general.Dock.Items.ToList().FindIndex(item => item.Id == current.Id);

        var next = SettingsEditor.ReplaceDockItemWithHidden(
            settings,
            DefaultProfiles.GeneralId,
            current.Id,
            hiddenId,
            stashCurrent: false);
        general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Equal(hiddenId, general.Dock.Items[at].Id);
        Assert.DoesNotContain(general.Dock.Items, item => item.Id == current.Id);
        Assert.DoesNotContain(SettingsEditor.HiddenNotes(general), item => item.Id == current.Id);
        Assert.DoesNotContain(SettingsEditor.HiddenNotes(general), item => item.Id == hiddenId);
    }

    [Fact]
    public void Replace_filled_current_stashes_it_into_history()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var hiddenId = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
        settings = SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, hiddenId);
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Notes);
        var currentId = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
            .Single(item => item.WidgetId == BuiltInWidgets.Notes).Id;

        var next = SettingsEditor.ReplaceDockItemWithHidden(
            settings,
            DefaultProfiles.GeneralId,
            currentId,
            hiddenId,
            stashCurrent: true);
        var general = next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Contains(general.Dock.Items, item => item.Id == hiddenId);
        Assert.DoesNotContain(general.Dock.Items, item => item.Id == currentId);
        Assert.Equal(currentId, Assert.Single(SettingsEditor.HiddenNotes(general)).Id);
    }
}

public class ByteRateTests
{
    [Fact]
    public void Formats_bytes_kb_and_mb()
    {
        Assert.Equal("512 B/s", ByteRate.Format(512));
        Assert.Equal("1.5 KB/s", ByteRate.Format(1536));
        Assert.Equal("2.00 MB/s", ByteRate.Format(2 * 1024 * 1024));
        Assert.Equal(("1.5", "KB/s"), ByteRate.Split(1536));
        Assert.Equal(("512", "B/s"), ByteRate.Split(512));
        Assert.Equal(("12.3", "KB/s"), ByteRate.Split(12.3 * 1024));
        Assert.Equal(("2.00", "MB/s"), ByteRate.Split(2 * 1024 * 1024));
    }
}

public class PerfHistoryTests
{
    [Fact]
    public void Unavailable_cpu_is_marked_and_does_not_throw()
    {
        var previous = new PerfSnapshot { Timestamp = DateTimeOffset.UnixEpoch };
        var next = PerfHistory.Push(previous, DateTimeOffset.UtcNow, cpu: null, memory: 12);
        Assert.False(next.CpuAvailable);
        Assert.Equal(0, next.CpuPercent);
        Assert.True(next.MemoryAvailable);
        Assert.Equal(12, next.MemoryPercent);
        Assert.Empty(next.CpuHistory);
        Assert.Single(next.MemoryHistory);
    }

    [Fact]
    public void History_is_capped()
    {
        var snapshot = new PerfSnapshot { Timestamp = DateTimeOffset.UnixEpoch };
        for (var i = 0; i < 40; i++)
        {
            snapshot = PerfHistory.Push(snapshot, DateTimeOffset.UtcNow, i, 0);
        }

        Assert.Equal(PerfHistory.Length, snapshot.CpuHistory.Count);
        Assert.Equal(39, snapshot.CpuHistory[^1]);
    }
}

public class FileNotesStoreTests
{
    [Fact]
    public void Round_trips_text_per_key()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        var store = new FileNotesStore(dir);
        Assert.Equal(string.Empty, store.Load("a"));
        store.Save("a", "hello");
        store.Save("b", "other");
        Assert.Equal("hello", store.Load("a"));
        Assert.Equal("other", store.Load("b"));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Save_blank_deletes_the_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileNotesStore(dir);
            store.Save("a", "hello");
            store.Save("a", "  \n\t");
            Assert.Equal(string.Empty, store.Load("a"));
            Assert.Empty(Directory.Exists(dir) ? Directory.GetFiles(dir, "*.txt") : []);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Sweep_keeps_live_keys_including_hidden()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileNotesStore(dir);
            store.Save("visible", "on island");
            store.Save("hidden", "in drawer");
            store.Save("orphan", "gone");
            store.Sweep(["visible", "hidden"]);
            Assert.Equal("on island", store.Load("visible"));
            Assert.Equal("in drawer", store.Load("hidden"));
            Assert.Equal(string.Empty, store.Load("orphan"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Legacy_notes_file_goes_to_the_first_key_only()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var legacy = Path.Combine(root, "notes.txt");
            File.WriteAllText(legacy, "old");
            var store = new FileNotesStore(Path.Combine(root, "notes"), legacy);
            store.TryAdoptLegacy("first");
            store.TryAdoptLegacy("second");
            Assert.Equal("old", store.Load("first"));
            Assert.Equal(string.Empty, store.Load("second"));
            Assert.False(File.Exists(legacy));
            Assert.True(File.Exists(legacy + ".bak"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Legacy_is_retired_even_when_the_key_already_has_a_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var notesDir = Path.Combine(root, "notes");
            Directory.CreateDirectory(notesDir);
            File.WriteAllText(Path.Combine(notesDir, "first.txt"), "mine");
            var legacy = Path.Combine(root, "notes.txt");
            File.WriteAllText(legacy, "old");
            var store = new FileNotesStore(notesDir, legacy);
            store.TryAdoptLegacy("first");
            Assert.Equal("mine", store.Load("first"));
            Assert.False(File.Exists(legacy));
            store.TryAdoptLegacy("second");
            Assert.Equal(string.Empty, store.Load("second"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class MemoryNotesStoreTests
{
    [Fact]
    public void Sweep_drops_keys_not_in_the_live_set()
    {
        var store = new WinDock.Providers.MemoryNotesStore();
        store.Save("a", "keep");
        store.Save("b", "drop");
        store.Save("b", "  ");
        Assert.Equal(string.Empty, store.Load("b"));
        store.Save("b", "drop");
        store.Sweep(["a"]);
        Assert.Equal("keep", store.Load("a"));
        Assert.Equal(string.Empty, store.Load("b"));
    }
}

public class DockPinTests
{
    [Fact]
    public void Infers_app_folder_and_file()
    {
        var exe = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo");
        Assert.Equal(DockItemType.App, exe.Type);
        Assert.Equal("Demo", exe.Title);
        Assert.EndsWith(@"app.exe", exe.Path, StringComparison.OrdinalIgnoreCase);

        var folderPath = Path.Combine(Path.GetTempPath(), "WinDock-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folderPath);
        try
        {
            var folder = DockPins.FromTarget(folderPath);
            Assert.Equal(DockItemType.Folder, folder.Type);
            Assert.Equal(new DirectoryInfo(folderPath).Name, folder.Title);
        }
        finally
        {
            Directory.Delete(folderPath);
        }

        Assert.Throws<InvalidOperationException>(() => DockPins.FromTarget("https://example.com/app"));

        var file = DockPins.FromTarget(@"C:\Notes\todo.txt");
        Assert.Equal(DockItemType.File, file.Type);
        Assert.Equal("todo", file.Title);
    }

    [Fact]
    public void AddPin_appends_inserts_and_dedupes()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var exe = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo");
        var appended = SettingsEditor.AddPin(settings, DefaultProfiles.GeneralId, exe);
        var general = appended.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Equal(settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items.Count + 1, general.Dock.Items.Count);
        Assert.Equal(DockItemType.App, general.Dock.Items[^1].Type);
        Assert.Equal("Demo", general.Dock.Items[^1].Title);

        var twice = SettingsEditor.AddPin(appended, DefaultProfiles.GeneralId, DockPins.FromTarget(@"C:\Apps\Demo\app.exe"));
        Assert.Equal(general.Dock.Items.Count, twice.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items.Count);

        var other = DockPins.FromTarget(@"C:\Notes\todo.txt");
        var inserted = SettingsEditor.AddPin(appended, DefaultProfiles.GeneralId, other, 0);
        var items = inserted.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.Equal(DockItemType.File, items[0].Type);
        Assert.EndsWith(@"todo.txt", items[0].Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddPins_reports_skipped_duplicates()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var pin = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo");
        var first = SettingsEditor.AddPins(settings, DefaultProfiles.GeneralId, [pin]);
        Assert.Equal(1, first.Added);
        Assert.Equal(0, first.Skipped);
        var second = SettingsEditor.AddPins(first.Settings, DefaultProfiles.GeneralId, [DockPins.FromTarget(@"C:\Apps\Demo\app.exe")]);
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Skipped);
    }

    [Fact]
    public void Duplicate_profile_gets_new_ids_and_general_cannot_be_deleted()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var result = SettingsEditor.DuplicateProfile(settings, DefaultProfiles.GeneralId);
        var copy = result.Settings.Profiles.Last();
        Assert.NotEqual(DefaultProfiles.GeneralId, copy.Id);
        Assert.Empty(copy.Triggers);
        Assert.Contains("的副本", copy.Name);
        var sourceItems = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items.Select(item => item.Id);
        Assert.Empty(sourceItems.Intersect(copy.Dock.Items.Select(item => item.Id)));

        var renamed = SettingsEditor.RenameProfile(result.Settings, copy.Id, "书房");
        Assert.Equal("书房", renamed.Profiles.First(p => p.Id == copy.Id).Name);

        var refused = SettingsEditor.RemoveProfile(renamed, DefaultProfiles.GeneralId);
        Assert.Contains(refused.Profiles, p => p.Id == DefaultProfiles.GeneralId);

        var switched = SettingsEditor.SwitchProfile(renamed, copy.Id, null, "");
        var removed = SettingsEditor.RemoveProfile(switched, copy.Id);
        Assert.DoesNotContain(removed.Profiles, p => p.Id == copy.Id);
        Assert.Equal(DefaultProfiles.GeneralId, removed.ActiveProfileId);
        Assert.Equal(DefaultProfiles.GeneralId, removed.ManualOverrideProfileId);
    }

    [Fact]
    public void Pin_without_path_is_dropped()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var broken = general with
        {
            Dock = general.Dock with
            {
                Items = general.Dock.Items.Append(new DockItem { Type = DockItemType.App }).ToArray()
            }
        };
        var next = settings with
        {
            Profiles = settings.Profiles.Select(profile => profile.Id == general.Id ? broken : profile).ToArray()
        };
        var normalized = SettingsValidator.EnsureInvariants(next);
        var items = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.DoesNotContain(items, item => item.Type == DockItemType.App && string.IsNullOrWhiteSpace(item.Path));
    }

    [Fact]
    public void Url_and_separator_items_are_dropped()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var mixed = general with
        {
            Dock = general.Dock with
            {
                Items = general.Dock.Items
                    .Append(new DockItem { Type = DockItemType.Url, Url = "https://example.com/" })
                    .Append(new DockItem { Type = DockItemType.Separator })
                    .ToArray()
            }
        };
        var next = settings with
        {
            Profiles = settings.Profiles.Select(profile => profile.Id == general.Id ? mixed : profile).ToArray()
        };
        var normalized = SettingsValidator.EnsureInvariants(next);
        var items = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.DoesNotContain(items, item => item.Type is DockItemType.Url or DockItemType.Separator);
    }

    [Fact]
    public void Session_override_clears_on_load_and_auto_hide_round_trips()
    {
        var settings = DefaultProfiles.CreateFactorySettings() with
        {
            ManualOverride = new ManualOverrideSettings { Until = ManualOverrideUntil.Session },
            ManualOverrideProfileId = DefaultProfiles.DeepWorkId,
            ManualOverrideFocusActive = true,
            ManualOverrideTopologyKey = "1|x"
        };
        var cleared = SettingsEditor.ClearSessionOverride(settings);
        Assert.Null(cleared.ManualOverrideProfileId);
        Assert.Equal(ManualOverrideUntil.Session, cleared.ManualOverride.Until);

        var hidden = SettingsEditor.WithAutoHide(settings, DefaultProfiles.DeepWorkId, false);
        Assert.False(hidden.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Dock.AutoHide);
        var recorded = SettingsEditor.RecordSwitch(settings, DefaultProfiles.GeneralId, DefaultProfiles.DeepWorkId, TriggerReason.Manual);
        Assert.Equal(DefaultProfiles.GeneralId, recorded.LastProfileId);
        Assert.Equal(DefaultProfiles.GeneralId, SettingsEditor.UndoTarget(recorded, DefaultProfiles.DeepWorkId));
        Assert.Null(SettingsEditor.UndoTarget(DefaultProfiles.CreateFactorySettings(), DefaultProfiles.GeneralId));
        Assert.Equal(ManualOverrideUntil.UntilManual, DefaultProfiles.CreateFactorySettings().ManualOverride.Until);
    }

    [Fact]
    public void Peek_and_reveal_hotkey_round_trip()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.True(general.Dock.PeekOnPointer);
        Assert.False(general.Dock.RevealHotkey);
        Assert.Equal("Win+Shift+A", settings.Hotkeys.RevealDock);

        var peeked = SettingsEditor.WithPeekOnPointer(settings, DefaultProfiles.GeneralId, false);
        Assert.False(peeked.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.PeekOnPointer);
        Assert.True(peeked.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Dock.PeekOnPointer);

        var revealed = SettingsEditor.WithRevealHotkey(settings, DefaultProfiles.GeneralId, true);
        Assert.True(revealed.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.RevealHotkey);
        Assert.False(revealed.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Dock.RevealHotkey);

        var collapsed = SettingsEditor.WithCollapseApps(settings, DefaultProfiles.GeneralId, true);
        collapsed = SettingsEditor.WithCollapseFolders(collapsed, DefaultProfiles.GeneralId, true);
        Assert.True(collapsed.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.CollapseApps);
        Assert.True(collapsed.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.CollapseFolders);
        Assert.False(collapsed.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Dock.CollapseApps);
        Assert.False(collapsed.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Dock.CollapseFolders);

        var chord = SettingsEditor.WithRevealDockHotkey(settings, "Ctrl+Alt+F1");
        Assert.Equal("Ctrl+Alt+F1", chord.Hotkeys.RevealDock);
        Assert.Equal("Win+Shift+D", chord.Hotkeys.NextProfile);
    }

    [Fact]
    public void Revoking_media_permission_clears_all_profiles()
    {
        var settings = SettingsEditor.GrantPermission(
            DefaultProfiles.CreateFactorySettings(),
            PermissionIds.MediaRead,
            DefaultProfiles.GeneralId);
        settings = SettingsEditor.GrantPermission(settings, PermissionIds.MediaRead, DefaultProfiles.DeepWorkId);
        var revoked = SettingsEditor.RevokePermission(settings, PermissionIds.MediaRead);
        Assert.DoesNotContain(
            revoked.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Permissions,
            p => p == PermissionIds.MediaRead);
        Assert.DoesNotContain(
            revoked.Profiles.First(p => p.Id == DefaultProfiles.DeepWorkId).Permissions,
            p => p == PermissionIds.MediaRead);
    }

    [Fact]
    public void Switch_history_summarizes_display_names()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var record = new ProfileSwitchRecord
        {
            Timestamp = DateTimeOffset.Parse("2026-09-16T12:00:00+08:00"),
            FromProfileId = DefaultProfiles.GeneralId,
            ToProfileId = DefaultProfiles.DeepWorkId,
            Reason = TriggerReason.Manual,
            Result = ProfileSwitchResult.Succeeded
        };
        var line = ProfileSwitchLog.Summarize(record, settings.Profiles);
        Assert.Contains("日常", line, StringComparison.Ordinal);
        Assert.Contains("深度工作", line, StringComparison.Ordinal);
        Assert.DoesNotContain("general", line, StringComparison.Ordinal);

        var failed = record with
        {
            Result = ProfileSwitchResult.Failed,
            ErrorMessage = "目标情景不存在"
        };
        var failedLine = ProfileSwitchLog.Summarize(failed, settings.Profiles);
        Assert.Contains("失败", failedLine, StringComparison.Ordinal);
        Assert.Contains("目标情景不存在", failedLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Monitor_labels_name_primary_and_others()
    {
        var primary = new MonitorInfo
        {
            Id = "1",
            DevicePath = @"\\.\DISPLAY1",
            IsPrimary = true,
            Bounds = new RectLogical(0, 0, 2560, 1440),
            WorkArea = new RectLogical(0, 0, 2560, 1400),
            DpiScale = 1
        };
        var second = primary with
        {
            Id = "2",
            DevicePath = @"\\.\DISPLAY2",
            IsPrimary = false,
            Bounds = new RectLogical(2560, 0, 1920, 1080),
            WorkArea = new RectLogical(2560, 0, 1920, 1040)
        };
        Assert.Equal("始终跟着 Windows 主屏", MonitorLabels.FollowPrimary);
        Assert.Equal("主屏 · 2560×1440", MonitorLabels.For(primary, 1));
        Assert.Equal("屏幕 2 · 1920×1080", MonitorLabels.For(second, 2));
    }

    [Fact]
    public void Folder_badge_uses_trimmed_title_or_question_mark()
    {
        Assert.Equal("下载", DockPins.FolderBadge("下载"));
        Assert.Equal("项目文档", DockPins.FolderBadge("项目文档"));
        Assert.Equal("Documents", DockPins.FolderBadge("Documents"));
        Assert.Equal("A", DockPins.FolderBadge("A"));
        Assert.Equal("?", DockPins.FolderBadge("  "));
        Assert.Equal("?", DockPins.FolderBadge(null));
    }

    [Fact]
    public void Running_dot_follows_app_executable_path()
    {
        var app = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo");
        var other = DockPins.FromTarget(@"C:\Apps\Other\other.exe", "Other");
        var folder = new DockItem { Id = "folder", Type = DockItemType.Folder, Path = @"C:\Apps" };
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DockPins.NormalizePath(@"C:\Apps\Demo\app.exe")
        };

        var active = PinRunning.ActiveIds([app, other, folder], running);
        Assert.Contains(app.Id, active);
        Assert.DoesNotContain(other.Id, active);
        Assert.DoesNotContain("folder", active);
        Assert.True(PinRunning.Tracks(app));
        Assert.False(PinRunning.Tracks(folder));
    }

    [Fact]
    public void Running_dot_matches_same_exe_under_install_directory()
    {
        var launcher = DockPins.FromTarget(@"C:\Users\me\AppData\Local\Doubao\Application\Doubao.exe", "Doubao");
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DockPins.NormalizePath(@"C:\Users\me\AppData\Local\Doubao\Application\2.0.0\Doubao.exe")
        };

        Assert.Contains(launcher.Id, PinRunning.ActiveIds([launcher], running));
        Assert.True(PinRunning.IsActive(launcher, running));
    }

    [Fact]
    public void Running_dot_ignores_other_exes_in_the_same_tree()
    {
        var launcher = DockPins.FromTarget(@"C:\Users\me\AppData\Local\Doubao\Application\Doubao.exe", "Doubao");
        var sibling = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DockPins.NormalizePath(@"C:\Users\me\AppData\Local\Doubao\Application\helper.exe")
        };
        var elsewhere = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DockPins.NormalizePath(@"C:\Apps\Other\Doubao.exe")
        };

        Assert.False(PinRunning.IsActive(launcher, sibling));
        Assert.False(PinRunning.IsActive(launcher, elsewhere));
    }

    [Fact]
    public void Start_menu_apps_drop_uninstallers_and_duplicate_names()
    {
        Assert.True(StartMenuApps.IsUninstallName("卸载 豆包"));
        Assert.True(StartMenuApps.IsUninstallName("Uninstall Demo"));
        Assert.False(StartMenuApps.IsUninstallName("Doubao"));

        var collected = StartMenuApps.Collect(
        [
            new StartMenuApp { Name = "Doubao", LnkPath = @"C:\User\Doubao.lnk" },
            new StartMenuApp { Name = "Doubao", LnkPath = @"C:\Common\Doubao.lnk" },
            new StartMenuApp { Name = "Uninstall Foo", LnkPath = @"C:\Foo\uninstall.lnk" },
            new StartMenuApp { Name = "卸载 Bar", LnkPath = @"C:\Bar\uninst.lnk" }
        ]);

        Assert.Equal("Doubao", Assert.Single(collected).Name);
        Assert.Equal(@"C:\User\Doubao.lnk", collected[0].LnkPath);
    }
}
