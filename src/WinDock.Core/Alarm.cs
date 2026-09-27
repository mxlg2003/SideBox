namespace WinDock.Core;

public enum AlarmDay
{
    Today,
    Tomorrow,
    Later
}

public sealed record AlarmSettings
{
    public const int MaxLabelLength = 40;
    public static readonly TimeSpan MaxMissed = TimeSpan.FromHours(36);

    public int Hour { get; init; } = 8;

    public int Minute { get; init; }

    public string Label { get; init; } = "";

    public bool Armed { get; init; }

    public DateTimeOffset? RingsAt { get; init; }

    public static AlarmSettings Default { get; } = new();

    public static AlarmSettings Normalize(AlarmSettings? settings)
    {
        var value = settings ?? Default;
        var armed = value.Armed;
        return value with
        {
            Hour = Clamp(value.Hour, 0, 23),
            Minute = Clamp(value.Minute, 0, 59),
            Label = ClipLabel(value.Label),
            Armed = armed,
            RingsAt = armed ? value.RingsAt : null
        };
    }

    public static bool Same(AlarmSettings left, AlarmSettings right) =>
        left.Hour == right.Hour
        && left.Minute == right.Minute
        && left.Armed == right.Armed
        && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
        && Nullable.Equals(left.RingsAt, right.RingsAt);

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

    public static string ClipLabel(string? label)
    {
        var trimmed = (label ?? string.Empty).Trim();
        return trimmed.Length <= MaxLabelLength ? trimmed : trimmed[..MaxLabelLength];
    }

    internal static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}

public static class AlarmFormat
{
    public static string IdleTitle => UiText.WidgetAlarm;
    public static string IdleHint => UiLocale.T("点击设定时间", "Click to set time");
    public static string Time => UiLocale.T("时间", "Time");
    public static string Label => UiLocale.T("标签", "Label");
    public static string Set => UiLocale.T("设定闹钟", "Set alarm");
    public static string Cancel => UiLocale.T("取消闹钟", "Cancel alarm");
    public static string Finished => CountdownFormat.Finished;
    public static string Today => UiText.Today;
    public static string Tomorrow => UiText.Tomorrow;

    public static string Clock(int hour, int minute) =>
        $"{AlarmSettings.Clamp(hour, 0, 23):00}:{AlarmSettings.Clamp(minute, 0, 59):00}";

    public static string Day(AlarmDay day) => day switch
    {
        AlarmDay.Tomorrow => Tomorrow,
        AlarmDay.Later => Tomorrow,
        _ => Today
    };

    public static string When(AlarmDay day, int hour, int minute) =>
        $"{Day(day)} {Clock(hour, minute)}";

