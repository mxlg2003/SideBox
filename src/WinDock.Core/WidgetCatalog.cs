namespace WinDock.Core;

/// <summary>Settings catalog groups for the Add Widget picker. Categories with no built-ins are omitted.</summary>
public static class WidgetCatalog
{
    public const string AllId = "all";

    public sealed record Category(string Id, string Title, string Glyph, IReadOnlyList<string> WidgetIds);

    public static Category All => new(
        AllId,
        UiText.CatalogAll,
        PhosphorIcon.SquaresFour,
        BuiltInWidgets.All.Where(BuiltInWidgets.IsOffered).ToArray());

    public static IReadOnlyList<Category> Groups
    {
        get
        {
            Category[] groups =
            [
                new("clocks", UiText.CatalogClocks, PhosphorIcon.Clock, [BuiltInWidgets.Clock, BuiltInWidgets.Stopwatch, BuiltInWidgets.Countdown, BuiltInWidgets.Alarm, BuiltInWidgets.FocusTimer, BuiltInWidgets.TimeProgress]),
                new("notes", UiText.CatalogNotes, PhosphorIcon.Notepad, [BuiltInWidgets.Notes]),
                new("media", UiText.CatalogMedia, PhosphorIcon.Headphones, [BuiltInWidgets.NowPlaying]),
                new("system", UiText.CatalogSystem, PhosphorIcon.Cpu,
                [
                    BuiltInWidgets.SysActivity,
                    BuiltInWidgets.Network,
                    BuiltInWidgets.Power,
                    BuiltInWidgets.Disk,
                    BuiltInWidgets.Gpu,
                    BuiltInWidgets.Processes
                ]),
                new("wallpaper", UiText.CatalogWallpaper, PhosphorIcon.Image, [BuiltInWidgets.Wallpaper, BuiltInWidgets.BingWallpaper]),
                new("lifestyle", UiText.CatalogLifestyle, PhosphorIcon.Sun, [BuiltInWidgets.Weather, BuiltInWidgets.Schedule, BuiltInWidgets.WhiteNoise]),
                new("business", UiText.CatalogBusiness, PhosphorIcon.ChartBar, [BuiltInWidgets.Stocks])
            ];
            return groups
                .Select(group => group with
                {
                    WidgetIds = group.WidgetIds.Where(BuiltInWidgets.IsOffered).ToArray()
                })
                .Where(group => group.WidgetIds.Count > 0)
                .ToArray();
        }
    }

    public static IReadOnlyList<Category> Nav => new[] { All }.Concat(Groups).ToArray();

    public static IReadOnlyList<Category> VisibleGroups(string categoryId, string? query)
    {
        var groups = string.Equals(categoryId, AllId, StringComparison.Ordinal)
            ? Groups
            : Groups.Where(group => string.Equals(group.Id, categoryId, StringComparison.Ordinal));

        return groups
            .Select(group => group with
            {
                WidgetIds = group.WidgetIds.Where(id => Matches(id, query)).ToArray()
            })
            .Where(group => group.WidgetIds.Count > 0)
            .ToArray();
    }

    public static bool Matches(string widgetId, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var term = query.Trim();
        if (BuiltInWidgets.MatchesSearch(widgetId, term))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Weather
            && (term.Contains("天气", StringComparison.OrdinalIgnoreCase)
                || term.Contains("气温", StringComparison.OrdinalIgnoreCase)
                || term.Contains("温度", StringComparison.OrdinalIgnoreCase)
                || term.Contains("预报", StringComparison.OrdinalIgnoreCase)
                || term.Contains("weather", StringComparison.OrdinalIgnoreCase)
                || term.Contains("temp", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Schedule
            && (term.Contains("日程", StringComparison.OrdinalIgnoreCase)
                || term.Contains("倒数", StringComparison.OrdinalIgnoreCase)
                || term.Contains("待办", StringComparison.OrdinalIgnoreCase)
                || term.Contains("日历", StringComparison.OrdinalIgnoreCase)
                || term.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                || term.Contains("todo", StringComparison.OrdinalIgnoreCase)
                || term.Contains("calendar", StringComparison.OrdinalIgnoreCase)
                || term.Contains("countdown", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Disk
            && (term.Contains("磁盘", StringComparison.OrdinalIgnoreCase)
                || term.Contains("硬盘", StringComparison.OrdinalIgnoreCase)
                || term.Contains("disk", StringComparison.OrdinalIgnoreCase)
                || term.Contains("drive", StringComparison.OrdinalIgnoreCase)
                || term.Contains("storage", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Gpu
            && (term.Contains("gpu", StringComparison.OrdinalIgnoreCase)
                || term.Contains("显卡", StringComparison.OrdinalIgnoreCase)
                || term.Contains("显存", StringComparison.OrdinalIgnoreCase)
                || term.Contains("graphics", StringComparison.OrdinalIgnoreCase)
                || term.Contains("vram", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Stocks
            && (term.Contains("股票", StringComparison.OrdinalIgnoreCase)
                || term.Contains("行情", StringComparison.OrdinalIgnoreCase)
                || term.Contains("自选", StringComparison.OrdinalIgnoreCase)
                || term.Contains("stock", StringComparison.OrdinalIgnoreCase)
                || term.Contains("quote", StringComparison.OrdinalIgnoreCase)
                || term.Contains("商业", StringComparison.OrdinalIgnoreCase)
                || term.Contains("business", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Processes
            && (term.Contains("进程", StringComparison.OrdinalIgnoreCase)
                || term.Contains("process", StringComparison.OrdinalIgnoreCase)
                || term.Contains("占用", StringComparison.OrdinalIgnoreCase)
                || term.Contains("task", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.WhiteNoise
            && (term.Contains("白噪音", StringComparison.OrdinalIgnoreCase)
                || term.Contains("噪音", StringComparison.OrdinalIgnoreCase)
                || term.Contains("声音", StringComparison.OrdinalIgnoreCase)
                || term.Contains("专注", StringComparison.OrdinalIgnoreCase)
                || term.Contains("雨声", StringComparison.OrdinalIgnoreCase)
                || term.Contains("音效", StringComparison.OrdinalIgnoreCase)
                || term.Contains("自然", StringComparison.OrdinalIgnoreCase)
                || term.Contains("whitenoise", StringComparison.OrdinalIgnoreCase)
                || term.Contains("noise", StringComparison.OrdinalIgnoreCase)
                || term.Contains("sound", StringComparison.OrdinalIgnoreCase)
                || term.Contains("ambient", StringComparison.OrdinalIgnoreCase)
                || term.Contains("rain", StringComparison.OrdinalIgnoreCase)
                || term.Contains("focus", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return widgetId == BuiltInWidgets.SysActivity
               && (term.Contains("cpu", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("memory", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("mem", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("内存", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("系统", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("system", StringComparison.OrdinalIgnoreCase));
    }
}
