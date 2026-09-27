using System.Globalization;

namespace WinDock.Core;

/// <summary>Dockset-style clock copy: short / weekday dates, digital time, and a zone line.</summary>
public static class ClockFormat
{
    public static readonly IReadOnlyDictionary<string, string> Places = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["China Standard Time"] = "shanghai",
        ["Asia/Shanghai"] = "shanghai",
        ["Taipei Standard Time"] = "taipei",
        ["Tokyo Standard Time"] = "tokyo",
        ["Asia/Tokyo"] = "tokyo",
        ["Korea Standard Time"] = "seoul",
        ["Singapore Standard Time"] = "singapore",
        ["GMT Standard Time"] = "london",
        ["GMT"] = "london",
        ["Pacific Standard Time"] = "losangeles",
        ["Eastern Standard Time"] = "newyork",
        ["UTC"] = "utc"
    };

    public static IReadOnlyList<ClockZoneChoice> ZoneChoices => Places
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .Select(group => new ClockZoneChoice(group.First().Key, CityName(group.Key)))
        .ToArray();

    public static string CityName(string key) => key switch
    {
        "shanghai" => UiText.CityShanghai,
        "taipei" => UiText.CityTaipei,
        "tokyo" => UiText.CityTokyo,
        "seoul" => UiText.CitySeoul,
        "singapore" => UiText.CitySingapore,
        "london" => UiText.CityLondon,
        "losangeles" => UiText.CityLosAngeles,
        "newyork" => UiText.CityNewYork,
        "utc" => "UTC",
        _ => key
    };

    public static string Time(DateTime now) => now.ToString("HH:mm");

    public static string TimeWithSeconds(DateTime now) => now.ToString("HH:mm:ss");

    public static string CompactDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("MMM d", culture ?? UiLocale.Culture);

    public static string ExpandedDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("ddd, MMM d", culture ?? UiLocale.Culture);

    public static string FullDate(DateTime now, CultureInfo? culture = null) =>
        now.ToString("dddd, MMMM d", culture ?? UiLocale.Culture);

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
        if (Places.TryGetValue(zone.Id, out var key))
        {
            return CityName(key);
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
