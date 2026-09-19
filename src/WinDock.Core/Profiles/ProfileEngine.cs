using WinDock.Core.Permissions;

namespace WinDock.Core;

public static class TriggerMatcher
{
    public static bool Matches(ProfileTrigger trigger, ContextSnapshot snapshot)
    {
        return trigger.Type switch
        {
            TriggerType.Manual => false,
            TriggerType.Focus => MatchesFocus(trigger, snapshot),
            TriggerType.DisplayTopology => MatchesTopology(trigger, snapshot.Topology),
            TriggerType.Schedule => MatchesSchedule(trigger, snapshot.LocalNow),
            _ => false
        };
    }

    public static int PriorityRank(TriggerType type) => type switch
    {
        TriggerType.Manual => 1,
        TriggerType.Focus => 2,
        TriggerType.DisplayTopology => 3,
        TriggerType.Schedule => 4,
        _ => 99
    };

    public static TriggerReason ToReason(TriggerType type) => type switch
    {
        TriggerType.Manual => TriggerReason.Manual,
        TriggerType.Focus => TriggerReason.Focus,
        TriggerType.DisplayTopology => TriggerReason.DisplayTopology,
        TriggerType.Schedule => TriggerReason.Schedule,
        _ => TriggerReason.Fallback
    };

    private static bool MatchesFocus(ProfileTrigger trigger, ContextSnapshot snapshot)
    {
        if (!snapshot.FocusSupported)
        {
            return false;
        }

        return trigger.Active == snapshot.FocusActive;
    }

