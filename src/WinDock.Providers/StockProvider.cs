using System.Net;
using System.Net.Http;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class StockProvider : IStockProvider, IDisposable
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
    private static readonly TimeSpan FreshSeries = TimeSpan.FromSeconds(45);

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, StockQuote> _quotes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StockSeries> _series = new(StringComparer.Ordinal);
    private StockSymbol[] _watched = [];
    private Timer? _timer;
    private bool _started;
    private bool _quiet;
    private bool _disposed;
    private int _refreshing;
    private bool _refreshAgain;

    public event EventHandler? Changed;

    public StockQuote? QuoteFor(string id)
    {
        lock (_gate)
        {
            return _quotes.TryGetValue(id, out var quote) ? quote : null;
        }
    }

    public StockSeries? SeriesFor(string id, StockRange range)
    {
        lock (_gate)
        {
            return _series.TryGetValue(Key(id, range), out var series) ? series : null;
        }
    }

    public void Watch(IReadOnlyList<StockSymbol> symbols)
    {
        var next = new List<StockSymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            var clean = StockWidgetSettings.Normalize(new StockWidgetSettings { Symbol = symbol }).Symbol;
            if (clean is null || !seen.Add(clean.Id))
            {
                continue;
            }

            next.Add(clean);
            if (next.Count >= 24)
            {
                break;
            }
        }

        lock (_gate)
        {
            _watched = next.ToArray();
        }

        _ = RefreshQuotesAsync();
    }

    public void Start()
    {
        if (_disposed || _started)
        {
            return;
        }

        _started = true;
        ArmTimer();
    }

    public void Stop()
    {
        _started = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void SetQuiet(bool quiet)
    {
        if (_quiet == quiet)
        {
            return;
        }

        _quiet = quiet;
        if (_started)
        {
            ArmTimer();
        }
    }

    public async Task RefreshQuotesAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            _refreshAgain = true;
            return;
        }

        try
        {
            do
            {
                _refreshAgain = false;
                await FetchQuotesAsync(cancellationToken).ConfigureAwait(false);
            }
            while (_refreshAgain && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    public async Task<StockSeries?> LoadSeriesAsync(StockSymbol symbol, StockRange range, bool force = false, CancellationToken cancellationToken = default)
    {
        var clean = StockWidgetSettings.Normalize(new StockWidgetSettings { Symbol = symbol }).Symbol;
        if (clean is null)
        {
            return null;
        }

        var key = Key(clean.Id, range);
        if (!force)
        {
            lock (_gate)
            {
                if (_series.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow - cached.FetchedAt < FreshSeries)
                {
                    return cached;
                }
            }
        }

        try
        {
            var url = SeriesUrl(clean, range);
            var json = await GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                return SeriesFor(clean.Id, range);
            }

            var parsed = StockRanges.Intraday(range)
                ? EastmoneyStock.ParseTrends(json, StockRanges.BucketMinutes(range))
                : EastmoneyStock.ParseKlines(json);
            var series = new StockSeries
            {
                Id = clean.Id,
                Range = range,
                Points = parsed.Points,
                PreviousClose = parsed.PreviousClose,
                FetchedAt = DateTimeOffset.UtcNow
            };
            lock (_gate)
            {
                _series[key] = series;
                if (_series.Count > 24)
                {
                    var oldest = _series.OrderBy(item => item.Value.FetchedAt).First().Key;
                    _series.Remove(oldest);
                }
            }

            if (!_disposed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return series;
        }
        catch (Exception)
        {
            return SeriesFor(clean.Id, range);
        }
    }

    public async Task<IReadOnlyList<StockSymbol>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var keyword = (query ?? "").Trim();
        if (keyword.Length == 0)
        {
            return [];
        }

        try
        {
            var url = "https://search-codetable.eastmoney.com/codetable/search/web?client=web&clientType=webSuggest&clientVersion=lastest&keyword="
                + Uri.EscapeDataString(keyword)
                + "&pageIndex=1&pageSize=20&securityFilter=";
            var json = await GetAsync(url, cancellationToken).ConfigureAwait(false);
            return json is null ? [] : EastmoneyStock.ParseSearch(json);
        }
        catch (Exception)
        {
            return [];
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }

    private async Task FetchQuotesAsync(CancellationToken cancellationToken)
    {
        StockSymbol[] symbols;
        lock (_gate)
        {
            symbols = _watched.ToArray();
        }

        if (symbols.Length == 0 || _disposed)
        {
            return;
        }

        try
        {
            var secids = string.Join(",", symbols.Select(item => Uri.EscapeDataString(StockLinks.SecId(item))));
            var url = Host(false)
                + "/api/qt/ulist/get?secids=" + secids
                + "&fields=" + EastmoneyStock.QuoteFields
                + "&invt=2&ut=" + EastmoneyStock.Ut
                + "&pn=1&pz=" + symbols.Length
                + "&np=1&_=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var json = await GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                MarkQuotesStale();
                return;
            }

            var quotes = EastmoneyStock.ParseQuotes(json);
            if (quotes.Count == 0)
            {
                MarkQuotesStale();
                return;
            }

            lock (_gate)
            {
                var returned = new HashSet<string>(quotes.Select(quote => quote.Id), StringComparer.Ordinal);
                foreach (var quote in quotes)
                {
                    _quotes[quote.Id] = quote with { Stale = false };
                }

                foreach (var symbol in symbols)
                {
                    if (returned.Contains(symbol.Id) || !_quotes.TryGetValue(symbol.Id, out var previous) || previous.Stale)
                    {
                        continue;
                    }

                    _quotes[symbol.Id] = previous with { Stale = true };
                }
            }

            if (!_disposed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception)
        {
            MarkQuotesStale();
        }
    }

    private void MarkQuotesStale()
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var pair in _quotes.ToArray())
            {
                if (pair.Value.Stale)
                {
                    continue;
                }

                _quotes[pair.Key] = pair.Value with { Stale = true };
                changed = true;
            }
        }

        if (changed && !_disposed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string SeriesUrl(StockSymbol symbol, StockRange range)
    {
        var secid = Uri.EscapeDataString(StockLinks.SecId(symbol));
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (StockRanges.Intraday(range))
        {
            var days = range == StockRange.FiveDay ? 5 : 1;
            return Host(true)
                + "/api/qt/stock/trends2/get?secid=" + secid
                + "&fields1=f1,f2,f3,f4,f5,f6,f7,f8,f9,f10,f11,f12,f13,f14,f17"
                + "&fields2=f51,f52,f53,f54,f55,f56,f57,f58"
                + "&ut=" + EastmoneyStock.Ut
                + "&ndays=" + days
                + "&iscr=0&iscca=0&_=" + stamp;
        }

        var klt = range == StockRange.FiveYear ? 102 : 101;
        var limit = range switch
        {
            StockRange.Month => 23,
            StockRange.SixMonth => 132,
            StockRange.Year => 260,
            StockRange.FiveYear => 270,
            _ => 23
        };
        return Host(true)
            + "/api/qt/stock/kline/get?secid=" + secid
            + "&klt=" + klt
            + "&fqt=1&lmt=" + limit
            + "&end=20500101"
            + "&fields1=f1,f2,f3,f4,f5,f6"
            + "&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61"
            + "&ut=" + EastmoneyStock.Ut
            + "&_=" + stamp;
    }

    private static string Host(bool history)
    {
        var n = Random.Shared.Next(1, 100);
        return history
            ? "https://" + n + ".push2his.eastmoney.com"
            : "https://" + n + ".push2.eastmoney.com";
    }

    private static async Task<string?> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ArmTimer()
    {
        _timer?.Dispose();
        var interval = _quiet ? TimeSpan.FromSeconds(90) : TimeSpan.FromSeconds(20);
        _timer = new Timer(_ => _ = RefreshQuotesAsync(), null, interval, interval);
    }

    private static string Key(string id, StockRange range) => id + "|" + StockRanges.Key(range);
}
