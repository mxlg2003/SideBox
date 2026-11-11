namespace WinDock.Core;

/// <summary>One widget chip on a profile's island, for the settings list.</summary>
public sealed record ProfileModuleChip(string WidgetId, string Glyph, string Label);

public static class ProfileModules
{
    public static IReadOnlyList<ProfileModuleChip> OnDock(Profile profile)
    {
        var chips = new List<ProfileModuleChip>();
        foreach (var item in profile.Dock.Items)
        {
            if (item.Type != DockItemType.WidgetChip || string.IsNullOrWhiteSpace(item.WidgetId))
            {
                continue;
            }

            if (!BuiltInWidgets.IsOffered(item.WidgetId))
            {
                continue;
            }

            chips.Add(new ProfileModuleChip(item.WidgetId, BuiltInWidgets.Glyph(item.WidgetId), Label(item)));
        }

        return chips;
    }

    private static string Label(DockItem item)
    {
        var name = BuiltInWidgets.DisplayName(item.WidgetId!);
        var detail = Detail(item);
        return string.IsNullOrWhiteSpace(detail) ? name : $"{name} · {detail}";
    }

    private static string? Detail(DockItem item) => item.WidgetId switch
    {
        BuiltInWidgets.Clock => ClockDetail(item.TimeZoneId),
        BuiltInWidgets.Alarm => AlarmDetail(item.Alarm),
        BuiltInWidgets.Countdown => Text(item.Countdown?.Name),
        BuiltInWidgets.Weather => Text(item.Weather?.City),
        BuiltInWidgets.Stocks => StockDetail(item.Stocks),
        _ => null
    };

    private static string? ClockDetail(string? timeZoneId)
    {
        var zone = ClockFormat.Resolve(timeZoneId);
        if (zone is null || string.Equals(zone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Text(ClockFormat.PlaceName(zone));
    }

    private static string AlarmDetail(AlarmSettings? settings)
    {
        var alarm = AlarmSettings.Normalize(settings);
        var clock = AlarmFormat.Clock(alarm.Hour, alarm.Minute);
        var label = alarm.Label.Trim();
        return label.Length == 0 ? clock : $"{label} {clock}";
    }

    private static string? StockDetail(StockWidgetSettings? settings)
    {
        var symbol = StockWidgetSettings.Normalize(settings).Symbol;
        if (symbol is null)
        {
            return null;
        }

        return Text(symbol.Name) ?? Text(symbol.Symbol);
    }

    private static string? Text(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
