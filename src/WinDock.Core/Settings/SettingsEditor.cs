namespace WinDock.Core;

public static class SettingsEditor
{
    public static AppSettings WithTheme(AppSettings settings, ThemePreference theme) =>
        settings with { Ui = settings.Ui with { Theme = theme } };

    public static AppSettings WithLanguage(AppSettings settings, UiLanguage language) =>
        settings with { Ui = settings.Ui with { Language = language } };

    public static AppSettings WithBackdrop(AppSettings settings, DockBackdrop backdrop) =>
        settings with
        {
            Ui = settings.Ui with
            {
                Backdrop = backdrop,
                IslandTransparency = DockLayoutConstants.TransparencyFor(backdrop)
            }
        };

    public static AppSettings WithIslandTransparency(AppSettings settings, int transparency) =>
        settings with { Ui = settings.Ui with { IslandTransparency = Math.Clamp(transparency, 0, 100) } };

    public static AppSettings WithPrivacy(AppSettings settings, PrivacySettings privacy) =>
        settings with { Privacy = privacy };

    public static AppSettings WithLicense(AppSettings settings, LicenseSettings license) =>
        settings with { License = license };

    public static AppSettings EnsureLicenseDevice(AppSettings settings)
    {
        if (settings.License.DeviceId.Trim().Length >= 8)
        {
            return settings;
        }

        return settings with
        {
            License = settings.License with { DeviceId = "sbx-" + Guid.NewGuid().ToString("N") }
        };
    }

    public static AppSettings GrantPermission(AppSettings settings, string permissionId, string? profileId = null)
    {
        if (string.IsNullOrWhiteSpace(permissionId))
        {
            throw new InvalidOperationException("Permission id is required.");
        }

        return settings with
        {
            Profiles = settings.Profiles.Select(profile =>
            {
                if (profileId is not null && profile.Id != profileId)
                {
                    return profile;
                }

                return profile.Permissions.Contains(permissionId, StringComparer.Ordinal)
                    ? profile
                    : profile with { Permissions = profile.Permissions.Append(permissionId).ToArray() };
            }).ToArray()
        };
    }

    public static AppSettings RevokePermission(AppSettings settings, string permissionId, string? profileId = null)
    {
        if (string.IsNullOrWhiteSpace(permissionId))
        {
            throw new InvalidOperationException("Permission id is required.");
        }

        return settings with
        {
            Profiles = settings.Profiles.Select(profile =>
            {
                if (profileId is not null && profile.Id != profileId)
                {
                    return profile;
                }

                var next = profile.Permissions
                    .Where(permission => !string.Equals(permission, permissionId, StringComparison.Ordinal))
                    .ToArray();
                return next.Length == profile.Permissions.Count ? profile : profile with { Permissions = next };
            }).ToArray()
        };
    }

    public static AppSettings ClearSessionOverride(AppSettings settings)
    {
        if (settings.ManualOverride.Until != ManualOverrideUntil.Session)
        {
            return settings;
        }

        return settings with
        {
            ManualOverrideProfileId = null,
            ManualOverrideFocusActive = null,
            ManualOverrideTopologyKey = null
        };
    }