    public static string Remaining(TimeSpan remaining)
    {
        var total = Math.Max(0, (long)remaining.TotalSeconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;
        return $"{hours}:{minutes:00}:{seconds:00}";
    }

    public static string CompactRemaining(TimeSpan remaining)
    {
        var total = Math.Max(0, (long)remaining.TotalSeconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;
        if (hours > 0)
        {
            return $"{hours}h{minutes:00}m";
        }

        return minutes > 0 ? $"{minutes}m" : $"{seconds}s";
    }

    public static string ArmedStatus(AlarmDay day, TimeSpan remaining) =>
        UiLocale.IsChinese
            ? $"{Day(day)} · 还剩 {Remaining(remaining)}"
            : $"{Day(day)} · {Remaining(remaining)} left";

    public static string FinishedMessage(string? label) =>
        string.IsNullOrWhiteSpace(label) ? Finished : label.Trim();
}

public sealed class AlarmEngine
{
    private readonly TimeZoneInfo _zone;

    public AlarmEngine(AlarmSettings? settings = null, TimeZoneInfo? zone = null)
    {
        _zone = zone ?? TimeZoneInfo.Local;
        Settings = AlarmSettings.Normalize(settings);
    }

    public AlarmSettings Settings { get; private set; }

    public bool Armed => Settings.Armed;

    public void ApplySettings(AlarmSettings? settings, DateTimeOffset now)
    {
        var next = WithResolvedRing(AlarmSettings.Normalize(settings), now);
        if (AlarmSettings.Same(next, Settings))
        {
            return;
        }

        Settings = next;
    }

    public void SetTime(int hour, int minute)
    {
        if (Settings.Armed)
        {
            return;
        }

        Settings = AlarmSettings.Normalize(Settings with { Hour = hour, Minute = minute });
    }

    public void SetLabel(string? label)
    {
        Settings = Settings with { Label = AlarmSettings.ClipLabel(label) };
    }

    public DateTimeOffset Peek(DateTimeOffset now) =>
        NextRing(now, Settings.Hour, Settings.Minute, _zone);

    public TimeSpan Remaining(DateTimeOffset now)
    {
        if (Settings.RingsAt is not DateTimeOffset ringsAt)
        {
            var peek = Peek(now);
            return peek > now ? peek - now : TimeSpan.Zero;
        }

        return ringsAt > now ? ringsAt - now : TimeSpan.Zero;
    }

    public AlarmDay Day(DateTimeOffset now, DateTimeOffset? ringsAt = null)
    {
        var at = ringsAt ?? (Settings.Armed ? Settings.RingsAt : null) ?? Peek(now);
        var localNow = TimeZoneInfo.ConvertTime(now, _zone).Date;
        var localRing = TimeZoneInfo.ConvertTime(at, _zone).Date;
        var days = (localRing - localNow).Days;
        return days switch
        {
            <= 0 => AlarmDay.Today,
            1 => AlarmDay.Tomorrow,
            _ => AlarmDay.Later
        };
    }

    public void Arm(DateTimeOffset now)
    {
        var ringsAt = NextRing(now, Settings.Hour, Settings.Minute, _zone);
        Settings = AlarmSettings.Normalize(Settings with
        {
            Armed = true,
            RingsAt = ringsAt
        });
    }

    public void Cancel()
    {
        Settings = AlarmSettings.Normalize(Settings with { Armed = false, RingsAt = null });
    }

    public bool Tick(DateTimeOffset now)
    {
        if (!Settings.Armed || Settings.RingsAt is not DateTimeOffset ringsAt)
        {
            return false;
        }

        if (now < ringsAt)
        {
            return false;
        }

        var overdue = now - ringsAt;
        Cancel();
        return overdue <= AlarmSettings.MaxMissed;
    }

    private AlarmSettings WithResolvedRing(AlarmSettings settings, DateTimeOffset now)
    {
        if (!settings.Armed)
        {
            return settings with { RingsAt = null };
        }

        if (settings.RingsAt is DateTimeOffset ringsAt)
        {
            if (now - ringsAt > AlarmSettings.MaxMissed)
            {
                return settings with { Armed = false, RingsAt = null };
            }

            return settings;
        }

        return settings with { RingsAt = NextRing(now, settings.Hour, settings.Minute, _zone) };
    }

    public static DateTimeOffset NextRing(DateTimeOffset now, int hour, int minute, TimeZoneInfo zone)
    {
        hour = AlarmSettings.Clamp(hour, 0, 23);
        minute = AlarmSettings.Clamp(minute, 0, 59);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        for (var add = 0; add < 3; add++)
        {
            if (TryLocalInstant(date.AddDays(add), hour, minute, zone, out var instant) && instant > now)
            {
                return instant;
            }
        }

        return now.AddDays(1);
    }

    private static bool TryLocalInstant(
        DateOnly date,
        int hour,
        int minute,
        TimeZoneInfo zone,
        out DateTimeOffset instant)
    {
        var unspecified = date.ToDateTime(new TimeOnly(hour, minute));
        if (zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
            if (zone.IsInvalidTime(unspecified))
            {
                instant = default;
                return false;
            }
        }

        if (zone.IsAmbiguousTime(unspecified))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(unspecified);
            DateTimeOffset? chosen = null;
            foreach (var offset in offsets.OrderBy(value => value))
            {
                var candidate = new DateTimeOffset(unspecified, offset);
                if (chosen is null || candidate < chosen)
                {
                    chosen = candidate;
                }
            }

            instant = chosen ?? default;
            return chosen is not null;
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified),
            zone);
        instant = new DateTimeOffset(utc, TimeSpan.Zero);
        return true;
    }
}
