namespace WinDock.Core;

/// <summary>Settings catalog groups for the Add Widget picker. Categories with no built-ins are omitted.</summary>
public static class WidgetCatalog
{
    public const string AllId = "all";

    public sealed record Category(string Id, string Title, string Glyph, IReadOnlyList<string> WidgetIds);

    public static Category All => new(AllId, UiText.CatalogAll, "\uE8A5", BuiltInWidgets.All);

    public static IReadOnlyList<Category> Groups =>
    [
        new("clocks", UiText.CatalogClocks, "\uE823", [BuiltInWidgets.Clock, BuiltInWidgets.Stopwatch, BuiltInWidgets.Countdown, BuiltInWidgets.Alarm, BuiltInWidgets.FocusTimer, BuiltInWidgets.TimeProgress]),
        new("notes", UiText.CatalogNotes, "\uE70B", [BuiltInWidgets.Notes]),
        new("media", UiText.CatalogMedia, "\uE8D6", [BuiltInWidgets.NowPlaying]),
        new("system", UiText.CatalogSystem, "\uE770",
        [
            BuiltInWidgets.SysActivity,
            BuiltInWidgets.Network,
            BuiltInWidgets.Power
        ]),
        new("wallpaper", UiText.CatalogWallpaper, "\uE91B", [BuiltInWidgets.BingWallpaper]),
        new("lifestyle", UiText.CatalogLifestyle, "\uEC05", [BuiltInWidgets.Weather, BuiltInWidgets.Schedule])
    ];

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

        return widgetId == BuiltInWidgets.SysActivity
               && (term.Contains("cpu", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("memory", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("mem", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("内存", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("系统", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("system", StringComparison.OrdinalIgnoreCase));
    }
}