    private static bool MatchesTopology(ProfileTrigger trigger, DisplayTopologySnapshot topology)
    {
        var hasConstraint = trigger.MonitorCount is not null
                             || !string.IsNullOrWhiteSpace(trigger.PrimaryDevicePath)
                             || trigger.RequireDevicePaths is { Count: > 0 };
        if (!hasConstraint)
        {
            return false;
        }

        if (trigger.MonitorCount is int count && topology.Monitors.Count != count)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(trigger.PrimaryDevicePath)
            && !string.Equals(topology.Primary.DevicePath, trigger.PrimaryDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trigger.RequireDevicePaths is { Count: > 0 } required)
        {
            var online = topology.Monitors
                .Select(m => m.DevicePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (required.Any(path => !online.Contains(path)))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesSchedule(ProfileTrigger trigger, DateTime localNow)
    {
        if (trigger.Days is { Count: > 0 } && !trigger.Days.Contains(localNow.DayOfWeek))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(trigger.Start) || string.IsNullOrWhiteSpace(trigger.End))
        {
            return false;
        }

        if (!TimeOnly.TryParse(trigger.Start, out var start) || !TimeOnly.TryParse(trigger.End, out var end))
        {
            return false;
        }

        var now = TimeOnly.FromDateTime(localNow);
        if (end > start)
        {
            return now >= start && now < end;
        }

        return now >= start || now < end;
    }
}

public interface IProfileEngine
{
    ProfileDiff Evaluate(AppSettings settings, ContextSnapshot snapshot);
}

public sealed class ProfileEngine : IProfileEngine
{
    public ProfileDiff Evaluate(AppSettings settings, ContextSnapshot snapshot)
    {
        var normalized = SettingsValidator.EnsureInvariants(settings);
        var general = normalized.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);

        if (snapshot.SafeMode)
        {
            return ToDiff(normalized, general, TriggerReason.SafeMode, includeWidgets: false);
        }

        if (!snapshot.ProfilesEnabled)
        {
            return ToDiff(normalized, general, TriggerReason.Fallback, includeWidgets: true);
        }

        if (TryManualOverride(normalized, snapshot, out var manual))
        {
            return ToDiff(normalized, manual, TriggerReason.Manual, includeWidgets: true);
        }

        if (snapshot.AutoProfileTriggersEnabled
            && TryAutoMatch(normalized, snapshot, out var matched, out var reason))
        {
            return ToDiff(normalized, matched, reason, includeWidgets: true);
        }

        var active = normalized.Profiles.FirstOrDefault(profile => profile.Id == normalized.ActiveProfileId)
                     ?? general;
        var activeReason = active.Id == DefaultProfiles.GeneralId
            ? TriggerReason.Fallback
            : TriggerReason.Manual;
        return ToDiff(normalized, active, activeReason, includeWidgets: true);
    }

    private static bool TryManualOverride(AppSettings settings, ContextSnapshot snapshot, out Profile profile)
    {
        profile = null!;
        if (string.IsNullOrWhiteSpace(snapshot.ManualOverrideProfileId))
        {
            return false;
        }

        var candidate = settings.Profiles.FirstOrDefault(p => p.Id == snapshot.ManualOverrideProfileId);
        if (candidate is null)
        {
            return false;
        }

        var until = snapshot.AutoProfileTriggersEnabled
            ? snapshot.ManualOverrideUntil
            : ManualOverrideUntil.UntilManual;

        switch (until)
        {
            case ManualOverrideUntil.UntilManual:
            case ManualOverrideUntil.Session:
                profile = candidate;
                return true;
            case ManualOverrideUntil.NextFocusOrTopology:
                var focusChanged = snapshot.ManualOverrideFocusActive is bool recordedFocus
                                  && recordedFocus != snapshot.FocusActive;
                var topologyChanged = snapshot.ManualOverrideTopologyKey is string recordedKey
                                      && !string.Equals(recordedKey, snapshot.Topology.TopologyKey, StringComparison.Ordinal);
                if (focusChanged || topologyChanged)
                {
                    return false;
                }

                profile = candidate;
                return true;
            default:
                return false;
        }
    }

    private static bool TryAutoMatch(AppSettings settings, ContextSnapshot snapshot, out Profile profile, out TriggerReason reason)
    {
        profile = null!;
        reason = TriggerReason.Fallback;

        var bestRank = int.MaxValue;
        var bestIndex = int.MinValue;
        Profile? best = null;
        TriggerReason bestReason = TriggerReason.Fallback;

        for (var index = 0; index < settings.Profiles.Count; index++)
        {
            var candidate = settings.Profiles[index];
            foreach (var trigger in candidate.Triggers)
            {
                if (!TriggerMatcher.Matches(trigger, snapshot))
                {
                    continue;
                }

                var rank = TriggerMatcher.PriorityRank(trigger.Type);
                if (rank < bestRank || (rank == bestRank && index >= bestIndex))
                {
                    bestRank = rank;
                    bestIndex = index;
                    best = candidate;
                    bestReason = TriggerMatcher.ToReason(trigger.Type);
                }
            }
        }

        if (best is null)
        {
            return false;
        }

        profile = best;
        reason = bestReason;
        return true;
    }

    private static ProfileDiff ToDiff(AppSettings settings, Profile profile, TriggerReason reason, bool includeWidgets)
    {
        var widgets = includeWidgets
            ? profile.Widgets.Where(w => w.Visible && BuiltInWidgets.IsKnown(w.Id)).ToArray()
            : [];

        var dock = includeWidgets
            ? profile.Dock
            : profile.Dock with
            {
                Items = profile.Dock.Items.Where(item => item.Type != DockItemType.WidgetChip).ToArray()
            };

        return new ProfileDiff
        {
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            Reason = reason,
            Dock = dock,
            Widgets = widgets,
            Theme = settings.Ui.Theme,
            DockMode = snapshotDockMode(settings, reason),
            ActionsToRun = reason == TriggerReason.SafeMode ? [] : profile.Actions.Where(a => a.On == ActionHook.Activate).ToArray(),
            Permissions = profile.Permissions
        };

        static DockMode snapshotDockMode(AppSettings settings, TriggerReason reason)
        {
            if (reason == TriggerReason.SafeMode)
            {
                return DockMode.Overlay;
            }

            return settings.Ui.DockMode;
        }
    }
}
