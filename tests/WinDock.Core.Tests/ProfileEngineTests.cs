using WinDock.Core;
using WinDock.Core.Permissions;
using Xunit;

namespace WinDock.Core.Tests;

public class ProfileEngineTests
{
    private readonly ProfileEngine _engine = new();

    [Fact]
    public void Factory_settings_include_general_and_deep_work()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        Assert.Contains(settings.Profiles, p => p.Id == DefaultProfiles.GeneralId);
        Assert.Contains(settings.Profiles, p => p.Id == DefaultProfiles.DeepWorkId);
        Assert.Equal(1, settings.SchemaVersion);
    }

    [Fact]
    public void Missing_general_profile_is_reinserted()
    {
        var settings = new AppSettings
        {
            Profiles = [DefaultProfiles.CreateDeepWork()]
        };

        var normalized = SettingsValidator.EnsureInvariants(settings);
        Assert.Contains(normalized.Profiles, p => p.Id == DefaultProfiles.GeneralId);
    }

    [Fact]
    public void Unknown_widget_is_dropped()
    {
        var bad = new AppSettings
        {
            Profiles =
            [
                DefaultProfiles.CreateGeneral() with
                {
                    Widgets = [new WidgetPlacement { Id = "weather", X = 0, Y = 0 }]
                }
            ]
        };

        var normalized = SettingsValidator.EnsureInvariants(bad);
        Assert.DoesNotContain(normalized.Profiles.SelectMany(profile => profile.Widgets), widget => widget.Id == "weather");
    }

    [Fact]
    public void Safe_mode_returns_general_without_widgets_and_forces_overlay()
    {
        var settings = SettingsWithAppBar();
        var diff = _engine.Evaluate(settings, Snapshot(safeMode: true, focusSupported: true, focusActive: true));

        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.SafeMode, diff.Reason);
        Assert.Empty(diff.Widgets);
        Assert.DoesNotContain(diff.Dock.Items, item => item.Type == DockItemType.WidgetChip);
        Assert.Equal(DockMode.Overlay, diff.DockMode);
        Assert.Empty(diff.ActionsToRun);
    }

    [Fact]
    public void Disabled_profiles_stay_on_general_even_when_focus_is_active()
    {
        var settings = DualTriggerSettings();
        var diff = _engine.Evaluate(settings, Snapshot(focusSupported: true, focusActive: true) with
        {
            ProfilesEnabled = false,
            ManualOverrideProfileId = DefaultProfiles.DeepWorkId
        });

        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.Fallback, diff.Reason);
    }

    [Fact]
    public void Auto_triggers_off_ignores_focus_and_keeps_manual()
    {
        var settings = DualTriggerSettings();
        var autoOff = Snapshot(focusSupported: true, focusActive: true, topology: Fixtures.TwoMonitors()) with
        {
            AutoProfileTriggersEnabled = false
        };
        var diff = _engine.Evaluate(settings, autoOff);
        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);

        var manual = SettingsEditor.SwitchProfile(settings, "focus-profile", true, autoOff.Topology.TopologyKey);
        var held = _engine.Evaluate(manual, autoOff with
        {
            ManualOverrideProfileId = "focus-profile",
            ManualOverrideUntil = ManualOverrideUntil.NextFocusOrTopology,
            ManualOverrideFocusActive = false,
            ManualOverrideTopologyKey = "stale"
        });
        Assert.Equal("focus-profile", held.ProfileId);
        Assert.Equal(TriggerReason.Manual, held.Reason);
    }

    [Fact]
    public void Focus_profile_wins_over_topology_when_focus_is_active()
    {
        var settings = DualTriggerSettings();
        var diff = _engine.Evaluate(settings, Snapshot(
            focusSupported: true,
            focusActive: true,
            topology: Fixtures.TwoMonitors()));

        Assert.Equal("focus-profile", diff.ProfileId);
        Assert.Equal(TriggerReason.Focus, diff.Reason);
    }

    [Fact]
    public void Topology_matches_when_focus_is_inactive()
    {
        var settings = DualTriggerSettings();
        var diff = _engine.Evaluate(settings, Snapshot(
            focusSupported: true,
            focusActive: false,
            topology: Fixtures.TwoMonitors()));

        Assert.Equal("dock-profile", diff.ProfileId);
        Assert.Equal(TriggerReason.DisplayTopology, diff.Reason);
    }

    [Fact]
    public void Focus_trigger_never_matches_when_unsupported()
    {
        var settings = DualTriggerSettings();
        var diff = _engine.Evaluate(settings, Snapshot(
            focusSupported: false,
            focusActive: true,
            topology: Fixtures.OneMonitor()));

        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.Fallback, diff.Reason);
    }

    [Fact]
    public void Empty_topology_trigger_does_not_match()
    {
        var settings = DefaultProfiles.CreateFactorySettings() with
        {
            Profiles =
            [
                DefaultProfiles.CreateGeneral(),
                new Profile
                {
                    Id = "empty-topo",
                    Name = "Empty",
                    Triggers = [new ProfileTrigger { Type = TriggerType.DisplayTopology }]
                }
            ]
        };

        var diff = _engine.Evaluate(settings, Snapshot(topology: Fixtures.TwoMonitors()));
        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
    }

    [Fact]
    public void Later_profile_wins_when_priority_is_equal()
    {
        var settings = DefaultProfiles.CreateFactorySettings() with
        {
            Profiles =
            [
                DefaultProfiles.CreateGeneral(),
                ScheduleProfile("morning-a", "08:00", "12:00"),
                ScheduleProfile("morning-b", "08:00", "12:00")
            ]
        };

        var local = new DateTime(2026, 9, 13, 9, 0, 0);
        var diff = _engine.Evaluate(settings, Snapshot(localNow: local));
        Assert.Equal("morning-b", diff.ProfileId);
        Assert.Equal(TriggerReason.Schedule, diff.Reason);
    }

    [Fact]
    public void Overnight_schedule_matches()
    {
        var settings = DefaultProfiles.CreateFactorySettings() with
        {
            Profiles =
            [
                DefaultProfiles.CreateGeneral(),
                ScheduleProfile("night", "22:00", "06:00")
            ]
        };

        var diff = _engine.Evaluate(settings, Snapshot(localNow: new DateTime(2026, 9, 13, 23, 30, 0)));
        Assert.Equal("night", diff.ProfileId);
    }

    [Fact]
    public void Manual_override_until_manual_beats_focus()
    {
        var settings = DualTriggerSettings();
        var snapshot = Snapshot(focusSupported: true, focusActive: true, topology: Fixtures.TwoMonitors()) with
        {
            ManualOverrideProfileId = DefaultProfiles.GeneralId,
            ManualOverrideUntil = ManualOverrideUntil.UntilManual
        };

        var diff = _engine.Evaluate(settings, snapshot);
        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.Manual, diff.Reason);
    }

    [Fact]
    public void Manual_override_clears_when_focus_changes()
    {
        var settings = DualTriggerSettings();
        var snapshot = Snapshot(focusSupported: true, focusActive: true, topology: Fixtures.TwoMonitors()) with
        {
            ManualOverrideProfileId = DefaultProfiles.GeneralId,
            ManualOverrideUntil = ManualOverrideUntil.NextFocusOrTopology,
            ManualOverrideFocusActive = false,
            ManualOverrideTopologyKey = Fixtures.TwoMonitors().TopologyKey
        };

        var diff = _engine.Evaluate(settings, snapshot);
        Assert.Equal("focus-profile", diff.ProfileId);
        Assert.Equal(TriggerReason.Focus, diff.Reason);
    }

    [Fact]
    public void Manual_override_holds_when_focus_and_topology_are_unchanged()
    {
        var topology = Fixtures.TwoMonitors();
        var settings = DualTriggerSettings();
        var snapshot = Snapshot(focusSupported: true, focusActive: true, topology: topology) with
        {
            ManualOverrideProfileId = DefaultProfiles.GeneralId,
            ManualOverrideUntil = ManualOverrideUntil.NextFocusOrTopology,
            ManualOverrideFocusActive = true,
            ManualOverrideTopologyKey = topology.TopologyKey
        };

        var diff = _engine.Evaluate(settings, snapshot);
        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.Manual, diff.Reason);
    }

    [Fact]
    public void Fallback_is_general_when_nothing_matches()
    {
        var diff = _engine.Evaluate(DefaultProfiles.CreateFactorySettings(), Snapshot(focusActive: false));
        Assert.Equal(DefaultProfiles.GeneralId, diff.ProfileId);
        Assert.Equal(TriggerReason.Fallback, diff.Reason);
    }

    [Fact]
    public void SwitchProfile_then_evaluate_is_manual()
    {
        var topology = Fixtures.OneMonitor();
        var settings = SettingsEditor.SwitchProfile(
            DefaultProfiles.CreateFactorySettings(),
            DefaultProfiles.DeepWorkId,
            focusActive: false,
            topology.TopologyKey);

        var snapshot = Snapshot(focusSupported: true, focusActive: false, topology: topology) with
        {
            ManualOverrideProfileId = settings.ManualOverrideProfileId,
            ManualOverrideUntil = settings.ManualOverride.Until,
            ManualOverrideFocusActive = settings.ManualOverrideFocusActive,
            ManualOverrideTopologyKey = settings.ManualOverrideTopologyKey
        };

        var evaluated = _engine.Evaluate(settings, snapshot);
        Assert.Equal(DefaultProfiles.DeepWorkId, evaluated.ProfileId);
        Assert.Equal(TriggerReason.Manual, evaluated.Reason);
    }

    [Fact]
    public void Factory_general_includes_starter_widgets()
    {
        var general = DefaultProfiles.CreateGeneral();
        Assert.Equal(
            new[] { BuiltInWidgets.Clock, BuiltInWidgets.Notes },
            general.Widgets.Select(w => w.Id).ToArray());
        Assert.DoesNotContain(general.Widgets, w => w.Id == "battery");
        Assert.DoesNotContain(general.Widgets, w => w.Id == "cpu");
        Assert.DoesNotContain(general.Widgets, w => w.Id == "memory");
        Assert.DoesNotContain(general.Widgets, w => w.Id == BuiltInWidgets.Power);
        Assert.DoesNotContain(general.Widgets, w => w.Id == BuiltInWidgets.Network);
        Assert.DoesNotContain(general.Widgets, w => w.Id == BuiltInWidgets.NowPlaying);
        Assert.DoesNotContain(general.Widgets, w => w.Id == BuiltInWidgets.BingWallpaper);
        Assert.DoesNotContain(general.Widgets, w => w.Id == BuiltInWidgets.SysActivity);
    }

    [Fact]
    public void Unknown_monitor_is_relocated_to_primary()
    {
        var topology = Fixtures.OneMonitor();
        var resolved = DockLayoutCalculator.ResolveMonitor(topology, @"\\.\DISPLAY9");
        Assert.True(resolved.IsPrimary);
        Assert.Equal(topology.Primary.DevicePath, resolved.DevicePath);
    }

    [Fact]
    public void Top_edge_uses_work_area_width()
    {
        var monitor = Fixtures.OneMonitor().Primary;
        var bounds = DockLayoutCalculator.GetEdgeBounds(monitor, DockEdge.Top, 48);
        Assert.Equal(monitor.WorkArea.Width, bounds.Width);
        Assert.Equal(48, bounds.Height);
        Assert.Equal(monitor.WorkArea.Y, bounds.Y);
    }

    private static AppSettings SettingsWithAppBar()
    {
        var factory = DefaultProfiles.CreateFactorySettings();
        return factory with
        {
            Ui = new UiSettings { DockMode = DockMode.AppBar, Theme = ThemePreference.Dark }
        };
    }

    private static AppSettings DualTriggerSettings()
    {
        return DefaultProfiles.CreateFactorySettings() with
        {
            Profiles =
            [
                DefaultProfiles.CreateGeneral(),
                new Profile
                {
                    Id = "focus-profile",
                    Name = "Focus",
                    Triggers = [new ProfileTrigger { Type = TriggerType.Focus, Active = true }]
                },
                new Profile
                {
                    Id = "dock-profile",
                    Name = "Docked",
                    Triggers = [new ProfileTrigger { Type = TriggerType.DisplayTopology, MonitorCount = 2 }]
                }
            ]
        };
    }

    private static Profile ScheduleProfile(string id, string start, string end) => new()
    {
        Id = id,
        Name = id,
        Triggers =
        [
            new ProfileTrigger { Type = TriggerType.Schedule, Start = start, End = end }
        ]
    };

    private static ContextSnapshot Snapshot(
        bool safeMode = false,
        bool focusSupported = true,
        bool focusActive = false,
        DisplayTopologySnapshot? topology = null,
        DateTime? localNow = null)
    {
        var local = localNow ?? new DateTime(2026, 9, 13, 10, 0, 0);
        return new ContextSnapshot
        {
            UtcNow = new DateTimeOffset(local, TimeSpan.FromHours(8)),
            LocalNow = local,
            FocusSupported = focusSupported,
            FocusActive = focusActive,
            Topology = topology ?? Fixtures.OneMonitor(),
            SafeMode = safeMode
        };
    }
}

