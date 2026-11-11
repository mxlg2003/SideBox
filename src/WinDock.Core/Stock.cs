using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDock.Core;

public enum StockRange
{
    Day,
    FiveDay,
    Month,
    SixMonth,
    Year,
    FiveYear
}

/// <summary>One saved symbol. Ids are <c>market:SYMBOL</c>, matching the East Money secid map used by Batman stocks.</summary>
public sealed record StockSymbol
{
    public string Id { get; init; } = "";

    public string Market { get; init; } = "";

    public string Symbol { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Exchange { get; init; }

    /// <summary>East Money secid, for example <c>105.AAPL</c>.</summary>
    public string? ProviderSymbol { get; init; }

    /// <summary>Canonical board key such as star, chinext, or bse. Display text comes from <see cref="StockLinks.BoardText"/>.</summary>
    public string? Board { get; init; }
}

public sealed record StockWidgetSettings
{
    /// <summary>The one symbol this chip follows.</summary>
    public StockSymbol? Symbol { get; init; }

    /// <summary>Older saves stored the shown stock here. Kept so those files still open one symbol.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StockSymbol? Featured { get; init; }

    /// <summary>Older saves stored extra symbols here. The first is used only when <see cref="Symbol"/> and <see cref="Featured"/> are empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<StockSymbol>? Watchlist { get; init; }

    public string Range { get; init; } = "1D";

    public bool ShowName { get; init; } = true;

    public bool ShowBadge { get; init; } = true;

    public bool ShowCode { get; init; } = true;

    public bool ShowBoard { get; init; } = true;

    public bool ShowPrice { get; init; } = true;

    public bool ShowSpark { get; init; } = true;

    public bool ShowChange { get; init; } = true;

    /// <summary>Append the currency to the island price. The quote page always shows it.</summary>
    public bool ShowCurrency { get; init; }

    public bool ShowHint { get; init; } = true;

    public bool ShowAsOf { get; init; } = true;

    public bool ShowPrevious { get; init; } = true;

    public bool ShowSample { get; init; } = true;

    public static StockWidgetSettings Default { get; } = new();

    public static StockWidgetSettings Normalize(StockWidgetSettings? settings)
    {
        var value = settings ?? new StockWidgetSettings();
        var symbol = Clean(value.Symbol) ?? Clean(value.Featured);
        if (symbol is null)
        {
            foreach (var item in value.Watchlist ?? [])
            {
                symbol = Clean(item);
                if (symbol is not null)
                {
                    break;
                }
            }
        }

        return value with
        {
            Symbol = symbol,
            Featured = null,
            Watchlist = null,
            Range = StockRanges.Key(StockRanges.Parse(value.Range))
        };
    }

    public static bool Same(StockWidgetSettings left, StockWidgetSettings right) =>
        string.Equals(left.Range, right.Range, StringComparison.Ordinal)
        && SameSymbol(left.Symbol, right.Symbol)
        && left.ShowName == right.ShowName
        && left.ShowBadge == right.ShowBadge
        && left.ShowCode == right.ShowCode
        && left.ShowBoard == right.ShowBoard
        && left.ShowPrice == right.ShowPrice
        && left.ShowSpark == right.ShowSpark
        && left.ShowChange == right.ShowChange
        && left.ShowCurrency == right.ShowCurrency
        && left.ShowHint == right.ShowHint
        && left.ShowAsOf == right.ShowAsOf
        && left.ShowPrevious == right.ShowPrevious
        && left.ShowSample == right.ShowSample;

    public static IReadOnlyList<StockSymbol> AllSymbols(StockWidgetSettings settings) =>
        settings.Symbol is { Id.Length: > 0 } symbol ? [symbol] : [];

    private static bool SameSymbol(StockSymbol? left, StockSymbol? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.ProviderSymbol, right.ProviderSymbol, StringComparison.Ordinal)
            && string.Equals(left.Exchange, right.Exchange, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Board, right.Board, StringComparison.Ordinal);
    }

