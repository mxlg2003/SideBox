using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class WeatherFormatTests
{
    [Fact]
    public void Temperature_conversion_celsius_and_fahrenheit()
    {
        Assert.Equal("25°", WeatherFormat.Temperature(25.0, false));
        Assert.Equal("25°C", WeatherFormat.TemperatureFull(25.0, false));

        // 0 C = 32 F
        Assert.Equal("32°F", WeatherFormat.Temperature(0.0, true));
        Assert.Equal("32°F", WeatherFormat.TemperatureFull(0.0, true));

        // 100 C = 212 F
        Assert.Equal("212°F", WeatherFormat.Temperature(100.0, true));
    }

    [Fact]
    public void Temperature_range_format()
    {
        Assert.Equal("15° ~ 25°", WeatherFormat.Range(15.2, 24.8, false));
        Assert.Equal("59° ~ 77°", WeatherFormat.Range(15.0, 25.0, true));
    }

    [Theory]
    [InlineData(0, WeatherCondition.Clear)]
    [InlineData(1, WeatherCondition.Clear)]
    [InlineData(2, WeatherCondition.PartlyCloudy)]
    [InlineData(3, WeatherCondition.Overcast)]
    [InlineData(45, WeatherCondition.Fog)]
    [InlineData(61, WeatherCondition.Rain)]
    [InlineData(71, WeatherCondition.Snow)]
    [InlineData(95, WeatherCondition.Thunderstorm)]
    public void Wmo_code_maps_to_weather_condition(int code, WeatherCondition expected)
    {
        var (condition, text) = WeatherGlyphMapper.FromWmoCode(code);
        Assert.Equal(expected, condition);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void Glyphs_mapped_for_all_conditions()
    {
        foreach (WeatherCondition cond in Enum.GetValues<WeatherCondition>())
        {
            var glyph = WeatherGlyphMapper.ConditionGlyph(cond);
            Assert.False(string.IsNullOrEmpty(glyph));
        }
    }

    [Fact]
    public void Condition_color_mapped_for_all_conditions()
    {
        foreach (WeatherCondition cond in Enum.GetValues<WeatherCondition>())
        {
            var color = WeatherGlyphMapper.ConditionColor(cond);
            Assert.True(color > 0);
        }
    }

    [Fact]
    public void Condition_glyphs_not_using_broken_mdl2_codes()
    {
        // Must not use old flashlight \uE754 or window box \uE756
        Assert.NotEqual("\uE754", WeatherGlyphMapper.ConditionGlyph(WeatherCondition.Drizzle));
        Assert.NotEqual("\uE754", WeatherGlyphMapper.ConditionGlyph(WeatherCondition.Rain));
        Assert.NotEqual("\uE756", WeatherGlyphMapper.ConditionGlyph(WeatherCondition.Thunderstorm));
    }

    [Theory]
    [InlineData(30, 0xFF10B981u)]
    [InlineData(75, 0xFF3B82F6u)]
    [InlineData(120, 0xFFF59E0Bu)]
    [InlineData(180, 0xFFEF4444u)]
    [InlineData(250, 0xFF8B5CF6u)]
    [InlineData(350, 0xFF78350Fu)]
    public void Aqi_color_classification(int aqi, uint expectedColor)
    {
        Assert.Equal(expectedColor, WeatherFormat.AqiColor(aqi));
    }

    [Fact]
    public void Missing_aqi_is_em_dash_and_neutral_color()
    {
        Assert.Equal("—", WeatherFormat.AqiLevel(-1));
        Assert.Equal(0xFF94A3B8u, WeatherFormat.AqiColor(-1));
    }

    [Fact]
    public void WeatherWidget_lifecycle_and_density_switching()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                var provider = new WinDock.Providers.WeatherProvider(dir);
                var widget = new WinDock.Widgets.WeatherWidget(provider);

                widget.ApplyDensity(WidgetDensity.Compact);
                widget.ApplyTheme(true);
                widget.ApplyTheme(false);

                widget.ApplyDensity(WidgetDensity.Expanded);
                widget.ApplyLocale();

                Assert.NotNull(widget);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
    }

    [Theory]
    [InlineData("南", "南京", "南昌", "南宁")]
    [InlineData("bj", "北京")]
    [InlineData("shanghai", "上海")]
    [InlineData("Tokyo", "东京")]
    [InlineData("成都", "成都")]
    public void WeatherCities_search_matches_query(string query, params string[] expectedMatches)
    {
        var results = WeatherCities.Search(query, 10);
        Assert.NotEmpty(results);
        foreach (var expected in expectedMatches)
        {
            Assert.Contains(results, r => r.Name.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
                                          r.DisplayLocation.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void WeatherCities_search_empty_returns_defaults()
    {
        var results = WeatherCities.Search("", 5);
        Assert.NotEmpty(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void WeatherProvider_keeps_per_city_snapshots()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cache = new
        {
            cities = new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["北京"] = new WeatherSnapshot
                {
                    City = "北京",
                    TemperatureC = 11,
                    AirQualityIndex = 40,
                    Condition = WeatherCondition.Clear,
                    ConditionText = "晴",
                    UpdatedTime = DateTime.Now,
                    Error = WeatherError.None
                },
                ["上海"] = new WeatherSnapshot
                {
                    City = "上海",
                    TemperatureC = 22,
                    AirQualityIndex = 80,
                    Condition = WeatherCondition.Rain,
                    ConditionText = "雨",
                    UpdatedTime = DateTime.Now,
                    Error = WeatherError.None
                }
            }
        };
        File.WriteAllText(
            Path.Combine(dir, "weather-cache.json"),
            System.Text.Json.JsonSerializer.Serialize(cache));

        using var provider = new WinDock.Providers.WeatherProvider(dir);
        var beijing = provider.SnapshotFor(new WeatherSettings { City = "北京" });
        var shanghai = provider.SnapshotFor(new WeatherSettings { City = "上海" });

        Assert.Equal(11, beijing?.TemperatureC);
        Assert.Equal(22, shanghai?.TemperatureC);
        Assert.Equal("北京", beijing?.City);
        Assert.Equal("上海", shanghai?.City);
    }

    [Fact]
    public void WeatherProvider_reads_legacy_single_city_cache()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var snapshot = new WeatherSnapshot
        {
            City = "成都",
            TemperatureC = 18,
            AirQualityIndex = 55,
            Condition = WeatherCondition.Overcast,
            ConditionText = "阴",
            UpdatedTime = DateTime.Now,
            Error = WeatherError.None
        };
        File.WriteAllText(
            Path.Combine(dir, "weather-cache.json"),
            System.Text.Json.JsonSerializer.Serialize(snapshot));

        using var provider = new WinDock.Providers.WeatherProvider(dir);
        var loaded = provider.SnapshotFor(new WeatherSettings { City = "成都" });
        Assert.Equal(18, loaded?.TemperatureC);
        Assert.True(loaded?.IsOffline);
    }

    [Fact]
    public void WidgetChrome_scroll_bar_style_created_and_applied()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var lightStyle = WinDock.Widgets.WidgetChrome.CreateScrollBarStyle(true);
                var darkStyle = WinDock.Widgets.WidgetChrome.CreateScrollBarStyle(false);

                Assert.NotNull(lightStyle);
                Assert.NotNull(darkStyle);
                Assert.Equal(typeof(System.Windows.Controls.Primitives.ScrollBar), lightStyle.TargetType);
                Assert.Equal(typeof(System.Windows.Controls.Primitives.ScrollBar), darkStyle.TargetType);

                var scroll = new System.Windows.Controls.ScrollViewer();
                WinDock.Widgets.WidgetChrome.PaintScrollViewer(scroll, true);
                Assert.True(scroll.Resources.Contains(typeof(System.Windows.Controls.Primitives.ScrollBar)));
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
    }
}

