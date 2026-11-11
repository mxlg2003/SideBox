namespace WinDock.Core;

/// <summary>Canonical IDs for built-in widgets. Unknown IDs are rejected by the engine.</summary>
public static class BuiltInWidgets
{
    public const string Clock = "clock";
    public const string Notes = "notes";
    public const string SysActivity = "sysactivity";
    public const string Network = "network";
    public const string Power = "power";
    public const string NowPlaying = "nowplaying";
    public const string BingWallpaper = "bing";
    public const string Wallpaper = "wallpaper";
    public const string Stopwatch = "stopwatch";
    public const string Countdown = "countdown";
    public const string Alarm = "alarm";
    public const string FocusTimer = "focustimer";
    public const string TimeProgress = "timeprogress";
    public const string Weather = "weather";
    public const string Schedule = "schedule";
    public const string Disk = "disk";
    public const string Gpu = "gpu";
    public const string Processes = "processes";
    public const string Stocks = "stocks";
    public const string WhiteNoise = "whitenoise";
    public const string PlaceholderTag = "wd-placeholder";

    public static readonly IReadOnlyList<string> All =
    [
        Clock,
        Notes,
        SysActivity,
        Network,
        Power,
        NowPlaying,
        Wallpaper,
        BingWallpaper,
        Stopwatch,
        Countdown,
        Alarm,
        FocusTimer,
        TimeProgress,
        Weather,
        Schedule,
        Disk,
        Gpu,
        Processes,
        Stocks,
        WhiteNoise
    ];

    public static bool IsKnown(string? widgetId) =>
        widgetId is not null && All.Contains(widgetId, StringComparer.Ordinal);

    public static bool IsShielded(string? widgetId) =>
        (!ProductFeatures.NowPlayingEnabled
            && string.Equals(widgetId, NowPlaying, StringComparison.Ordinal))
        || (!ProductFeatures.ProcessesEnabled
            && string.Equals(widgetId, Processes, StringComparison.Ordinal));

    public static bool IsComplimentary(string? widgetId) =>
        IsOffered(widgetId);

    public static bool IsOffered(string? widgetId) =>
        IsKnown(widgetId) && !IsShielded(widgetId);

    public static bool CanAdd(string? widgetId) =>
        IsOffered(widgetId);

    public static readonly IReadOnlyList<string> Implemented = All;

    public static bool IsImplemented(string? widgetId) =>
        widgetId is not null && Implemented.Contains(widgetId, StringComparer.Ordinal);

    public static bool CanOpen(string? widgetId) =>
        widgetId is Notes or Clock or Stopwatch or Countdown or Alarm or FocusTimer or Power or TimeProgress or SysActivity or Network or Weather or Schedule or Disk or Gpu or Processes or Stocks or WhiteNoise or Wallpaper or BingWallpaper;

    public static bool CanCustomize(string? widgetId) =>
        widgetId is Notes or Clock or FocusTimer or Power or TimeProgress or SysActivity or Network or Disk or Gpu or Processes or Schedule or Stocks or Wallpaper or BingWallpaper;

    public static bool HasDensityModes(string? widgetId) => widgetId is
        Clock or Network or NowPlaying or Wallpaper or BingWallpaper or TimeProgress or Weather or WhiteNoise or Stocks;

    public static string DisplayName(string widgetId) => widgetId switch
    {
        Clock => UiText.WidgetClock,
        Notes => UiText.WidgetNotes,
        SysActivity => UiText.WidgetSysActivity,
        Network => UiText.WidgetNetwork,
        Power => UiText.WidgetPower,
        NowPlaying => UiText.WidgetNowPlaying,
        Wallpaper => UiText.WidgetWallpaper,
        BingWallpaper => UiText.WidgetBing,
        Stopwatch => UiText.WidgetStopwatch,
        Countdown => UiText.WidgetCountdown,
        Alarm => UiText.WidgetAlarm,
        FocusTimer => UiText.WidgetFocusTimer,
        TimeProgress => UiText.WidgetTimeProgress,
        Weather => UiText.WidgetWeather,
        Schedule => UiText.WidgetSchedule,
        Disk => UiText.WidgetDisk,
        Gpu => UiText.WidgetGpu,
        Processes => UiText.WidgetProcesses,
        Stocks => UiText.WidgetStocks,
        WhiteNoise => UiText.WidgetWhiteNoise,
        _ => widgetId
    };

    public static string Glyph(string widgetId) => widgetId switch
    {
        Clock => PhosphorIcon.Clock,
        Notes => PhosphorIcon.Notepad,
        SysActivity => PhosphorIcon.Cpu,
        Network => PhosphorIcon.Lightning,
        Power => PhosphorIcon.BatteryMedium,
        NowPlaying => PhosphorIcon.Headphones,
        Wallpaper => PhosphorIcon.Image,
        BingWallpaper => PhosphorIcon.Image,
        Stopwatch => PhosphorIcon.Timer,
        Countdown => PhosphorIcon.Timer,
        Alarm => PhosphorIcon.Alarm,
        FocusTimer => PhosphorIcon.Leaf,
        TimeProgress => PhosphorIcon.Calendar,
        Weather => PhosphorIcon.Sun,
        Schedule => PhosphorIcon.Calendar,
        Disk => PhosphorIcon.HardDrives,
        Gpu => PhosphorIcon.GraphicsCard,
        Processes => PhosphorIcon.AppWindow,
        Stocks => PhosphorIcon.ChartBar,
        WhiteNoise => PhosphorIcon.Headphones,
        _ => PhosphorIcon.SquaresFour
    };