    private static StockSymbol? Clean(StockSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        var market = StockLinks.NormalizeMarket(symbol.Market);
        var code = (symbol.Symbol ?? "").Trim();
        if (market is null || code.Length == 0 || code.Length > 16)
        {
            return null;
        }

        if (market == "us")
        {
            code = code.ToUpperInvariant();
        }

        var name = (symbol.Name ?? "").Trim();
        if (name.Length > 80)
        {
            name = name[..80];
        }

        var provider = (symbol.ProviderSymbol ?? "").Trim();
        if (provider.Length > 32)
        {
            provider = provider[..32];
        }

        var exchange = (symbol.Exchange ?? "").Trim();
        var exchangeText = exchange.Length == 0 ? null : exchange.ToUpperInvariant();
        return new StockSymbol
        {
            Id = market + ":" + code,
            Market = market,
            Symbol = code,
            Name = name,
            Exchange = exchangeText,
            ProviderSymbol = provider.Length == 0 ? null : provider,
            Board = StockLinks.BoardKey(symbol.Board, market, code, exchangeText)
        };
    }
}

public static class StockRanges
{
    public static IReadOnlyList<StockRange> All { get; } =
    [
        StockRange.Day,
        StockRange.FiveDay,
        StockRange.Month,
        StockRange.SixMonth,
        StockRange.Year,
        StockRange.FiveYear
    ];

    public static string Key(StockRange range) => range switch
    {
        StockRange.FiveDay => "5D",
        StockRange.Month => "1M",
        StockRange.SixMonth => "6M",
        StockRange.Year => "1Y",
        StockRange.FiveYear => "5Y",
        _ => "1D"
    };

    public static StockRange Parse(string? key) => key?.Trim().ToUpperInvariant() switch
    {
        "5D" => StockRange.FiveDay,
        "1M" => StockRange.Month,
        "6M" => StockRange.SixMonth,
        "1Y" => StockRange.Year,
        "5Y" => StockRange.FiveYear,
        _ => StockRange.Day
    };

    public static int BucketMinutes(StockRange range) => range switch
    {
        StockRange.Day => 5,
        StockRange.FiveDay => 30,
        _ => 0
    };

    public static bool Intraday(StockRange range) =>
        range is StockRange.Day or StockRange.FiveDay;

    public static bool ShowPreviousClose(StockRange range) => range == StockRange.Day;
}

public sealed record StockQuote
{
    public required string Id { get; init; }

    public required string Symbol { get; init; }

    public string? Name { get; init; }

    public double? Price { get; init; }

    public double? Change { get; init; }

    public double? ChangePercent { get; init; }

    public double? PreviousClose { get; init; }

    public string Currency { get; init; } = "USD";

    public DateTimeOffset AsOf { get; init; }

    public bool Stale { get; init; }
}

public sealed record StockPoint(DateTime Time, double Price);

public sealed record StockSeries
{
    public required string Id { get; init; }

    public required StockRange Range { get; init; }

    public IReadOnlyList<StockPoint> Points { get; init; } = [];

    public double? PreviousClose { get; init; }

    public DateTimeOffset FetchedAt { get; init; }
}

public static class StockFormat
{
    public static string Price(double? value)
    {
        if (value is not double number || !double.IsFinite(number))
        {
            return "—";
        }

        var digits = Math.Abs(number) >= 1 ? 2 : 4;
        return number.ToString("0." + new string('0', digits), CultureInfo.InvariantCulture);
    }