public class PermissionGateTests
{
    [Fact]
    public void Media_requires_global_and_profile_permission()
    {
        var gate = new PermissionGate(new PrivacySettings { MediaRead = true });
        Assert.False(gate.Allows(PermissionIds.MediaRead));

        gate.SetActiveProfile([PermissionIds.MediaRead]);
        Assert.True(gate.Allows(PermissionIds.MediaRead));
    }

    [Fact]
    public void Media_denied_when_global_privacy_is_off()
    {
        var gate = new PermissionGate(new PrivacySettings { MediaRead = false });
        gate.SetActiveProfile([PermissionIds.MediaRead]);
        Assert.False(gate.Allows(PermissionIds.MediaRead));
    }

    [Fact]
    public void Unknown_permission_is_denied()
    {
        var gate = new PermissionGate(new PrivacySettings { CrashReports = true });
        Assert.False(gate.Allows("registry.write"));
    }

    [Fact]
    public void Gated_media_provider_strips_metadata()
    {
        var gate = new PermissionGate(new PrivacySettings { MediaRead = false });
        var inner = new FakeMediaProvider(new MediaSessionSnapshot
        {
            SourceAppId = "spotify",
            IsPlaying = true,
            Title = "secret",
            Artist = "band",
            ThumbnailPath = "thumb.png",
            CanPlayPause = true
        });
        var gated = new GatedMediaProvider(gate, inner);

        Assert.Null(gated.Current?.Title);
        Assert.Null(gated.Current?.Artist);
        Assert.Null(gated.Current?.ThumbnailPath);
        Assert.Equal("spotify", gated.Current?.SourceAppId);
        Assert.True(gated.Current?.CanPlayPause);
        Assert.False(gated.Current?.UsesFallbackTransport);
    }

