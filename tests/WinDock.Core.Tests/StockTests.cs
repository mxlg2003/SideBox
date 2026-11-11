using System.Text.Json;
using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class StockTests
{
    [Fact]
    public void Quote_scale_matches_eastmoney_fields()
    {
        const string json = """
            {"data":{"diff":[{"f1":3,"f2":333690,"f3":102,"f4":3370,"f12":"AAPL","f13":105,"f14":"Apple","f18":330320,"f152":2,"f124":1790971200}]}}
            """;

        var quote = Assert.Single(EastmoneyStock.ParseQuotes(json));
        Assert.Equal("us:AAPL", quote.Id);
        Assert.Equal(333.69, quote.Price);
        Assert.Equal(1.02, quote.ChangePercent);
        Assert.Equal(3.37, quote.Change);
        Assert.Equal(330.32, quote.PreviousClose);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal("Apple", quote.Name);
    }

    [Fact]
    public void Trends_bucket_to_five_minutes_and_keep_previous_close()
    {
        const string json = """
            {"data":{"preClose":330.32,"trends":[
              "2026-10-02 21:30,333.205,333.060,1,1,1,1,1",
              "2026-10-02 21:34,1,332.345,1,1,1,1,1",
              "2026-10-02 21:35,1,332.415,1,1,1,1,1"
            ]}}
            """;

        var parsed = EastmoneyStock.ParseTrends(json, 5);
        Assert.Equal(330.32, parsed.PreviousClose);
        Assert.Equal(2, parsed.Points.Count);
        Assert.Equal(332.345, parsed.Points[0].Price);
        Assert.Equal(new DateTime(2026, 10, 2, 21, 34, 0), parsed.Points[0].Time);
        Assert.Equal(332.415, parsed.Points[1].Price);
    }

    [Fact]
    public void Kline_uses_the_close()
    {
        const string json = """
            {"data":{"klines":["2026-10-02,333.260,333.690,334.540,330.610,1"]}}
            """;

        var point = Assert.Single(EastmoneyStock.ParseKlines(json).Points);
        Assert.Equal(333.690, point.Price);
        Assert.Equal(new DateTime(2026, 10, 2), point.Time);
    }

    [Fact]
    public void Search_skips_us_notes_and_keeps_the_equity()
    {
        const string json = """
            {"result":[
              {"code":"AAPL","shortName":"Apple","market":105},
              {"code":"AAPL22","shortName":"Apple Inc Notes 2022","market":105},
              {"code":"600519","shortName":"贵州茅台","market":1},
              {"code":"688825","shortName":"长鑫科技","market":1,"securityTypeName":"科创板"}
            ]}
            """;

        var hits = EastmoneyStock.ParseSearch(json);
        Assert.Equal(3, hits.Count);
        Assert.Equal("us:AAPL", hits[0].Id);
        Assert.Equal("105.AAPL", hits[0].ProviderSymbol);
        Assert.Equal("cn:600519", hits[1].Id);
        Assert.Equal("SH", hits[1].Exchange);
        Assert.Null(hits[1].Board);
        Assert.Equal("cn:688825", hits[2].Id);
        Assert.Equal("star", hits[2].Board);
        Assert.Equal("https://quote.eastmoney.com/sh600519.html", StockLinks.Url(hits[1]));
        Assert.Equal("https://finance.yahoo.com/quote/AAPL", StockLinks.Url(hits[0]));
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("科创", StockLinks.BoardText(hits[2]));
            Assert.Equal("A", StockLinks.Badge(hits[2].Market));
        }
    }

    [Fact]
    public void Settings_keep_one_symbol_and_read_the_old_watchlist()
    {
        var settings = StockWidgetSettings.Normalize(new StockWidgetSettings
        {
            Featured = new StockSymbol { Market = "US", Symbol = "aapl", Name = "Apple Inc." },
            Watchlist =
            [
                new StockSymbol { Market = "cn", Symbol = "688825", Name = "长鑫科技" },
                new StockSymbol { Market = "us", Symbol = "msft", Name = "Microsoft" }
            ],
            Range = "nope"
        });

        Assert.Equal("us:AAPL", settings.Symbol?.Id);
        Assert.Null(settings.Featured);
        Assert.Null(settings.Watchlist);
        Assert.Equal("1D", settings.Range);
        Assert.Single(StockWidgetSettings.AllSymbols(settings));

        var fromWatch = StockWidgetSettings.Normalize(new StockWidgetSettings
        {
            Watchlist = [new StockSymbol { Market = "cn", Symbol = "688825", Name = "长鑫科技", Board = "科创板" }]
        });
        Assert.Equal("cn:688825", fromWatch.Symbol?.Id);
        Assert.Equal("star", fromWatch.Symbol?.Board);
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("科创", StockLinks.BoardText(fromWatch.Symbol!));
        }
    }

    [Fact]
    public void Saved_symbol_round_trips_onto_the_dock_item()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Stocks);
        var itemId = settings.Profiles.First(profile => profile.Id == DefaultProfiles.GeneralId).Dock.Items
            .Last(item => item.WidgetId == BuiltInWidgets.Stocks).Id;
        var stock = StockWidgetSettings.Normalize(new StockWidgetSettings
        {
            Symbol = new StockSymbol { Market = "hk", Symbol = "00700", Name = "腾讯控股" },
            Range = "1M"
        });

        var saved = SettingsEditor.WithStockWidget(settings, DefaultProfiles.GeneralId, itemId, stock);
        var item = saved.Profiles.First(profile => profile.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(entry => entry.Id == itemId);

        Assert.True(StockWidgetSettings.Same(stock, item.Stocks!));
        Assert.Equal("1M", item.Stocks!.Range);
        Assert.Equal("hk:00700", item.Stocks.Symbol?.Id);
        Assert.Equal("H", StockLinks.Badge("hk"));
    }

    [Fact]
    public void Stocks_live_in_the_business_catalog()
    {
        var business = Assert.Single(WidgetCatalog.Groups, group => group.Id == "business");
        Assert.Equal([BuiltInWidgets.Stocks], business.WidgetIds);
        var lifestyle = WidgetCatalog.Groups.First(group => group.Id == "lifestyle");
        Assert.DoesNotContain(BuiltInWidgets.Stocks, lifestyle.WidgetIds);
    }

    [Fact]
    public void Display_flags_stay_on_when_an_older_save_omits_them()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var loaded = JsonSerializer.Deserialize<StockWidgetSettings>(
            """{"symbol":{"market":"cn","symbol":"688825","name":"长鑫科技"},"range":"1D"}""",
            options);
        var settings = StockWidgetSettings.Normalize(loaded);
        Assert.True(settings.ShowName);
        Assert.True(settings.ShowBadge);
        Assert.True(settings.ShowCode);
        Assert.True(settings.ShowBoard);
        Assert.True(settings.ShowPrice);
        Assert.True(settings.ShowSpark);
        Assert.True(settings.ShowChange);
        Assert.False(settings.ShowCurrency);
        Assert.True(settings.ShowHint);
        Assert.True(settings.ShowAsOf);
        Assert.True(settings.ShowPrevious);
        Assert.True(settings.ShowSample);

        var hidden = settings with { ShowSpark = false, ShowBoard = false, ShowCurrency = true, ShowHint = false };
        var again = StockWidgetSettings.Normalize(JsonSerializer.Deserialize<StockWidgetSettings>(
            JsonSerializer.Serialize(hidden, options),
            options));
        Assert.False(again.ShowSpark);
        Assert.False(again.ShowBoard);
        Assert.True(again.ShowCurrency);
        Assert.False(again.ShowHint);
        Assert.True(again.ShowName);
        Assert.True(StockWidgetSettings.Same(hidden, again));
    }

    [Fact]
    public void Percent_and_tone_follow_the_international_colors()
    {
        Assert.Equal("+1.02%", StockFormat.Percent(1.02));
        Assert.Equal("-3.36%", StockFormat.Percent(-3.36));
        Assert.Equal(DockTokens.StockUp, StockFormat.Tone(0.24));
        Assert.Equal(DockTokens.StockDown, StockFormat.Tone(-3.36));
        Assert.Equal(DockTokens.StockFlat, StockFormat.Tone(0));
        Assert.Equal("333.69 USD", StockFormat.Money(333.69, "USD"));
    }

    [Fact]
    public void Stocks_widget_supports_density_modes()
    {
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Stocks));
    }
}
