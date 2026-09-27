using WinDock.Core.Permissions;

namespace WinDock.Core;

public static class DefaultProfiles
{
    public const string GeneralId = "general";
    public const string DeepWorkId = "deep-work";

    public static AppSettings CreateFactorySettings()
    {
        var general = CreateGeneral();
        return SettingsValidator.EnsureInvariants(new AppSettings
        {
            SchemaVersion = SettingsSchema.CurrentVersion,
            ActiveProfileId = GeneralId,
            Profiles = [general, CreateDeepWork()]
        });
    }

    public static Profile CreateGeneral() => new()
    {
        Id = GeneralId,
        Name = UiText.ProfileDaily,
        Triggers = [],
        Dock = new DockLayout
        {
            Edge = DockEdge.Top,
            Monitor = "primary",
            AutoHide = false,
            Items =
            [
                new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.Clock, Title = BuiltInWidgets.DisplayName(BuiltInWidgets.Clock) },
                new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.Notes, Title = BuiltInWidgets.DisplayName(BuiltInWidgets.Notes) }
            ]
        },
        Widgets =
        [
            BuiltInWidgets.DefaultPlacement(BuiltInWidgets.Clock),
            BuiltInWidgets.DefaultPlacement(BuiltInWidgets.Notes)
        ]
    };

    public static Profile CreateDeepWork() => new()
    {
        Id = DeepWorkId,
        Name = UiText.ProfileDeepWork,
        Triggers =
        [
            new ProfileTrigger { Type = TriggerType.Focus, Active = true }
        ],
        Dock = new DockLayout
        {
            Edge = DockEdge.Top,
            Monitor = "primary",
            AutoHide = false,
            Items =
            [
                new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.FocusTimer, Title = BuiltInWidgets.DisplayName(BuiltInWidgets.FocusTimer) },
                new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.Notes, Title = BuiltInWidgets.DisplayName(BuiltInWidgets.Notes) }
            ]
        },
        Widgets =
        [
            BuiltInWidgets.DefaultPlacement(BuiltInWidgets.FocusTimer),
            new WidgetPlacement { Id = BuiltInWidgets.Notes, Monitor = "primary", X = 40, Y = 200, Visible = true }
        ]
    };

    public static string DisplayName(Profile profile)
    {
        if (profile.Id == GeneralId && IsFactoryName(profile.Name, "日常", "Daily"))
        {
            return UiText.ProfileDaily;
        }

        if (profile.Id == DeepWorkId && IsFactoryName(profile.Name, "深度工作", "Deep Work"))
        {
            return UiText.ProfileDeepWork;
        }

        return profile.Name;
    }

    private static bool IsFactoryName(string name, string zh, string en) =>
        string.Equals(name, zh, StringComparison.Ordinal)
        || string.Equals(name, en, StringComparison.Ordinal);
}

public static class SettingsValidator
{
    public static AppSettings EnsureInvariants(AppSettings settings)
    {
        var profiles = settings.Profiles.ToList();
        if (profiles.All(p => p.Id != DefaultProfiles.GeneralId))
        {
            profiles.Insert(0, DefaultProfiles.CreateGeneral());
        }

        DropRetiredWidgets(profiles);
        AssignDockItemIds(profiles);
        RepairProfiles(profiles);

        var active = profiles.Any(p => p.Id == settings.ActiveProfileId)
            ? settings.ActiveProfileId
            : DefaultProfiles.GeneralId;

        return new AppSettings
        {
            SchemaVersion = settings.SchemaVersion == 0 ? SettingsSchema.CurrentVersion : settings.SchemaVersion,
            Ui = settings.Ui,
            Privacy = settings.Privacy,
            Hotkeys = settings.Hotkeys,
            ManualOverride = settings.ManualOverride,
            ActiveProfileId = active,
            LastProfileId = settings.LastProfileId,
            ManualOverrideProfileId = settings.ManualOverrideProfileId,
            ManualOverrideFocusActive = settings.ManualOverrideFocusActive,
            ManualOverrideTopologyKey = settings.ManualOverrideTopologyKey,
            Profiles = profiles,
            SwitchHistory = settings.SwitchHistory
        };
    }

