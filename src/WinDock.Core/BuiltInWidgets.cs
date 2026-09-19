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
    public const string Stopwatch = "stopwatch";
    public const string Countdown = "countdown";
    public const string FocusTimer = "focustimer";
    public const string TimeProgress = "timeprogress";

    public static readonly IReadOnlyList<string> All =
    [
        Clock,
        Notes,
        SysActivity,
        Network,
        Power,
        NowPlaying,
        BingWallpaper,
        Stopwatch,
        Countdown,
        FocusTimer,
        TimeProgress
    ];

    public static bool IsKnown(string? widgetId) =>
        widgetId is not null && All.Contains(widgetId, StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> Implemented = All;

    public static bool IsImplemented(string? widgetId) =>
        widgetId is not null && Implemented.Contains(widgetId, StringComparer.Ordinal);

    public static bool CanOpen(string? widgetId) =>
        widgetId is Notes or Clock or Stopwatch or Countdown or FocusTimer or Power or TimeProgress or SysActivity or Network;

    public static bool CanCustomize(string? widgetId) =>
        widgetId is Notes or Clock or FocusTimer or Power or TimeProgress or SysActivity or Network;

    public static bool HasDensityModes(string? widgetId) => widgetId is
        Clock or Network or NowPlaying or BingWallpaper or TimeProgress;

    public static string DisplayName(string widgetId) => widgetId switch
    {
        Clock => "时钟",
        Notes => "便笺",
        SysActivity => "系统状态",
        Network => "网络",
        Power => "电源",
        NowPlaying => "正在播放",
        BingWallpaper => "Bing 壁纸",
        Stopwatch => "秒表",
        Countdown => "倒计时",
        FocusTimer => "专注计时器",
        TimeProgress => "时间进度",
        _ => widgetId
    };

    public static string Blurb(string widgetId) => widgetId switch
    {
        Clock => "本地时间，点开看大钟。",
        Notes => "贴在岛上的便笺，点开即改。",
        SysActivity => "CPU 和内存占用。",
        Network => "上下行速率。添加时会征求显示适配器名。",
        Power => "笔记本和蓝牙外设电量。",
        NowPlaying => "当前歌曲。添加时会征求读取标题和封面。",
        BingWallpaper => "每日壁纸。添加时会访问 bing.com。",
        Stopwatch => "独立秒表。",
        Countdown => "按名称保存预设，倒数到零通知。",
        FocusTimer => "本地番茄钟，不会打开系统专注。",
        TimeProgress => "今年、本月或今天过了多少。",
        _ => widgetId
    };

    public static bool AllowsMultiple(string widgetId) =>
        widgetId is Notes or Stopwatch or Countdown or FocusTimer;

    public static WidgetPlacement DefaultPlacement(string widgetId) => widgetId switch
    {
        Clock => new WidgetPlacement { Id = Clock, Monitor = "primary", X = 40, Y = 80 },
        Notes => new WidgetPlacement { Id = Notes, Monitor = "primary", X = 40, Y = 200 },
        Power => new WidgetPlacement { Id = Power, Monitor = "primary", X = 40, Y = 320 },
        SysActivity => new WidgetPlacement { Id = SysActivity, Monitor = "primary", X = 280, Y = 80 },
        Network => new WidgetPlacement { Id = Network, Monitor = "primary", X = 280, Y = 320 },
        NowPlaying => new WidgetPlacement { Id = NowPlaying, Monitor = "primary", X = 520, Y = 80 },
        BingWallpaper => new WidgetPlacement { Id = BingWallpaper, Monitor = "primary", X = 520, Y = 200 },
        Stopwatch => new WidgetPlacement { Id = Stopwatch, Monitor = "primary", X = 520, Y = 320 },
        Countdown => new WidgetPlacement { Id = Countdown, Monitor = "primary", X = 760, Y = 320 },
        FocusTimer => new WidgetPlacement { Id = FocusTimer, Monitor = "primary", X = 520, Y = 440 },
        TimeProgress => new WidgetPlacement { Id = TimeProgress, Monitor = "primary", X = 520, Y = 560 },
        _ => new WidgetPlacement { Id = widgetId, Monitor = "primary", X = 40, Y = 80 }
    };
}