    [Fact]
    public void Gated_media_provider_keeps_fallback_transport_flag()
    {
        var gate = new PermissionGate(new PrivacySettings { MediaRead = false });
        var inner = new FakeMediaProvider(new MediaSessionSnapshot
        {
            SourceAppId = KnownLocalPlayers.SourceId("cloudmusic"),
            IsPlaying = true,
            Title = "网易云音乐",
            Artist = "secret",
            UsesFallbackTransport = true,
            CanPlayPause = true
        });
        var gated = new GatedMediaProvider(gate, inner);

        Assert.Null(gated.Current?.Title);
        Assert.Null(gated.Current?.Artist);
        Assert.True(gated.Current?.UsesFallbackTransport);
        Assert.True(gated.Current?.CanPlayPause);
    }

    [Fact]
    public void Gated_network_provider_hides_details()
    {
        var gate = new PermissionGate(new PrivacySettings { NetworkDetails = false });
        var inner = new FakeNetworkProvider(new NetworkSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            IsConnected = true,
            AdapterName = "Ethernet",
            ReceiveBytesPerSecond = 100,
            SendBytesPerSecond = 50
        });
        var gated = new GatedNetworkProvider(gate, inner);
        Assert.True(gated.Current.IsConnected);
        Assert.Null(gated.Current.AdapterName);
        Assert.Equal(0, gated.Current.ReceiveBytesPerSecond);
        Assert.Empty(gated.Current.ReceiveHistory);
        Assert.Empty(gated.Current.SendHistory);
    }

    [Fact]
    public void Gated_network_provider_reveals_details_when_allowed()
    {
        var gate = new PermissionGate(new PrivacySettings { NetworkDetails = true });
        var inner = new FakeNetworkProvider(new NetworkSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            IsConnected = true,
            AdapterName = "Ethernet",
            ReceiveBytesPerSecond = 100
        });
        var gated = new GatedNetworkProvider(gate, inner);
        Assert.Equal("Ethernet", gated.Current.AdapterName);
        Assert.Equal(100, gated.Current.ReceiveBytesPerSecond);
    }
}

