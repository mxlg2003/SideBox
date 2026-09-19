namespace WinDock.Core;

/// <summary>Settings catalog groups for the Add Widget picker. Categories with no built-ins are omitted.</summary>
public static class WidgetCatalog
{
    public const string AllId = "all";

    public sealed record Category(string Id, string Title, string Glyph, IReadOnlyList<string> WidgetIds);

    public static readonly Category All = new(AllId, "全部模块", "\uE8A5", BuiltInWidgets.All);

    public static readonly IReadOnlyList<Category> Groups =
    [
        new("clocks", "时钟", "\uE823", [BuiltInWidgets.Clock, BuiltInWidgets.Stopwatch, BuiltInWidgets.Countdown, BuiltInWidgets.FocusTimer, BuiltInWidgets.TimeProgress]),
        new("notes", "便笺", "\uE70B", [BuiltInWidgets.Notes]),
        new("media", "媒体", "\uE8D6", [BuiltInWidgets.NowPlaying]),
        new("system", "系统", "\uE770",
        [
            BuiltInWidgets.SysActivity,
            BuiltInWidgets.Network,
            BuiltInWidgets.Power
        ]),
        new("wallpaper", "壁纸", "\uE91B", [BuiltInWidgets.BingWallpaper])
    ];

    public static IReadOnlyList<Category> Nav { get; } = new[] { All }.Concat(Groups).ToArray();

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
        if (BuiltInWidgets.DisplayName(widgetId).Contains(term, StringComparison.OrdinalIgnoreCase)
            || widgetId.Contains(term, StringComparison.OrdinalIgnoreCase)
            || BuiltInWidgets.Blurb(widgetId).Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return widgetId == BuiltInWidgets.SysActivity
               && (term.Contains("cpu", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("memory", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("内存", StringComparison.OrdinalIgnoreCase)
                   || term.Contains("系统", StringComparison.OrdinalIgnoreCase));
    }
}
