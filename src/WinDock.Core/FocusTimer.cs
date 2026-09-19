namespace WinDock.Core;

public enum FocusTimerPhase
{
    Focus,
    ShortBreak,
    LongBreak
}

public sealed record FocusTimerSettings
{
    public string Color { get; init; } = FocusTimerColors.Default;

    public int FocusMinutes { get; init; } = 25;

    public int ShortBreakMinutes { get; init; } = 5;

    public int LongBreakMinutes { get; init; } = 15;

    public int LongBreakEvery { get; init; } = 4;

    public bool NotifyWhenFinished { get; init; } = true;

    public static FocusTimerSettings Default { get; } = new();

    public static FocusTimerSettings Normalize(FocusTimerSettings? settings)
    {
        var value = settings ?? Default;
        return value with
        {
            Color = FocusTimerColors.Normalize(value.Color),
            FocusMinutes = Clamp(value.FocusMinutes, 1, 180),
            ShortBreakMinutes = Clamp(value.ShortBreakMinutes, 1, 60),
            LongBreakMinutes = Clamp(value.LongBreakMinutes, 1, 60),
            LongBreakEvery = Clamp(value.LongBreakEvery, 2, 12)
        };
    }

    public int MinutesFor(FocusTimerPhase phase) => phase switch
    {
        FocusTimerPhase.ShortBreak => ShortBreakMinutes,
        FocusTimerPhase.LongBreak => LongBreakMinutes,
        _ => FocusMinutes
    };

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}

public static class FocusTimerColors
{
    public const string Orange = "orange";
    public const string Red = "red";
    public const string Magenta = "magenta";
    public const string Purple = "purple";
    public const string Indigo = "indigo";
    public const string Blue = "blue";
    public const string Teal = "teal";
    public const string Green = "green";
    public const string Default = Orange;

    public static readonly IReadOnlyList<string> All =
    [
        Orange,
        Red,
        Magenta,
        Purple,
        Indigo,
        Blue,
        Teal,
        Green
    ];

    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Default;
        }

        return All.FirstOrDefault(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    public static uint Fill(string? id) => Normalize(id) switch
    {
        Red => 0xFFFF3B30,
        Magenta => 0xFFFF2D55,
        Purple => 0xFFAF52DE,
        Indigo => 0xFF5856D6,
        Blue => 0xFF007AFF,
        Teal => 0xFF00C7BE,
        Green => 0xFF34C759,
        _ => 0xFFFF9F0A
    };
}

public static class FocusTimerFormat
{
    public static readonly int[] FocusPresets = [15, 25, 45, 60];

    public static string Time(TimeSpan remaining)
    {
        var total = Math.Max(0, (long)remaining.TotalSeconds);

        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var seconds = total % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    public static string Phase(FocusTimerPhase phase) => phase switch
    {
        FocusTimerPhase.ShortBreak => "休息",
        FocusTimerPhase.LongBreak => "长休息",
        _ => "专注"
    };

    public static string SessionLine(FocusTimerPhase phase, int session, int every) =>
        $"{Phase(phase)} · 第 {Math.Max(1, session)} / {Math.Max(1, every)} 段";

    public static string TileStatus(bool running, bool paused, FocusTimerPhase phase) =>
        paused && !running ? "已暂停" : Phase(phase);

    public static string Preset(int minutes) => $"{minutes} 分";

    public static string StepValue(int value, string unit) => $"{value} {unit}";
}

public sealed class FocusTimerEngine
{
    private TimeSpan _held;
    private DateTimeOffset? _endsAt;

    public FocusTimerEngine(FocusTimerSettings? settings = null)
    {
        Settings = FocusTimerSettings.Normalize(settings);
        _held = Duration;
    }

    public FocusTimerSettings Settings { get; private set; }

    public FocusTimerPhase Phase { get; private set; } = FocusTimerPhase.Focus;

    public int Session { get; private set; } = 1;

    public bool Running { get; private set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(Settings.MinutesFor(Phase));

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

    public double RemainingFraction(DateTimeOffset now)
    {
        var total = Duration.TotalSeconds;
        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(Remaining(now).TotalSeconds / total, 0, 1);
    }

    public void ApplySettings(FocusTimerSettings? settings, DateTimeOffset now)
    {
        var next = FocusTimerSettings.Normalize(settings);
        if (next == Settings)
        {
            return;
        }

        Settings = next;
        Session = Math.Min(Session, Settings.LongBreakEvery);
        var remaining = Remaining(now);
        if (!Running)
        {
            _held = remaining > Duration ? Duration : remaining;
            if (!IsPaused(now) && remaining >= Duration - TimeSpan.FromMilliseconds(400))
            {
                _held = Duration;
            }
        }
        else if (remaining > Duration)
        {
            _endsAt = now + Duration;
        }
    }

    public void SetFocusMinutes(int minutes, DateTimeOffset now)
    {
        Settings = FocusTimerSettings.Normalize(Settings with { FocusMinutes = minutes });
        if (Phase != FocusTimerPhase.Focus)
        {
            return;
        }

        Running = false;
        _endsAt = null;
        _held = Duration;
    }

    public void Start(DateTimeOffset now)
    {
        var remaining = Remaining(now);
        if (remaining <= TimeSpan.Zero)
        {
            remaining = Duration;
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

    public void ResetPeriod()
    {
        Running = false;
        _endsAt = null;
        _held = Duration;
    }

    public void ResetToSession1()
    {
        Running = false;
        _endsAt = null;
        Phase = FocusTimerPhase.Focus;
        Session = 1;
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

        CompletePeriod();
        return true;
    }

    private void CompletePeriod()
    {
        Running = false;
        _endsAt = null;
        if (Phase == FocusTimerPhase.Focus)
        {
            Phase = Session % Settings.LongBreakEvery == 0
                ? FocusTimerPhase.LongBreak
                : FocusTimerPhase.ShortBreak;
        }
        else
        {
            Session = Phase == FocusTimerPhase.LongBreak ? 1 : Session + 1;
            Phase = FocusTimerPhase.Focus;
        }

        _held = Duration;
    }
}
