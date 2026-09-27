namespace WinDock.Core;

public sealed record CountdownPreset
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public int Hours { get; init; }

    public int Minutes { get; init; }

    public int Seconds { get; init; }
}

public sealed record CountdownSettings
{
    public const int MaxNameLength = 40;
    public const int MaxPresets = 12;
    public const int MaxHours = 99;

    public int Hours { get; init; }

    public int Minutes { get; init; } = 5;

    public int Seconds { get; init; }

    public string Name { get; init; } = "";

    public IReadOnlyList<CountdownPreset> Presets { get; init; } = [];

    public static CountdownSettings Default { get; } = new();

    public TimeSpan Duration => ToDuration(Hours, Minutes, Seconds);

    public static CountdownSettings Normalize(CountdownSettings? settings)
    {
        var value = settings ?? Default;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var presets = new List<CountdownPreset>();
        foreach (var preset in value.Presets ?? [])
        {
            var next = NormalizePreset(preset);
            if (next is null || !seen.Add(next.Name))
            {
                continue;
            }

            presets.Add(next);
            if (presets.Count == MaxPresets)
            {
                break;
            }
        }

        return value with
        {
            Hours = Clamp(value.Hours, 0, MaxHours),
            Minutes = Clamp(value.Minutes, 0, 59),
            Seconds = Clamp(value.Seconds, 0, 59),
            Name = ClipName(value.Name),
            Presets = presets
        };
    }

    public static bool Same(CountdownSettings left, CountdownSettings right)
    {
        if (left.Hours != right.Hours
            || left.Minutes != right.Minutes
            || left.Seconds != right.Seconds
            || !string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || left.Presets.Count != right.Presets.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Presets.Count; i++)
        {
            if (left.Presets[i] != right.Presets[i])
            {
                return false;
            }
        }

        return true;
    }

    public static TimeSpan ToDuration(int hours, int minutes, int seconds) =>
        TimeSpan.FromHours(Clamp(hours, 0, MaxHours))
        + TimeSpan.FromMinutes(Clamp(minutes, 0, 59))
        + TimeSpan.FromSeconds(Clamp(seconds, 0, 59));

    /// <summary>Wrap <paramref name="value"/> into 0..<paramref name="max"/> inclusive.</summary>
    public static int Wrap(int value, int max)
    {
        var range = max + 1;
        if (range <= 0)
        {
            return 0;
        }

        var wrapped = value % range;
        return wrapped < 0 ? wrapped + range : wrapped;
    }

    public static string ClipName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }

    public static CountdownPreset? NormalizePreset(CountdownPreset? preset)
    {
        if (preset is null)
        {
            return null;
        }

        var name = ClipName(preset.Name);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var hours = Clamp(preset.Hours, 0, MaxHours);
        var minutes = Clamp(preset.Minutes, 0, 59);
        var seconds = Clamp(preset.Seconds, 0, 59);
        if (hours == 0 && minutes == 0 && seconds == 0)
        {
            return null;
        }

        var id = string.IsNullOrWhiteSpace(preset.Id) ? name : preset.Id.Trim();
        return preset with
        {
            Id = id,
            Name = name,
            Hours = hours,
            Minutes = minutes,
            Seconds = seconds
        };
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}

public static class CountdownFormat
{
    public static string Ready => UiText.WidgetCountdown;
    public static string Paused => StopwatchFormat.Paused;
    public static string Finished => UiLocale.T("时间到", "Time’s up");
    public static string Presets => UiLocale.T("预设", "Presets");
    public static string TimerName => UiLocale.T("计时器名称", "Timer name");
    public static string Hours => UiLocale.T("小时", "Hours");
    public static string Minutes => UiLocale.T("分钟", "Minutes");
    public static string Seconds => UiLocale.T("秒", "Seconds");
    public static string PresetSaved => UiLocale.T("已保存预设", "Preset saved");

    public static string Time(TimeSpan remaining) => StopwatchFormat.Time(remaining);

    public static string TileStatus(bool running, bool paused) =>
        paused && !running ? Paused : Ready;

    public static string FinishedMessage(string? name) =>
        string.IsNullOrWhiteSpace(name) ? Finished : name.Trim();
}

public sealed class CountdownEngine
{
    private TimeSpan _held;
    private DateTimeOffset? _endsAt;

    public CountdownEngine(CountdownSettings? settings = null)
    {
        Settings = CountdownSettings.Normalize(settings);
        _held = Settings.Duration;
    }

    public CountdownSettings Settings { get; private set; }