    public static string Blurb(string widgetId) => widgetId switch
    {
        Clock => UiText.BlurbClock,
        Notes => UiText.BlurbNotes,
        SysActivity => UiText.BlurbSysActivity,
        Network => UiText.BlurbNetwork,
        Power => UiText.BlurbPower,
        NowPlaying => UiText.BlurbNowPlaying,
        Wallpaper => UiText.BlurbWallpaper,
        BingWallpaper => UiText.BlurbBing,
        Stopwatch => UiText.BlurbStopwatch,
        Countdown => UiText.BlurbCountdown,
        Alarm => UiText.BlurbAlarm,
        FocusTimer => UiText.BlurbFocusTimer,
        TimeProgress => UiText.BlurbTimeProgress,
        Weather => UiText.BlurbWeather,
        Schedule => UiText.BlurbSchedule,
        Disk => UiText.BlurbDisk,
        Gpu => UiText.BlurbGpu,
        Processes => UiText.BlurbProcesses,
        Stocks => UiText.BlurbStocks,
        WhiteNoise => UiText.BlurbWhiteNoise,
        _ => widgetId
    };

    public static bool MatchesSearch(string widgetId, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            if (Contains(widgetId, term))
            {
                return true;
            }
        }

        using (UiLocale.Push(UiLanguage.English))
        {
            if (Contains(widgetId, term))
            {
                return true;
            }
        }

        return widgetId.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string widgetId, string term) =>
        DisplayName(widgetId).Contains(term, StringComparison.OrdinalIgnoreCase)
        || Blurb(widgetId).Contains(term, StringComparison.OrdinalIgnoreCase);

    public static bool AllowsMultiple(string widgetId) =>
        widgetId is Notes or Stopwatch or Countdown or Alarm or FocusTimer or Schedule or Weather or Stocks;

    public static bool IsSingleton(string widgetId) =>
        widgetId is Wallpaper or BingWallpaper or WhiteNoise;

    public static WidgetPlacement DefaultPlacement(string widgetId) => widgetId switch
    {
        Clock => new WidgetPlacement { Id = Clock, Monitor = "primary", X = 40, Y = 80 },
        Notes => new WidgetPlacement { Id = Notes, Monitor = "primary", X = 40, Y = 200 },
        Power => new WidgetPlacement { Id = Power, Monitor = "primary", X = 40, Y = 320 },
        SysActivity => new WidgetPlacement { Id = SysActivity, Monitor = "primary", X = 280, Y = 80 },
        Network => new WidgetPlacement { Id = Network, Monitor = "primary", X = 280, Y = 320 },
        NowPlaying => new WidgetPlacement { Id = NowPlaying, Monitor = "primary", X = 520, Y = 80 },
        Wallpaper => new WidgetPlacement { Id = Wallpaper, Monitor = "primary", X = 520, Y = 200 },
        BingWallpaper => new WidgetPlacement { Id = BingWallpaper, Monitor = "primary", X = 520, Y = 200 },
        Stopwatch => new WidgetPlacement { Id = Stopwatch, Monitor = "primary", X = 520, Y = 320 },
        Countdown => new WidgetPlacement { Id = Countdown, Monitor = "primary", X = 760, Y = 320 },
        Alarm => new WidgetPlacement { Id = Alarm, Monitor = "primary", X = 760, Y = 200 },
        FocusTimer => new WidgetPlacement { Id = FocusTimer, Monitor = "primary", X = 520, Y = 440 },
        TimeProgress => new WidgetPlacement { Id = TimeProgress, Monitor = "primary", X = 520, Y = 560 },
        Weather => new WidgetPlacement { Id = Weather, Monitor = "primary", X = 760, Y = 440 },
        Schedule => new WidgetPlacement { Id = Schedule, Monitor = "primary", X = 760, Y = 560 },
        Disk => new WidgetPlacement { Id = Disk, Monitor = "primary", X = 280, Y = 200 },
        Gpu => new WidgetPlacement { Id = Gpu, Monitor = "primary", X = 280, Y = 440 },
        Processes => new WidgetPlacement { Id = Processes, Monitor = "primary", X = 40, Y = 440 },
        Stocks => new WidgetPlacement { Id = Stocks, Monitor = "primary", X = 40, Y = 560 },
        WhiteNoise => new WidgetPlacement { Id = WhiteNoise, Monitor = "primary", X = 760, Y = 80 },
        _ => new WidgetPlacement { Id = widgetId, Monitor = "primary", X = 40, Y = 80 }
    };
}
