namespace WinDock.Core;

public enum WeatherCondition
{
    Clear,
    PartlyCloudy,
    Overcast,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Thunderstorm,
    Windy,
    Unknown
}

public enum WeatherError
{
    None,
    NeedCity,
    CityNotFound,
    LocateFailed,
    Network
}

public sealed record WeatherHourlyForecast
{
    public DateTime Time { get; init; }

    public double TemperatureC { get; init; }

    public WeatherCondition Condition { get; init; }

    public int PrecipitationProbability { get; init; }
}

public sealed record WeatherDailyForecast
{
    public DateOnly Date { get; init; }

    public double TempMaxC { get; init; }

    public double TempMinC { get; init; }

    public WeatherCondition Condition { get; init; }

    public string ConditionText { get; init; } = "";
}

public sealed record WeatherSnapshot
{
    public string City { get; init; } = "";

    public double TemperatureC { get; init; }

    public double FeelsLikeC { get; init; }

    public int Humidity { get; init; }

    public double WindSpeedKmh { get; init; }

    public string WindDirection { get; init; } = "";

    public int AirQualityIndex { get; init; }

    public WeatherCondition Condition { get; init; }

    public string ConditionText { get; init; } = "";

    public DateTime UpdatedTime { get; init; } = DateTime.Now;

    public bool IsOffline { get; init; }

    public WeatherError Error { get; init; }

    public IReadOnlyList<WeatherHourlyForecast> Hourly { get; init; } = [];

    public IReadOnlyList<WeatherDailyForecast> Daily { get; init; } = [];
}

public sealed record WeatherSettings
{
    public const int DefaultIntervalMinutes = 30;

    public string City { get; init; } = "";

    public bool AutoLocate { get; init; }

    public bool UseFahrenheit { get; init; }

    public int RefreshIntervalMinutes { get; init; } = DefaultIntervalMinutes;

    public static WeatherSettings Default { get; } = new();

    public static WeatherSettings Normalize(WeatherSettings? settings)
    {
        var value = settings ?? Default;
        return value with
        {
            City = (value.City ?? "").Trim(),
            RefreshIntervalMinutes = Math.Clamp(value.RefreshIntervalMinutes, 10, 180)
        };
    }

    public static bool Same(WeatherSettings left, WeatherSettings right) =>
        string.Equals(left.City, right.City, StringComparison.OrdinalIgnoreCase)
        && left.AutoLocate == right.AutoLocate
        && left.UseFahrenheit == right.UseFahrenheit
        && left.RefreshIntervalMinutes == right.RefreshIntervalMinutes;
}

public static class WeatherGlyphMapper
{
    public static (WeatherCondition Condition, string DefaultText) FromWmoCode(int code, bool isDay = true)
    {
        return code switch
        {
            0 => (WeatherCondition.Clear, isDay ? "晴" : "晴朗"),
            1 => (WeatherCondition.Clear, isDay ? "大部分晴" : "晴朗"),
            2 => (WeatherCondition.PartlyCloudy, "多云"),
            3 => (WeatherCondition.Overcast, "阴"),
            45 or 48 => (WeatherCondition.Fog, "雾"),
            51 or 53 or 55 => (WeatherCondition.Drizzle, "毛毛雨"),
            56 or 57 => (WeatherCondition.Drizzle, "冻毛毛雨"),
            61 => (WeatherCondition.Rain, "小雨"),
            63 => (WeatherCondition.Rain, "中雨"),
            65 => (WeatherCondition.Rain, "大雨"),
            66 or 67 => (WeatherCondition.Rain, "冻雨"),
            71 => (WeatherCondition.Snow, "小雪"),
            73 => (WeatherCondition.Snow, "中雪"),
            75 => (WeatherCondition.Snow, "大雪"),
            77 => (WeatherCondition.Snow, "雪粒"),
            80 or 81 or 82 => (WeatherCondition.Rain, "阵雨"),
            85 or 86 => (WeatherCondition.Snow, "阵雪"),
            95 => (WeatherCondition.Thunderstorm, "雷阵雨"),
            96 or 99 => (WeatherCondition.Thunderstorm, "雷暴伴冰雹"),
            _ => (WeatherCondition.Unknown, "未知")
        };
    }