public class ZorderPolicyTests
{
    private readonly DefaultZorderPolicy _policy = new();

    [Fact]
    public void Fullscreen_yields_even_in_safe_mode()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            SafeMode = true,
            IsPresentationOrFullscreen = true,
            DockMode = DockMode.AppBar,
            AppBarHealthy = true
        });
        Assert.Equal(DockZorderState.FullscreenYield, state);
    }

    [Fact]
    public void Safe_mode_never_uses_appbar()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            SafeMode = true,
            DockMode = DockMode.AppBar,
            AppBarHealthy = true
        });
        Assert.Equal(DockZorderState.OverlayTopmost, state);
    }

    [Fact]
    public void Auto_hide_idle_hides()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            AutoHide = true,
            IsIdle = true
        });
        Assert.Equal(DockZorderState.AutoHidden, state);
    }

    [Fact]
    public void Hotzone_peeks()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            AutoHide = true,
            IsIdle = true,
            PointerInHotzone = true
        });
        Assert.Equal(DockZorderState.Peeking, state);
    }

    [Fact]
    public void Hotzone_stays_hidden_when_pointer_peek_is_off()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            AutoHide = true,
            IsIdle = true,
            PeekOnPointer = false,
            PointerInHotzone = true
        });
        Assert.Equal(DockZorderState.AutoHidden, state);
    }

    [Fact]
    public void Keep_visible_is_topmost_when_pointer_peek_is_off()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            AutoHide = true,
            IsIdle = true,
            PeekOnPointer = false,
            KeepVisible = true
        });
        Assert.Equal(DockZorderState.OverlayTopmost, state);
    }

    [Fact]
    public void Open_settings_or_flyout_keeps_dock_visible()
    {
        var state = _policy.Evaluate(new DockHostContext
        {
            AutoHide = true,
            IsIdle = true,
            KeepVisible = true
        });
        Assert.Equal(DockZorderState.OverlayTopmost, state);
    }
}