    public static string Percent(double? value)
    {
        if (value is not double number || !double.IsFinite(number))
        {
            return "—";
        }

        var sign = number > 0 ? "+" : "";
        return sign + number.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    public static string Money(double? value, string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? Price(value) : Price(value) + " " + currency;

    public static uint Tone(double? change)
    {
        if (change is not double number || !double.IsFinite(number) || Math.Abs(number) < 0.0005)
        {
            return DockTokens.StockFlat;
        }

        return number > 0 ? DockTokens.StockUp : DockTokens.StockDown;
    }
}

public static class StockLinks
{
    public static string? NormalizeMarket(string? market)
    {
        var value = (market ?? "").Trim().ToLowerInvariant();
        return value is "cn" or "hk" or "us" ? value : null;
    }

    public static string Currency(string? market) => market switch
    {
        "hk" => "HKD",
        "cn" => "CNY",
        _ => "USD"
    };

    public static string Badge(string? market) => market switch
    {
        "hk" => "H",
        "us" => "US",
        _ => "A"
    };

    /// <summary>Board chip key. Prefers a stored label, then infers 科创 / 创业 / 北证 from the code.</summary>
    public static string? BoardKey(string? raw, string market, string symbol, string? exchange)
    {
        var fromRaw = KeyFromText(raw);
        if (fromRaw == "main" && market == "cn")
        {
            fromRaw = null;
        }

        return fromRaw ?? InferBoard(market, symbol, exchange);
    }

    public static string? BoardText(StockSymbol symbol)
    {
        var key = string.IsNullOrWhiteSpace(symbol.Board)
            ? InferBoard(symbol.Market, symbol.Symbol, symbol.Exchange)
            : symbol.Board;
        return key switch
        {
            "star" => UiText.StockBoardStar,
            "chinext" => UiText.StockBoardChiNext,
            "bse" => UiText.StockBoardBse,
            "sme" => UiText.StockBoardSme,
            "gem" => "GEM",
            "nasd" => "NASD",
            "nyse" => "NYSE",
            "amex" => "AMEX",
            "ndx" => "NDX",
            "djia" => "DJIA",
            "spx" => "SPX",
            "main" or null or "" => null,
            _ => key
        };
    }

    private static string? KeyFromText(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0 || IsBoardNoise(value))
        {
            return null;
        }

        if (value.Contains("科创", StringComparison.Ordinal) || value.Contains("STAR", StringComparison.OrdinalIgnoreCase))
        {
            return "star";
        }

        if (value.Contains("创业", StringComparison.Ordinal) || value.Contains("ChiNext", StringComparison.OrdinalIgnoreCase))
        {
            return "chinext";
        }

        if (value.Contains("北证", StringComparison.Ordinal) || value.Contains("北交", StringComparison.Ordinal))
        {
            return "bse";
        }

        if (value.Contains("中小", StringComparison.Ordinal))
        {
            return "sme";
        }

        if (value.Contains("主板", StringComparison.Ordinal))
        {
            return "main";
        }

        if (value.Equals("GEM", StringComparison.OrdinalIgnoreCase))
        {
            return "gem";
        }

        if (value.Contains("NASDAQ", StringComparison.OrdinalIgnoreCase) || value.Contains("NASD", StringComparison.OrdinalIgnoreCase))
        {
            return "nasd";
        }

        if (value.Contains("NYSE", StringComparison.OrdinalIgnoreCase))
        {
            return "nyse";
        }

        if (value.Contains("AMEX", StringComparison.OrdinalIgnoreCase))
        {
            return "amex";
        }

        return value.Length <= 6 ? value : null;
    }

    private static bool IsBoardNoise(string value) =>
        value is "A股" or "股票" or "证券"
        || value.Equals("stock", StringComparison.OrdinalIgnoreCase)
        || value.Equals("equity", StringComparison.OrdinalIgnoreCase);

    private static string? InferBoard(string market, string symbol, string? exchange)
    {
        if (market == "cn")
        {
            if (symbol.StartsWith("688", StringComparison.Ordinal) || symbol.StartsWith("689", StringComparison.Ordinal))
            {
                return "star";
            }

            if (symbol.StartsWith("300", StringComparison.Ordinal) || symbol.StartsWith("301", StringComparison.Ordinal))
            {
                return "chinext";
            }

            if (symbol.StartsWith('8') || symbol.StartsWith('4')
                || string.Equals(exchange, "BJ", StringComparison.OrdinalIgnoreCase))
            {
                return "bse";
            }

            if (symbol.StartsWith("002", StringComparison.Ordinal))
            {
                return "sme";
            }

            return null;
        }

        if (market == "us")
        {
            var code = symbol.ToUpperInvariant();
            if (code is "QQQ" or "QQQM" or "TQQQ" or "SQQQ" or "QLD" or "QID")
            {
                return "ndx";
            }

            if (code is "DIA" or "UDOW" or "SDOW" or "DDM" or "DOG")
            {
                return "djia";
            }

            if (code is "SPY" or "VOO" or "IVV" or "SPX" or "SSO" or "SDS" or "UPRO" or "SPXU")
            {
                return "spx";
            }

            var listed = (exchange ?? "").ToUpperInvariant();
            if (listed.Contains("NASDAQ", StringComparison.Ordinal) || listed.Contains("NASD", StringComparison.Ordinal) || listed is "NMS" or "NSQ")
            {
                return "nasd";
            }

            if (listed.Contains("NYSE", StringComparison.Ordinal) || listed == "NYQ")
            {
                return "nyse";
            }

            if (listed.Contains("AMEX", StringComparison.Ordinal) || listed is "ASE" or "ARCA" or "PCX")
            {
                return "amex";
            }
        }

        if (market == "hk" && symbol.StartsWith("08", StringComparison.Ordinal))
        {
            return "gem";
        }

        return null;
    }

    public static string SecId(StockSymbol symbol)
    {
        var provider = (symbol.ProviderSymbol ?? "").Trim();
        if (provider.Contains('.', StringComparison.Ordinal))
        {
            return provider;
        }

        var code = symbol.Symbol.Trim();
        if (symbol.Market == "us")
        {
            return "105." + code.ToUpperInvariant();
        }

        if (symbol.Market == "hk")
        {
            return "116." + code;
        }

        var exchange = (symbol.Exchange ?? InferCnExchange(code)).ToUpperInvariant();
        return exchange is "SH" or "SSE" ? "1." + code : "0." + code;
    }

    public static string Url(StockSymbol symbol)
    {
        if (symbol.Market == "us")
        {
            return "https://finance.yahoo.com/quote/" + Uri.EscapeDataString(symbol.Symbol);
        }

        if (symbol.Market == "hk")
        {
            return "https://quote.eastmoney.com/hk/" + Uri.EscapeDataString(symbol.Symbol) + ".html";
        }

        var exchange = (symbol.Exchange ?? InferCnExchange(symbol.Symbol)).ToLowerInvariant();
        var prefix = exchange is "sh" or "sse" ? "sh" : exchange is "bj" or "bse" ? "bj" : "sz";
        return "https://quote.eastmoney.com/" + prefix + symbol.Symbol + ".html";
    }

    public static string InferCnExchange(string symbol)
    {
        if (symbol.StartsWith('6') || symbol.StartsWith('5') || symbol.StartsWith('9'))
        {
            return "SH";
        }

        if (symbol.StartsWith('4') || symbol.StartsWith('8'))
        {
            return "BJ";
        }

        return "SZ";
    }

    /// <summary>East Money market number to cn/hk/us. Same map as the Batman stocks module.</summary>
    public static (string? Market, string? Exchange) FromEastmoneyMarket(int marketNumber)
    {
        if (marketNumber is 0 or 1)
        {
            return ("cn", marketNumber == 1 ? "SH" : "SZ");
        }

        if (marketNumber == 90)
        {
            return ("cn", "BJ");
        }

        if (marketNumber is 116 or 128)
        {
            return ("hk", null);
        }

        if (marketNumber is 105 or 106 or 107)
        {
            return ("us", null);
        }

        return (null, null);
    }
}

public sealed record StockSeriesParse(IReadOnlyList<StockPoint> Points, double? PreviousClose);

/// <summary>Pure East Money payload parsing. Quote fields use the f1/f152 scale from the Batman stocks client.</summary>
public static class EastmoneyStock
{
    public const string Ut = "fa5fd1943c7b386f172d6893dbfba10b";