    public bool Running { get; private set; }

    public TimeSpan Duration => Settings.Duration;

    public TimeSpan Remaining(DateTimeOffset now)
    {
        if (Running && _endsAt is DateTimeOffset end)
        {
            return end > now ? end - now : TimeSpan.Zero;
        }

        return _held < TimeSpan.Zero ? TimeSpan.Zero : _held;
    }

    public bool IsPaused(DateTimeOffset now) =>
        !Running && Remaining(now) > TimeSpan.Zero && Remaining(now) + TimeSpan.FromMilliseconds(400) < Duration;

    public bool CanStart(DateTimeOffset now) =>
        Remaining(now) > TimeSpan.Zero || Duration > TimeSpan.Zero;

    public void ApplySettings(CountdownSettings? settings, DateTimeOffset now)
    {
        var next = CountdownSettings.Normalize(settings);
        if (CountdownSettings.Same(next, Settings))
        {
            return;
        }

        var remaining = Remaining(now);
        var wasIdle = !Running && !IsPaused(now);
        Settings = next;
        if (Running)
        {
            if (remaining > Duration)
            {
                _endsAt = now + Duration;
            }

            return;
        }

        _held = wasIdle || remaining > Duration ? Duration : remaining;
    }

    public void SetDuration(int hours, int minutes, int seconds, DateTimeOffset now)
    {
        if (Running)
        {
            return;
        }

        var next = CountdownSettings.Normalize(Settings with
        {
            Hours = hours,
            Minutes = minutes,
            Seconds = seconds
        });
        var sameDuration = next.Hours == Settings.Hours
            && next.Minutes == Settings.Minutes
            && next.Seconds == Settings.Seconds;
        Settings = next;
        if (sameDuration)
        {
            return;
        }

        _held = Duration;
        _endsAt = null;
    }

    public void SetName(string? name)
    {
        Settings = Settings with { Name = CountdownSettings.ClipName(name) };
    }

    public bool ApplyPreset(string id, DateTimeOffset now)
    {
        var preset = Settings.Presets.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal));
        if (preset is null)
        {
            return false;
        }

        if (Running)
        {
            Pause(now);
        }

        Settings = CountdownSettings.Normalize(Settings with
        {
            Name = preset.Name,
            Hours = preset.Hours,
            Minutes = preset.Minutes,
            Seconds = preset.Seconds
        });
        _held = Duration;
        _endsAt = null;
        return true;
    }

    public bool UpsertPreset()
    {
        var name = Settings.Name;
        if (string.IsNullOrEmpty(name) || Duration <= TimeSpan.Zero)
        {
            return false;
        }

        var list = Settings.Presets.ToList();
        var index = list.FindIndex(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        var preset = new CountdownPreset
        {
            Id = index >= 0 ? list[index].Id : name,
            Name = name,
            Hours = Settings.Hours,
            Minutes = Settings.Minutes,
            Seconds = Settings.Seconds
        };
        if (index >= 0)
        {
            list[index] = preset;
        }
        else
        {
            if (list.Count >= CountdownSettings.MaxPresets)
            {
                list.RemoveAt(0);
            }

            list.Add(preset);
        }

        Settings = CountdownSettings.Normalize(Settings with { Presets = list });
        return true;
    }

    public bool RemovePreset(string id)
    {
        var next = Settings.Presets.Where(item =>
            !string.Equals(item.Id, id, StringComparison.Ordinal)).ToArray();
        if (next.Length == Settings.Presets.Count)
        {
            return false;
        }

        Settings = Settings with { Presets = next };
        return true;
    }

    public void Start(DateTimeOffset now)
    {
        var remaining = Remaining(now);
        if (remaining <= TimeSpan.Zero)
        {
            remaining = Duration;
        }

        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        _held = remaining;
        _endsAt = now + remaining;
        Running = true;
    }

    public void Pause(DateTimeOffset now)
    {
        if (!Running)
        {
            return;
        }

        _held = Remaining(now);
        _endsAt = null;
        Running = false;
    }

    public void Toggle(DateTimeOffset now)
    {
        if (Running)
        {
            Pause(now);
            return;
        }

        Start(now);
    }

    public void Reset()
    {
        Running = false;
        _endsAt = null;
        _held = Duration;
    }

    public bool Tick(DateTimeOffset now)
    {
        if (!Running)
        {
            return false;
        }

        if (Remaining(now) > TimeSpan.Zero)
        {
            return false;
        }

        Reset();
        return true;
    }
}