public class BuiltInWidgetsTests
{
    [Fact]
    public void Canonical_ids_are_known()
    {
        Assert.Equal(11, BuiltInWidgets.All.Count);
        Assert.True(BuiltInWidgets.IsKnown("nowplaying"));
        Assert.True(BuiltInWidgets.IsKnown("bing"));
        Assert.True(BuiltInWidgets.IsKnown("stopwatch"));
        Assert.True(BuiltInWidgets.IsKnown("countdown"));
        Assert.True(BuiltInWidgets.IsKnown("focustimer"));
        Assert.True(BuiltInWidgets.IsKnown("power"));
        Assert.True(BuiltInWidgets.IsKnown("timeprogress"));
        Assert.True(BuiltInWidgets.IsKnown("sysactivity"));
        Assert.False(BuiltInWidgets.IsKnown("cpu"));
        Assert.False(BuiltInWidgets.IsKnown("memory"));
        Assert.False(BuiltInWidgets.IsKnown("battery"));
        Assert.False(BuiltInWidgets.IsKnown("focus"));
        Assert.False(BuiltInWidgets.IsKnown("weather"));
        Assert.False(BuiltInWidgets.IsKnown("CLOCK"));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.Clock));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.Stopwatch));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.Countdown));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.FocusTimer));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.Power));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.TimeProgress));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.SysActivity));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.BingWallpaper));
    }

    [Fact]
    public void Catalog_groups_cover_every_built_in_once()
    {
        var ids = WidgetCatalog.Groups.SelectMany(group => group.WidgetIds).ToArray();
        Assert.Equal(BuiltInWidgets.All.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(BuiltInWidgets.All, id => Assert.Contains(id, ids));
        Assert.DoesNotContain(WidgetCatalog.Groups, group => group.WidgetIds.Count == 0);
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Clock, "时"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Stopwatch, "秒表"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Countdown, "倒计时"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Countdown, "预设"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.FocusTimer, "专注计时"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.FocusTimer, "番茄"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Power, "电源"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.TimeProgress, "时间进度"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.SysActivity, "系统状态"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.SysActivity, "cpu"));
        Assert.False(WidgetCatalog.Matches(BuiltInWidgets.Clock, "xyz"));
        Assert.Equal(BuiltInWidgets.SysActivity, WidgetCatalog.VisibleGroups(WidgetCatalog.AllId, "cpu").Single().WidgetIds.Single());
    }
}