    private static void RepairProfiles(List<Profile> profiles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id;
            if (!seen.Add(id))
            {
                if (id == DefaultProfiles.GeneralId)
                {
                    profiles.RemoveAt(i);
                    i--;
                    continue;
                }

                do
                {
                    id = Guid.NewGuid().ToString("N");
                } while (!seen.Add(id));
            }

            var widgets = profile.Widgets.Where(widget => BuiltInWidgets.IsKnown(widget.Id)).ToArray();
            var items = profile.Dock.Items.Where(IsValidDockItem).ToArray();
            var dockIds = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var hidden = (profile.HiddenWidgets ?? [])
                .Where(item => SettingsEditor.IsNotesChip(item) && IsValidDockItem(item) && dockIds.Add(item.Id))
                .ToArray();
            var permissions = profile.Permissions
                .Where(permission => permission == PermissionIds.MediaRead)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var actions = profile.Actions
                .Where(action => action.Kind is ActionKind.Notify or ActionKind.Launch)
                .ToArray();
            profiles[i] = profile with
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(profile.Name) ? id : profile.Name,
                Widgets = widgets,
                HiddenWidgets = hidden,
                Permissions = permissions,
                Actions = actions,
                Dock = profile.Dock with { Items = items }
            };
        }
    }

    private static bool IsValidDockItem(DockItem item) => item.Type switch
    {
        DockItemType.App or DockItemType.File or DockItemType.Folder => !string.IsNullOrWhiteSpace(item.Path),
        DockItemType.WidgetChip => BuiltInWidgets.IsKnown(item.WidgetId),
        _ => false
    };

    private static void DropRetiredWidgets(List<Profile> profiles)
    {
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var widgets = profile.Widgets
                .Where(widget => widget.Id is not "focus" and not "battery" and not "cpu" and not "memory")
                .ToList();
            var hasPower = profile.Dock.Items.Any(item =>
                item.Type == DockItemType.WidgetChip
                && string.Equals(item.WidgetId, BuiltInWidgets.Power, StringComparison.Ordinal));
            var hasSys = profile.Dock.Items.Any(item =>
                item.Type == DockItemType.WidgetChip
                && string.Equals(item.WidgetId, BuiltInWidgets.SysActivity, StringComparison.Ordinal));
            var items = new List<DockItem>();
            foreach (var item in profile.Dock.Items)
            {
                if (item.Type != DockItemType.WidgetChip)
                {
                    items.Add(item);
                    continue;
                }

                if (string.Equals(item.WidgetId, "focus", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(item.WidgetId, "battery", StringComparison.Ordinal))
                {
                    if (hasPower)
                    {
                        continue;
                    }

                    items.Add(item with
                    {
                        WidgetId = BuiltInWidgets.Power,
                        Title = BuiltInWidgets.DisplayName(BuiltInWidgets.Power)
                    });
                    hasPower = true;
                    if (widgets.All(widget => widget.Id != BuiltInWidgets.Power))
                    {
                        widgets.Add(BuiltInWidgets.DefaultPlacement(BuiltInWidgets.Power) with { Visible = true });
                    }

                    continue;
                }

                if (string.Equals(item.WidgetId, "cpu", StringComparison.Ordinal)
                    || string.Equals(item.WidgetId, "memory", StringComparison.Ordinal))
                {
                    if (hasSys)
                    {
                        continue;
                    }

                    items.Add(item with
                    {
                        WidgetId = BuiltInWidgets.SysActivity,
                        Title = BuiltInWidgets.DisplayName(BuiltInWidgets.SysActivity)
                    });
                    hasSys = true;
                    if (widgets.All(widget => widget.Id != BuiltInWidgets.SysActivity))
                    {
                        widgets.Add(BuiltInWidgets.DefaultPlacement(BuiltInWidgets.SysActivity) with { Visible = true });
                    }

                    continue;
                }

                items.Add(item);
            }

            profiles[i] = profile with
            {
                Widgets = widgets,
                Dock = profile.Dock with { Items = items }
            };
        }
    }

    private static void AssignDockItemIds(List<Profile> profiles)
    {
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var used = new HashSet<string>(StringComparer.Ordinal);
            var items = AssignIds(profile.Dock.Items, used);
            var hidden = AssignIds(profile.HiddenWidgets ?? [], used);
            profiles[i] = profile with
            {
                Dock = profile.Dock with { Items = items },
                HiddenWidgets = hidden
            };
        }
    }

    private static IReadOnlyList<DockItem> AssignIds(IReadOnlyList<DockItem> source, HashSet<string> used) =>
        source.Select(item =>
        {
            var id = item.Id;
            if (string.IsNullOrWhiteSpace(id) || !used.Add(id))
            {
                do
                {
                    id = Guid.NewGuid().ToString("N");
                } while (!used.Add(id));
                return item with { Id = id };
            }

            return item;
        }).ToArray();
}
