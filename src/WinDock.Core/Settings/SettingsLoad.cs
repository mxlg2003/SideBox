namespace WinDock.Core;

public sealed record SettingsLoadResult
{
    public required AppSettings Settings { get; init; }

    public string? UserMessage { get; init; }

    public bool UsedFactoryDefaults { get; init; }

    public bool NeedsPersist { get; init; }
}

public static class ProfileSwitchLog
{
    public const int MaxRecords = 100;

    public static IReadOnlyList<ProfileSwitchRecord> Append(
        IReadOnlyList<ProfileSwitchRecord> history,
        ProfileSwitchRecord record)
    {
        var list = history.ToList();
        list.Add(record);
        if (list.Count > MaxRecords)
        {
            list.RemoveRange(0, list.Count - MaxRecords);
        }

        return list;
    }

    public static string Summarize(ProfileSwitchRecord record, IReadOnlyList<Profile>? profiles = null)
    {
        var when = record.Timestamp.ToLocalTime().ToString("MM-dd HH:mm");
        var result = record.Result == ProfileSwitchResult.Succeeded ? UiText.SwitchSucceeded : UiText.SwitchFailed;
        var from = ProfileName(record.FromProfileId, profiles);
        var to = ProfileName(record.ToProfileId, profiles);
        var line = $"{when}  {from} → {to}  {TriggerReasonText.Format(record.Reason)}  {result}";
        return record.Result == ProfileSwitchResult.Failed && !string.IsNullOrWhiteSpace(record.ErrorMessage)
            ? $"{line}  {record.ErrorMessage}"
            : line;
    }

    private static string ProfileName(string? id, IReadOnlyList<Profile>? profiles)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "—";
        }

        var profile = profiles?.FirstOrDefault(item => item.Id == id);
        return profile is null ? id : DefaultProfiles.DisplayName(profile);
    }
}

public static class UserAlerts
{
    public static event Action<string, string>? Raised;

    public static void Show(string title, string message) => Raised?.Invoke(title, message);
}

public static class HotkeyParser
{
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;

    public static bool TryParse(string? text, out int modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "win" or "windows" or "meta" => ModWin,
                "shift" => ModShift,
                "ctrl" or "control" => ModControl,
                "alt" => ModAlt,
                _ => 0
            };
        }

        var key = parts[^1];
        if (key.Length == 1)
        {
            var ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = ch;
                return modifiers != 0;
            }
        }

        virtualKey = key.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "tab" => 0x09,
            "esc" or "escape" => 0x1B,
            "f1" => 0x70,
            "f2" => 0x71,
            "f3" => 0x72,
            "f4" => 0x73,
            "f5" => 0x74,
            "f6" => 0x75,
            "f7" => 0x76,
            "f8" => 0x77,
            "f9" => 0x78,
            "f10" => 0x79,
            "f11" => 0x7A,
            "f12" => 0x7B,
            _ => 0
        };
        return virtualKey != 0 && modifiers != 0;
    }

    public static string? Format(int modifiers, int virtualKey)
    {
        var key = KeyName(virtualKey);
        if (modifiers == 0 || key is null)
        {
            return null;
        }

        var parts = new List<string>(5);
        if ((modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }

        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        parts.Add(key);
        return string.Join("+", parts);
    }

    public static bool ConflictsWith(HotkeySettings hotkeys, string chord, bool ignoreReveal = true)
    {
        if (!TryParse(chord, out var modifiers, out var virtualKey))
        {
            return false;
        }

        return Same(hotkeys.NextProfile) || Same(hotkeys.PreviousProfile) || Same(hotkeys.UndoProfile)
            || (!ignoreReveal && Same(hotkeys.RevealDock));

        bool Same(string? other) =>
            TryParse(other, out var otherModifiers, out var otherKey)
            && otherModifiers == modifiers
            && otherKey == virtualKey;
    }

    private static string? KeyName(int virtualKey)
    {
        if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)virtualKey).ToString();
        }

        return virtualKey switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x1B => "Esc",
            0x70 => "F1",
            0x71 => "F2",
            0x72 => "F3",
            0x73 => "F4",
            0x74 => "F5",
            0x75 => "F6",
            0x76 => "F7",
            0x77 => "F8",
            0x78 => "F9",
            0x79 => "F10",
            0x7A => "F11",
            0x7B => "F12",
            _ => null
        };
    }
}

public enum HotkeyAction
{
    NextProfile,
    PreviousProfile,
    UndoProfile,
    RevealDock
}