    public static AppSettings WithAutoHide(AppSettings settings, string profileId, bool autoHide) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { AutoHide = autoHide } });

    public static AppSettings WithPeekOnPointer(AppSettings settings, string profileId, bool peek) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { PeekOnPointer = peek } });

    public static AppSettings WithRevealHotkey(AppSettings settings, string profileId, bool enabled) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { RevealHotkey = enabled } });

    public static AppSettings WithCollapseApps(AppSettings settings, string profileId, bool collapse) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { CollapseApps = collapse } });

    public static AppSettings WithCollapseFolders(AppSettings settings, string profileId, bool collapse) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { CollapseFolders = collapse } });

    public static AppSettings ReplaceDockItems(AppSettings settings, string profileId, IReadOnlyList<DockItem> items) =>
        MapProfile(settings, profileId, profile => profile with { Dock = profile.Dock with { Items = items } });

    /// <summary>
    /// Puts shielded chips back at their saved indexes after a reorder of the visible dock.
    /// </summary>
    public static IReadOnlyList<DockItem> RestoreShielded(IReadOnlyList<DockItem> stored, IReadOnlyList<DockItem> visible)
    {
        var held = new List<(int Index, DockItem Item)>();
        for (var i = 0; i < stored.Count; i++)
        {
            var item = stored[i];
            if (item.Type == DockItemType.WidgetChip && BuiltInWidgets.IsShielded(item.WidgetId))
            {
                held.Add((i, item));
            }
        }

        if (held.Count == 0)
        {
            return visible;
        }

        var present = visible.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var result = visible.ToList();
        foreach (var (index, item) in held)
        {
            if (present.Contains(item.Id))
            {
                continue;
            }

            result.Insert(Math.Min(index, result.Count), item);
        }

        return result;
    }

    /// <summary>
    /// Maps an insert index on the visible dock onto the saved list, which may still hold shielded chips.
    /// </summary>
    public static int MapInsertIndex(IReadOnlyList<DockItem> stored, IReadOnlyList<DockItem> visible, int visibleIndex)
    {
        if (visibleIndex <= 0)
        {
            return 0;
        }

        if (visibleIndex >= visible.Count)
        {
            return stored.Count;
        }

        var anchorId = visible[visibleIndex].Id;
        for (var i = 0; i < stored.Count; i++)
        {
            if (string.Equals(stored[i].Id, anchorId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return stored.Count;
    }

    public static AppSettings WithRevealDockHotkey(AppSettings settings, string? chord) =>
        settings with { Hotkeys = settings.Hotkeys with { RevealDock = chord } };

    public static AppSettings WithManualOverrideUntil(AppSettings settings, ManualOverrideUntil until) =>
        settings with { ManualOverride = settings.ManualOverride with { Until = until } };

    public static AppSettings RecordSwitch(
        AppSettings settings,
        string? fromProfileId,
        string toProfileId,
        TriggerReason reason,
        ProfileSwitchResult result = ProfileSwitchResult.Succeeded,
        string? error = null)
    {
        return settings with
        {
            LastProfileId = fromProfileId ?? settings.LastProfileId,
            SwitchHistory = []
        };
    }

    public static string? UndoTarget(AppSettings settings, string currentProfileId)
    {
        if (!string.IsNullOrWhiteSpace(settings.LastProfileId)
            && settings.LastProfileId != currentProfileId
            && settings.Profiles.Any(profile => profile.Id == settings.LastProfileId))
        {
            return settings.LastProfileId;
        }

        foreach (var record in settings.SwitchHistory.Reverse())
        {
            if (record.Result != ProfileSwitchResult.Succeeded
                || string.IsNullOrWhiteSpace(record.FromProfileId)
                || record.FromProfileId == currentProfileId)
            {
                continue;
            }

            if (settings.Profiles.Any(profile => profile.Id == record.FromProfileId))
            {
                return record.FromProfileId;
            }
        }

        return null;
    }

    public static string NextProfileId(AppSettings settings, string currentProfileId, int delta)
    {
        if (settings.Profiles.Count == 0)
        {
            return DefaultProfiles.GeneralId;
        }

        var index = settings.Profiles.ToList().FindIndex(profile => profile.Id == currentProfileId);
        if (index < 0)
        {
            index = 0;
        }

        var next = (index + delta) % settings.Profiles.Count;
        if (next < 0)
        {
            next += settings.Profiles.Count;
        }

        return settings.Profiles[next].Id;
    }

    public static AppSettings SwitchProfile(
        AppSettings settings,
        string profileId,
        bool? focusActive,
        string topologyKey)
    {
        if (settings.Profiles.All(p => p.Id != profileId))
        {
            throw new InvalidOperationException($"Unknown profile '{profileId}'.");
        }

        return settings with
        {
            LastProfileId = settings.ActiveProfileId,
            ActiveProfileId = profileId,
            ManualOverrideProfileId = profileId,
            ManualOverrideFocusActive = focusActive,
            ManualOverrideTopologyKey = topologyKey
        };
    }

    public static AppSettings WithDockMonitor(AppSettings settings, string profileId, string monitor) =>
        MapProfile(settings, profileId, profile => profile with
        {
            Dock = profile.Dock with
            {
                Monitor = string.IsNullOrWhiteSpace(monitor) ? "primary" : monitor.Trim()
            }
        });

    public static AppSettings RenameProfile(AppSettings settings, string profileId, string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? profileId : name.Trim();
        return MapProfile(settings, profileId, profile => profile with { Name = trimmed });
    }

    public static ProfileDuplicateResult DuplicateProfile(AppSettings settings, string sourceId)
    {
        var source = settings.Profiles.FirstOrDefault(profile => profile.Id == sourceId)
                     ?? throw new InvalidOperationException($"Unknown profile '{sourceId}'.");
        var notes = new List<(string From, string To)>();
        var copy = source with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = source.Name + UiText.CopySuffix,
            Triggers = [],
            Dock = source.Dock with { Items = CloneItems(source.Dock.Items, notes) },
            HiddenWidgets = CloneItems(source.HiddenWidgets ?? [], notes),
            Widgets = source.Widgets.ToArray(),
            Actions = source.Actions.ToArray(),
            Permissions = source.Permissions.ToArray()
        };
        var next = SettingsValidator.EnsureInvariants(settings with
        {
            Profiles = settings.Profiles.Append(copy).ToArray()
        });
        var created = next.Profiles.First(profile => profile.Id == copy.Id);
        return new ProfileDuplicateResult(SwitchProfile(next, created.Id, null, string.Empty), notes);
    }

    public static AppSettings RemoveProfile(AppSettings settings, string profileId)
    {
        if (profileId == DefaultProfiles.GeneralId
            || settings.Profiles.All(profile => profile.Id != profileId))
        {
            return settings;
        }

        var profiles = settings.Profiles.Where(profile => profile.Id != profileId).ToArray();
        var active = settings.ActiveProfileId == profileId
            ? DefaultProfiles.GeneralId
            : settings.ActiveProfileId;
        var manual = settings.ManualOverrideProfileId == profileId
            ? DefaultProfiles.GeneralId
            : settings.ManualOverrideProfileId;
        var last = settings.LastProfileId == profileId ? null : settings.LastProfileId;
        return settings with
        {
            Profiles = profiles,
            ActiveProfileId = active,
            ManualOverrideProfileId = manual,
            LastProfileId = last
        };
    }

    private static IReadOnlyList<DockItem> CloneItems(
        IReadOnlyList<DockItem> items,
        List<(string From, string To)> notes)
    {
        return items.Select(item =>
        {
            var id = Guid.NewGuid().ToString("N");
            if (IsNotesChip(item) && !string.IsNullOrWhiteSpace(item.Id))
            {
                notes.Add((item.Id, id));
            }

            return item with { Id = id };
        }).ToArray();
    }

    public static AppSettings WithDockEdge(AppSettings settings, string profileId, DockEdge edge)
    {
        if (edge == DockEdge.Bottom)
        {
            throw new InvalidOperationException("The primary-bottom edge is not offered in settings.");
        }

        return MapProfile(settings, profileId, profile => profile with
        {
            Dock = profile.Dock with { Edge = edge }
        });
    }

    public static AppSettings WithIslandLength(AppSettings settings, string profileId, double? length)
    {
        var normalized = length is > 0 ? length : null;
        return MapProfile(settings, profileId, profile => profile with
        {
            Dock = profile.Dock with { IslandLength = normalized }
        });
    }

    public static AppSettings AddWidget(AppSettings settings, string profileId, string widgetId) =>
        WithWidgetVisible(settings, profileId, widgetId, true);

    public static AppSettings AddPin(AppSettings settings, string profileId, DockItem pin, int? insertAt = null) =>
        AddPins(settings, profileId, [pin], insertAt).Settings;

    public static AddPinsResult AddPins(
        AppSettings settings,
        string profileId,
        IEnumerable<DockItem> pins,
        int? insertAt = null)
    {
        var added = 0;
        var skipped = 0;
        var next = MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items.ToList();
            var at = insertAt is null ? items.Count : Math.Clamp(insertAt.Value, 0, items.Count);
            foreach (var incoming in pins)
            {
                if (!DockPins.IsPin(incoming.Type))
                {
                    skipped++;
                    continue;
                }

                var pin = NormalizePin(incoming);
                if (items.Any(item => DockPins.SameTarget(item, pin)))
                {
                    skipped++;
                    continue;
                }

                items.Insert(at, pin);
                at++;
                added++;
            }

            return profile with { Dock = profile.Dock with { Items = items } };
        });
        return new AddPinsResult(next, added, skipped);
    }

    public static AppSettings WithClockTimeZone(AppSettings settings, string profileId, string itemId, string? timeZoneId)
    {
        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim();
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { TimeZoneId = zone } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithWidgetVisible(AppSettings settings, string profileId, string widgetId, bool visible)
    {
        if (!BuiltInWidgets.IsKnown(widgetId))
        {
            throw new InvalidOperationException($"Unknown widget '{widgetId}'.");
        }

        if (BuiltInWidgets.IsShielded(widgetId) || (visible && !BuiltInWidgets.CanAdd(widgetId)))
        {
            return settings;
        }

        return MapProfile(settings, profileId, profile =>
        {
            var widgets = profile.Widgets.ToList();
            var index = widgets.FindIndex(w => w.Id == widgetId);
            if (index >= 0)
            {
                widgets[index] = widgets[index] with { Visible = visible };
            }
            else if (visible)
            {
                widgets.Add(BuiltInWidgets.DefaultPlacement(widgetId) with { Visible = true });
            }

            var items = visible
                ? AppendChip(profile.Dock.Items, widgetId)
                : RemoveChips(profile.Dock.Items, widgetId);
            return profile with
            {
                Widgets = widgets,
                Dock = profile.Dock with { Items = items }
            };
        });
    }

    public static AppSettings RemoveDockItem(AppSettings settings, string profileId, string itemId)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var taken = TakeDockItem(profile, itemId);
            return taken is null ? profile : taken.Value.Profile;
        });
    }

    public static AppSettings HideDockItem(AppSettings settings, string profileId, string itemId)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var taken = TakeDockItem(profile, itemId);
            if (taken is null)
            {
                return profile;
            }

            var (next, removed) = taken.Value;
            if (!IsNotesChip(removed))
            {
                return next;
            }

            var hidden = HiddenOf(next).ToList();
            if (hidden.All(item => item.Id != removed.Id))
            {
                hidden.Add(removed);
            }

            return next with { HiddenWidgets = hidden };
        });
    }

    public static AppSettings ShowHiddenWidget(AppSettings settings, string profileId, string itemId)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var shown = HiddenOf(profile).FirstOrDefault(item => item.Id == itemId);
            if (shown is null)
            {
                return profile;
            }

            var hidden = HiddenOf(profile).Where(item => item.Id != itemId).ToArray();
            var items = profile.Dock.Items.ToList();
            if (items.All(item => item.Id != shown.Id))
            {
                items.Add(shown);
            }

            return profile with
            {
                Widgets = RevealWidget(profile.Widgets, shown.WidgetId ?? BuiltInWidgets.Notes),
                HiddenWidgets = hidden,
                Dock = profile.Dock with { Items = items }
            };
        });
    }

    public static AppSettings ReplaceDockItemWithHidden(
        AppSettings settings,
        string profileId,
        string currentId,
        string hiddenId,
        bool stashCurrent)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var at = profile.Dock.Items.ToList().FindIndex(item => item.Id == currentId);
            var current = at < 0 ? null : profile.Dock.Items[at];
            var incoming = HiddenOf(profile).FirstOrDefault(item => item.Id == hiddenId);
            if (current is null || incoming is null || current.Id == hiddenId)
            {
                return profile;
            }

            var items = profile.Dock.Items.Where(item => item.Id != currentId).ToList();
            var hidden = HiddenOf(profile).Where(item => item.Id != hiddenId).ToList();
            if (stashCurrent && IsNotesChip(current) && hidden.All(item => item.Id != current.Id))
            {
                hidden.Add(current);
            }

            var insertAt = Math.Clamp(at, 0, items.Count);
            if (items.All(item => item.Id != incoming.Id))
            {
                items.Insert(insertAt, incoming);
            }

            return profile with
            {
                Widgets = RevealWidget(profile.Widgets, incoming.WidgetId ?? BuiltInWidgets.Notes),
                HiddenWidgets = hidden,
                Dock = profile.Dock with { Items = items }
            };
        });
    }

    public static AppSettings DiscardHiddenWidget(AppSettings settings, string profileId, string itemId) =>
        MapProfile(settings, profileId, profile =>
            profile with { HiddenWidgets = HiddenOf(profile).Where(item => item.Id != itemId).ToArray() });

    public static IReadOnlyList<string> LiveNoteKeys(AppSettings settings) =>
        settings.Profiles.SelectMany(profile =>
                profile.Dock.Items.Concat(HiddenOf(profile)).Where(IsNotesChip).Select(item => item.Id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<DockItem> HiddenNotes(Profile profile) =>
        HiddenOf(profile).Where(IsNotesChip).ToArray();

    public static AppSettings WithWidgetDensity(AppSettings settings, string profileId, string itemId, WidgetDensity density)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Density = density } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithFocusTimer(AppSettings settings, string profileId, string itemId, FocusTimerSettings? timer)
    {
        var next = FocusTimerSettings.Normalize(timer);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { FocusTimer = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithCountdown(AppSettings settings, string profileId, string itemId, CountdownSettings? countdown)
    {
        var next = CountdownSettings.Normalize(countdown);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Countdown = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithAlarm(AppSettings settings, string profileId, string itemId, AlarmSettings? alarm)
    {
        var next = AlarmSettings.Normalize(alarm);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Alarm = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithPowerWidget(AppSettings settings, string profileId, string itemId, PowerWidgetSettings? power)
    {
        var next = PowerWidgetSettings.Normalize(power);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Power = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithTimeProgress(AppSettings settings, string profileId, string itemId, TimeProgressSettings? progress)
    {
        var next = TimeProgressSettings.Normalize(progress);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { TimeProgress = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithSysActivity(AppSettings settings, string profileId, string itemId, SysActivitySettings? activity)
    {
        var next = SysActivitySettings.Normalize(activity);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { SysActivity = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithNetworkWidget(AppSettings settings, string profileId, string itemId, NetworkWidgetSettings? network)
    {
        var next = NetworkWidgetSettings.Normalize(network);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Network = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithDiskWidget(AppSettings settings, string profileId, string itemId, DiskWidgetSettings? disk)
    {
        var next = DiskWidgetSettings.Normalize(disk);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Disk = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithGpuWidget(AppSettings settings, string profileId, string itemId, GpuWidgetSettings? gpu)
    {
        var next = GpuWidgetSettings.Normalize(gpu);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Gpu = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithProcessWidget(AppSettings settings, string profileId, string itemId, ProcessWidgetSettings? processes)
    {
        var next = ProcessWidgetSettings.Normalize(processes);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Processes = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithStockWidget(AppSettings settings, string profileId, string itemId, StockWidgetSettings? stocks)
    {
        var next = StockWidgetSettings.Normalize(stocks);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Stocks = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithWhiteNoise(AppSettings settings, string profileId, string itemId, WhiteNoiseSettings? whiteNoise)
    {
        var next = WhiteNoiseSettings.Normalize(whiteNoise);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { WhiteNoise = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithWidgetPaperColor(AppSettings settings, string profileId, string itemId, string? paperColor)
    {
        var paper = NotePapers.Normalize(paperColor);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { PaperColor = paper } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithWeather(AppSettings settings, string profileId, string itemId, WeatherSettings? weather)
    {
        var next = WeatherSettings.Normalize(weather);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Weather = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithSchedule(AppSettings settings, string profileId, string itemId, ScheduleSettings? schedule)
    {
        var next = ScheduleSettings.Normalize(schedule);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Schedule = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithBingWallpaper(AppSettings settings, string profileId, string itemId, BingWallpaperSettings? bing)
    {
        var next = BingWallpaperSettings.Normalize(bing);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { BingWallpaper = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithWallpaper(AppSettings settings, string profileId, string itemId, WallpaperSettings? wallpaper)
    {
        var next = WallpaperSettings.Normalize(wallpaper);
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.Id == itemId ? item with { Wallpaper = next } : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings WithBingWallpaperAutoApply(AppSettings settings, bool autoApply)
    {
        return MapProfile(settings, settings.ActiveProfileId, profile =>
        {
            var items = profile.Dock.Items
                .Select(item => item.WidgetId == BuiltInWidgets.BingWallpaper
                    ? item with { BingWallpaper = (item.BingWallpaper ?? BingWallpaperSettings.Default) with { AutoApplyDaily = autoApply } }
                    : item)
                .ToArray();
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static AppSettings MoveDockItem(AppSettings settings, string profileId, int from, int to)
    {
        return MapProfile(settings, profileId, profile =>
        {
            var items = profile.Dock.Items.ToList();
            if (from < 0 || from >= items.Count || to < 0 || to >= items.Count || from == to)
            {
                return profile;
            }

            var item = items[from];
            items.RemoveAt(from);
            items.Insert(to, item);
            return profile with { Dock = profile.Dock with { Items = items } };
        });
    }

    public static bool IsWidgetVisible(Profile profile, string widgetId) =>
        profile.Widgets.Any(w => w.Id == widgetId && w.Visible);

    public static bool HasWidgetChip(IEnumerable<DockItem> items, string widgetId) =>
        items.Any(item =>
            item.Type == DockItemType.WidgetChip
            && string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal));

    public static bool IsNotesChip(DockItem item) =>
        item.Type == DockItemType.WidgetChip
        && string.Equals(item.WidgetId, BuiltInWidgets.Notes, StringComparison.Ordinal);

    private static IReadOnlyList<DockItem> HiddenOf(Profile profile) =>
        profile.HiddenWidgets ?? [];

    private static IReadOnlyList<WidgetPlacement> RevealWidget(IReadOnlyList<WidgetPlacement> widgets, string widgetId)
    {
        var list = widgets.ToList();
        var index = list.FindIndex(w => w.Id == widgetId);
        if (index >= 0)
        {
            list[index] = list[index] with { Visible = true };
        }
        else
        {
            list.Add(BuiltInWidgets.DefaultPlacement(widgetId) with { Visible = true });
        }

        return list;
    }

    private static (Profile Profile, DockItem Removed)? TakeDockItem(Profile profile, string itemId)
    {
        var removed = profile.Dock.Items.FirstOrDefault(item => item.Id == itemId);
        if (removed is null)
        {
            return null;
        }

        var items = profile.Dock.Items.Where(item => item.Id != itemId).ToArray();
        var widgets = profile.Widgets.ToList();
        if (removed.Type == DockItemType.WidgetChip
            && !string.IsNullOrWhiteSpace(removed.WidgetId)
            && items.All(item => !string.Equals(item.WidgetId, removed.WidgetId, StringComparison.Ordinal)))
        {
            var index = widgets.FindIndex(w => w.Id == removed.WidgetId);
            if (index >= 0)
            {
                widgets[index] = widgets[index] with { Visible = false };
            }
        }

        return (profile with
        {
            Widgets = widgets,
            Dock = profile.Dock with { Items = items }
        }, removed);
    }

    private static DockItem NormalizePin(DockItem pin)
    {
        if (!DockPins.IsPin(pin.Type))
        {
            throw new InvalidOperationException($"Item type '{pin.Type}' is not a pin.");
        }

        var path = DockPins.NormalizePath(pin.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"A {pin.Type} pin needs a path.");
        }

        return pin with
        {
            Id = string.IsNullOrWhiteSpace(pin.Id) ? Guid.NewGuid().ToString("N") : pin.Id,
            Path = path,
            Url = null,
            Title = string.IsNullOrWhiteSpace(pin.Title) ? path : pin.Title,
            ShortcutPath = string.IsNullOrWhiteSpace(pin.ShortcutPath) ? null : DockPins.NormalizePath(pin.ShortcutPath)
        };
    }

    private static IReadOnlyList<DockItem> AppendChip(IReadOnlyList<DockItem> items, string widgetId)
    {
        if (BuiltInWidgets.IsSingleton(widgetId) && HasWidgetChip(items, widgetId))
        {
            return items;
        }

        var list = items.ToList();
        list.Add(new DockItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DockItemType.WidgetChip,
            WidgetId = widgetId,
            Title = BuiltInWidgets.DisplayName(widgetId)
        });
        return list;
    }

    private static IReadOnlyList<DockItem> RemoveChips(IReadOnlyList<DockItem> items, string widgetId) =>
        items.Where(item =>
            item.Type != DockItemType.WidgetChip
            || !string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal)).ToArray();

    private static AppSettings MapProfile(AppSettings settings, string profileId, Func<Profile, Profile> map)
    {
        if (settings.Profiles.All(p => p.Id != profileId))
        {
            throw new InvalidOperationException($"Unknown profile '{profileId}'.");
        }

        return settings with
        {
            Profiles = settings.Profiles.Select(p => p.Id == profileId ? map(p) : p).ToArray()
        };
    }
}

public sealed record ProfileDuplicateResult(
    AppSettings Settings,
    IReadOnlyList<(string From, string To)> NotesKeys);

public sealed record AddPinsResult(AppSettings Settings, int Added, int Skipped);

public static class PerfHistory
{
    public const int Length = 30;

    public static PerfSnapshot Push(
        PerfSnapshot previous,
        DateTimeOffset timestamp,
        double? cpu,
        double? memory,
        ulong memoryUsedBytes = 0,
        ulong memoryTotalBytes = 0,
        double? frequencyMhz = null,
        IReadOnlyList<double>? cores = null,
        ulong commitUsedBytes = 0,
        ulong commitLimitBytes = 0,
        bool commitAvailable = false)
    {
        return new PerfSnapshot
        {
            Timestamp = timestamp,
            CpuPercent = cpu ?? 0,
            CpuAvailable = cpu is not null,
            MemoryPercent = memory ?? 0,
            MemoryAvailable = memory is not null,
            CpuHistory = cpu is null ? previous.CpuHistory : Push(previous.CpuHistory, cpu.Value),
            MemoryHistory = memory is null ? previous.MemoryHistory : Push(previous.MemoryHistory, memory.Value),
            MemoryUsedBytes = memory is null ? previous.MemoryUsedBytes : memoryUsedBytes,
            MemoryTotalBytes = memory is null ? previous.MemoryTotalBytes : memoryTotalBytes,
            FrequencyMhz = frequencyMhz ?? 0,
            FrequencyAvailable = frequencyMhz is not null,
            CorePercents = cores ?? [],
            CommitUsedBytes = commitAvailable ? commitUsedBytes : 0,
            CommitLimitBytes = commitAvailable ? commitLimitBytes : 0,
            CommitAvailable = commitAvailable
        };
    }

    public static IReadOnlyList<double> Push(IReadOnlyList<double> history, double value, int length = Length)
    {
        var list = history.ToList();
        list.Add(value);
        if (list.Count > length)
        {
            list.RemoveRange(0, list.Count - length);
        }

        return list;
    }
}

public static class TriggerReasonText
{
    public static string Format(TriggerReason reason) => reason switch
    {
        TriggerReason.Manual => UiText.ReasonManual,
        TriggerReason.Focus => UiText.ReasonFocus,
        TriggerReason.DisplayTopology => UiText.ReasonDisplay,
        TriggerReason.Schedule => UiText.ReasonSchedule,
        TriggerReason.Fallback => UiText.ReasonFallback,
        TriggerReason.SafeMode => UiText.ReasonSafeMode,
        _ => reason.ToString()
    };

    public static string ToZh(TriggerReason reason) => Format(reason);
}
