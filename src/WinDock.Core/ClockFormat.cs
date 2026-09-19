using System.Globalization;

namespace WinDock.Core;

/// <summary>Dockset-style clock copy: short / weekday dates, digital time, and a zone line.</summary>
public static class ClockFormat
{
    public static readonly IReadOnlyDictionary<string, string> Places = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["China Standard Time"] = "上海",
        ["Asia/Shanghai"] = "上海",
        ["Taipei Standard Time"] = "台北",
        ["Tokyo Standard Time"] = "东京",
        ["Asia/Tokyo"] = "东京",
        ["Korea Standard Time"] = "首尔",
        ["Singapore Standard Time"] = "新加坡",
        ["GMT Standard Time"] = "伦敦",
        ["GMT"] = "伦敦",
        ["Pacific Standard Time"] = "洛杉矶",
        ["Eastern Standard Time"] = "纽约",
        ["UTC"] = "UTC"
    };

    public static IReadOnlyList<ClockZoneChoice> ZoneChoices { get; } = Places
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .Select(group => new ClockZoneChoice(group.First().Key, group.Key))
        .ToArray();

    public static string Time(DateTime now) => now.ToString("HH:mm");

    public static string TimeWithSeconds(DateTime now) => now.ToString("HH:mm:ss");

    public static string CompactDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("MMM d", culture ?? CultureInfo.CurrentCulture);

    public static string ExpandedDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("ddd, MMM d", culture ?? CultureInfo.CurrentCulture);

    public static string FullDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("dddd, MMMM d", culture ?? CultureInfo.CurrentCulture);

    public static string ZoneLine(TimeZoneInfo zone, DateTime now)
    {
        var offset = zone.GetUtcOffset(now);
        var abs = offset.Duration();
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var gmt = $"GMT{sign}{abs:hh\\:mm}";
        var place = PlaceName(zone);
        return string.IsNullOrEmpty(place) ? gmt : $"{place} · {gmt}";
    }

    public static string PlaceName(TimeZoneInfo zone)
    {
        if (Places.TryGetValue(zone.Id, out var place))
        {
            return place;
        }

        var name = zone.DisplayName;
        var close = name.LastIndexOf(')');
        var cities = close >= 0 && close + 1 < name.Length ? name[(close + 1)..].Trim() : name;
        var first = cities.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries);
        return first.Length > 0 ? first[0].Trim() : zone.StandardName;
    }

    public static TimeZoneInfo? Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        if (string.Equals(timeZoneId, "Asia/Shanghai", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
            catch (Exception)
            {
                return null;
            }
        }

        if (string.Equals(timeZoneId, "Asia/Tokyo", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public static DateTime ConvertLocal(DateTime local, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, zone);
}

public sealed record ClockZoneChoice(string Id, string Place);