internal static class Fixtures
{
    public static DisplayTopologySnapshot OneMonitor(string path = @"\\.\DISPLAY1") => new()
    {
        Monitors =
        [
            new MonitorInfo
            {
                Id = "1",
                DevicePath = path,
                IsPrimary = true,
                Bounds = new RectLogical(0, 0, 1920, 1080),
                WorkArea = new RectLogical(0, 0, 1920, 1040),
                DpiScale = 1
            }
        ]
    };

    public static DisplayTopologySnapshot TwoMonitors() => new()
    {
        Monitors =
        [
            new MonitorInfo
            {
                Id = "1",
                DevicePath = @"\\.\DISPLAY1",
                IsPrimary = true,
                Bounds = new RectLogical(0, 0, 1920, 1080),
                WorkArea = new RectLogical(0, 0, 1920, 1040),
                DpiScale = 1
            },
            new MonitorInfo
            {
                Id = "2",
                DevicePath = @"\\.\DISPLAY2",
                IsPrimary = false,
                Bounds = new RectLogical(1920, 0, 1920, 1080),
                WorkArea = new RectLogical(1920, 0, 1920, 1080),
                DpiScale = 1.5
            }
        ]
    };
}

internal sealed class FakeMediaProvider : IMediaProvider
{
    public FakeMediaProvider(MediaSessionSnapshot current)
    {
        Current = current;
    }

    public MediaSessionSnapshot? Current { get; }

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public bool CaptureMetadata { get; set; }

    public Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class FakeNetworkProvider : INetworkProvider
{
    public FakeNetworkProvider(NetworkSnapshot current)
    {
        Current = current;
    }

    public NetworkSnapshot Current { get; }

    public event EventHandler<NetworkSnapshot>? Updated
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public bool CollectDetails { get; set; }
}