    public static string ConditionGlyph(WeatherCondition condition, bool isDay = true) => condition switch
    {
        WeatherCondition.Clear => isDay ? "\u2600" : "\U0001F319",
        WeatherCondition.PartlyCloudy => "\u26C5",
        WeatherCondition.Overcast => "\u2601",
        WeatherCondition.Fog => "\U0001F32B",
        WeatherCondition.Drizzle => "\U0001F326",
        WeatherCondition.Rain => "\U0001F327",
        WeatherCondition.Snow => "\u2744",
        WeatherCondition.Thunderstorm => "\u26C8",
        WeatherCondition.Windy => "\U0001F4A8",
        _ => "\u2600"
    };

    public static uint ConditionColor(WeatherCondition condition, bool isDay = true) => condition switch
    {
        WeatherCondition.Clear => isDay ? 0xFFF59E0B : 0xFFFCD34D, // Golden amber / soft gold
        WeatherCondition.PartlyCloudy => 0xFF38BDF8,               // Sky blue / sun
        WeatherCondition.Overcast => 0xFF94A3B8,                   // Slate gray
        WeatherCondition.Fog => 0xFF94A3B8,                        // Mist gray
        WeatherCondition.Drizzle => 0xFF38BDF8,                    // Light rain blue
        WeatherCondition.Rain => 0xFF0284C7,                       // Rain blue
        WeatherCondition.Snow => 0xFF7DD3FC,                       // Ice cyan
        WeatherCondition.Thunderstorm => 0xFFA855F7,               // Lightning purple
        WeatherCondition.Windy => 0xFF0EA5E9,                      // Wind cyan
        _ => 0xFFF59E0B
    };

    public static string ConditionLocalized(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => UiText.WeatherClear,
        WeatherCondition.PartlyCloudy => UiText.WeatherPartlyCloudy,
        WeatherCondition.Overcast => UiText.WeatherOvercast,
        WeatherCondition.Fog => UiText.WeatherFog,
        WeatherCondition.Drizzle => UiText.WeatherDrizzle,
        WeatherCondition.Rain => UiText.WeatherRain,
        WeatherCondition.Snow => UiText.WeatherSnow,
        WeatherCondition.Thunderstorm => UiText.WeatherThunderstorm,
        WeatherCondition.Windy => UiText.WeatherWindy,
        _ => UiText.WeatherUnknown
    };
}

public static class WeatherFormat
{
    public static double ToDisplayTemperature(double tempC, bool fahrenheit) =>
        fahrenheit ? Math.Round((tempC * 9.0 / 5.0) + 32.0, 1) : Math.Round(tempC, 1);

    public static string Temperature(double tempC, bool fahrenheit)
    {
        var val = (int)Math.Round(ToDisplayTemperature(tempC, fahrenheit));
        return fahrenheit ? $"{val}°F" : $"{val}°";
    }

    public static string TemperatureFull(double tempC, bool fahrenheit)
    {
        var val = (int)Math.Round(ToDisplayTemperature(tempC, fahrenheit));
        return fahrenheit ? $"{val}°F" : $"{val}°C";
    }

    public static string Range(double minC, double maxC, bool fahrenheit)
    {
        var min = (int)Math.Round(ToDisplayTemperature(minC, fahrenheit));
        var max = (int)Math.Round(ToDisplayTemperature(maxC, fahrenheit));
        return $"{min}° ~ {max}°";
    }

    public static string AqiLevel(int aqi)
    {
        if (aqi < 0)
        {
            return "—";
        }

        return aqi switch
        {
            <= 50 => UiText.AqiGood,
            <= 100 => UiText.AqiModerate,
            <= 150 => UiText.AqiUnhealthySensitive,
            <= 200 => UiText.AqiUnhealthy,
            <= 300 => UiText.AqiVeryUnhealthy,
            _ => UiText.AqiHazardous
        };
    }

    public static uint AqiColor(int aqi)
    {
        if (aqi < 0)
        {
            return 0xFF94A3B8;
        }

        return aqi switch
        {
            <= 50 => 0xFF10B981,  // Good - Emerald Green
            <= 100 => 0xFF3B82F6, // Moderate - Blue / Cyan
            <= 150 => 0xFFF59E0B, // Sensitive - Orange
            <= 200 => 0xFFEF4444, // Unhealthy - Red
            <= 300 => 0xFF8B5CF6, // Very Unhealthy - Purple
            _ => 0xFF78350F       // Hazardous - Maroon
        };
    }

    public static string ErrorText(WeatherError error) => error switch
    {
        WeatherError.NeedCity => UiText.WeatherNeedCity,
        WeatherError.CityNotFound => UiText.WeatherCityNotFound,
        WeatherError.LocateFailed => UiText.WeatherLocateFailed,
        WeatherError.Network => UiText.WeatherNetworkError,
        _ => ""
    };
}