    public const string QuoteFields = "f1,f2,f3,f4,f12,f13,f14,f18,f152,f124";

    public static IReadOnlyList<StockQuote> ParseQuotes(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("diff", out var diff))
        {
            return [];
        }

        var rows = diff.ValueKind switch
        {
            JsonValueKind.Array => diff.EnumerateArray().ToArray(),
            JsonValueKind.Object => diff.EnumerateObject().Select(item => item.Value).ToArray(),
            _ => []
        };
        var quotes = new List<StockQuote>(rows.Length);
        foreach (var row in rows)
        {
            var quote = MapQuote(row);
            if (quote is not null)
            {
                quotes.Add(quote);
            }
        }

        return quotes;
    }

    public static StockSeriesParse ParseTrends(string json, int bucketMinutes)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            return new StockSeriesParse([], null);
        }

        var previous = Number(data, "preClose") ?? Number(data, "prePrice");
        var points = ReadBars(data, "trends", bucketMinutes);
        return new StockSeriesParse(points, previous);
    }

    public static StockSeriesParse ParseKlines(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            return new StockSeriesParse([], null);
        }

        return new StockSeriesParse(ReadBars(data, "klines", 0), null);
    }

    public static IReadOnlyList<StockSymbol> ParseSearch(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var hits = new List<StockSymbol>();
        foreach (var row in result.EnumerateArray())
        {
            var symbol = MapSearch(row);
            if (symbol is not null)
            {
                hits.Add(symbol);
            }

            if (hits.Count >= 12)
            {
                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<StockPoint> Bucket(IReadOnlyList<StockPoint> points, int minutes)
    {
        if (minutes <= 1 || points.Count == 0)
        {
            return points;
        }

        var result = new List<StockPoint>();
        DateTime? slot = null;
        var last = points[0];
        foreach (var point in points)
        {
            var minute = point.Time.Minute / minutes * minutes;
            var next = new DateTime(point.Time.Year, point.Time.Month, point.Time.Day, point.Time.Hour, minute, 0);
            if (slot is null)
            {
                slot = next;
                last = point;
                continue;
            }

            if (next != slot)
            {
                result.Add(last);
                slot = next;
            }

            last = point;
        }

        result.Add(last);
        return result;
    }

    private static StockQuote? MapQuote(JsonElement row)
    {
        var code = Text(row, "f12");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var marketNumber = Number(row, "f13");
        if (marketNumber is null)
        {
            return null;
        }

        var mapped = StockLinks.FromEastmoneyMarket((int)marketNumber.Value);
        if (mapped.Market is null)
        {
            return null;
        }

        var symbol = mapped.Market == "us" ? code.Trim().ToUpperInvariant() : code.Trim();
        var valueDigits = Number(row, "f1");
        var ratioDigits = Number(row, "f152");
        var unix = Number(row, "f124");
        var asOf = unix is > 1_000_000_000
            ? DateTimeOffset.FromUnixTimeSeconds((long)unix.Value)
            : DateTimeOffset.UtcNow;
        return new StockQuote
        {
            Id = mapped.Market + ":" + symbol,
            Symbol = symbol,
            Name = Text(row, "f14"),
            Price = Scale(Number(row, "f2"), valueDigits),
            Change = Scale(Number(row, "f4"), valueDigits),
            ChangePercent = Scale(Number(row, "f3"), ratioDigits),
            PreviousClose = Scale(Number(row, "f18"), valueDigits),
            Currency = StockLinks.Currency(mapped.Market),
            AsOf = asOf
        };
    }

    private static StockSymbol? MapSearch(JsonElement row)
    {
        var code = Text(row, "code");
        var marketNumber = Number(row, "market");
        if (string.IsNullOrWhiteSpace(code) || marketNumber is null)
        {
            return null;
        }

        var mapped = StockLinks.FromEastmoneyMarket((int)marketNumber.Value);
        if (mapped.Market is null)
        {
            return null;
        }

        var symbol = mapped.Market == "us" ? code.Trim().ToUpperInvariant() : code.Trim();
        if (mapped.Market == "us" && symbol.Any(char.IsDigit))
        {
            return null;
        }

        var market = (int)marketNumber.Value;
        return StockWidgetSettings.Normalize(new StockWidgetSettings
        {
            Symbol = new StockSymbol
            {
                Market = mapped.Market,
                Symbol = symbol,
                Name = Text(row, "shortName") ?? symbol,
                Exchange = mapped.Exchange,
                ProviderSymbol = market + "." + code.Trim(),
                Board = Text(row, "securityTypeName")
            }
        }).Symbol;
    }

    private static IReadOnlyList<StockPoint> ReadBars(JsonElement data, string name, int bucketMinutes)
    {
        if (!data.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var points = new List<StockPoint>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var parts = (item.GetString() ?? "").Split(',');
            if (parts.Length < 2 || !TryTime(parts[0], out var time))
            {
                continue;
            }

            var raw = parts.Length > 2 ? parts[2] : parts[1];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) || !double.IsFinite(price))
            {
                continue;
            }

            points.Add(new StockPoint(time, price));
        }

        return Bucket(points, bucketMinutes);
    }

    private static bool TryTime(string text, out DateTime time) =>
        DateTime.TryParseExact(
            text.Trim(),
            ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);

    public static double? Scale(double? raw, double? precision)
    {
        if (raw is not double value || !double.IsFinite(value))
        {
            return null;
        }

        if (precision is not double digits || !double.IsFinite(digits))
        {
            return value;
        }

        var places = (int)digits;
        if (places is < 0 or > 8)
        {
            return value;
        }

        return value / Math.Pow(10, places);
    }

    private static double? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
