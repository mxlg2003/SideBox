using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class WeatherProvider : IWeatherProvider, IDisposable
{
    private const string GeoJsUrl = "https://get.geojs.io/v1/ip/geo.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly string _cacheFile;
    private readonly object _gate = new();
    private System.Threading.Timer? _refreshTimer;
    private WeatherSnapshot? _current;
    private WeatherSettings _lastSettings = WeatherSettings.Default;
    private bool _started;
    private bool _isDisposed;

    public WeatherSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool AllowAutoLocate { get; set; }

    public event EventHandler? Changed;

    public WeatherProvider(string? cacheDirectory = null)
    {
        var dir = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinDock");
        Directory.CreateDirectory(dir);
        _cacheFile = Path.Combine(dir, "weather-cache.json");
        TryLoadDiskCache();
    }

    public void Start()
    {
        if (_isDisposed || _started)
        {
            return;
        }

        _started = true;
        _refreshTimer = new System.Threading.Timer(
            OnBackgroundTimer,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(30));
    }

    public void Stop()
    {
        _started = false;
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    private void OnBackgroundTimer(object? state)
    {
        if (_isDisposed || !_started)
        {
            return;
        }

        _ = RefreshAsync(_lastSettings, force: false);
    }

    public async Task<WeatherSnapshot?> RefreshAsync(
        WeatherSettings settings,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        settings = WeatherSettings.Normalize(settings);
        _lastSettings = settings;

        WeatherSnapshot? existing;
        lock (_gate)
        {
            existing = _current;
        }

        if (!force
            && existing is { Error: WeatherError.None, IsOffline: false }
            && CitiesMatch(existing.City, settings)
            && (DateTime.Now - existing.UpdatedTime).TotalMinutes < 15)
        {
            return existing;
        }

        try
        {
            var snapshot = await FetchFromApiAsync(settings, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _current = snapshot;
            }

            if (snapshot.Error == WeatherError.None)
            {
                SaveDiskCache(snapshot);
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            WeatherSnapshot fallback;
            lock (_gate)
            {
                if (_current is { Error: WeatherError.None })
                {
                    _current = _current with { IsOffline = true };
                    fallback = _current;
                }
                else
                {
                    fallback = ErrorSnapshot(WeatherError.Network, settings.City);
                    _current = fallback;
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return fallback;
        }
    }

    private async Task<WeatherSnapshot> FetchFromApiAsync(WeatherSettings settings, CancellationToken cancellationToken)
    {
        double lat;
        double lon;
        string cityName;

        if (string.IsNullOrWhiteSpace(settings.City))
        {
            if (!AllowAutoLocate || !settings.AutoLocate)
            {
                return ErrorSnapshot(WeatherError.NeedCity, "");
            }

            var located = await TryLocateAsync(cancellationToken).ConfigureAwait(false);
            if (located is null)
            {
                return ErrorSnapshot(WeatherError.LocateFailed, "");
            }

            (lat, lon, cityName) = located.Value;
        }
        else
        {
            cityName = settings.City;
            var geo = await TryGeocodeAsync(settings.City, cancellationToken).ConfigureAwait(false);
            if (geo is null)
            {
                return ErrorSnapshot(WeatherError.CityNotFound, settings.City);
            }

            (lat, lon, cityName) = geo.Value;
        }

        var weatherUrl =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString("F4", CultureInfo.InvariantCulture)}&longitude={lon.ToString("F4", CultureInfo.InvariantCulture)}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&hourly=temperature_2m,weather_code,precipitation_probability&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=7";
        var weatherJson = await Http.GetStringAsync(weatherUrl, cancellationToken).ConfigureAwait(false);

        var aqi = -1;
        try
        {
            var aqiUrl =
                $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={lat.ToString("F4", CultureInfo.InvariantCulture)}&longitude={lon.ToString("F4", CultureInfo.InvariantCulture)}&current=us_aqi";
            var aqiJson = await Http.GetStringAsync(aqiUrl, cancellationToken).ConfigureAwait(false);
            using var aqiDoc = JsonDocument.Parse(aqiJson);
            if (aqiDoc.RootElement.TryGetProperty("current", out var aqiCurrent)
                && aqiCurrent.TryGetProperty("us_aqi", out var aqiVal)
                && aqiVal.ValueKind is JsonValueKind.Number)
            {
                aqi = aqiVal.GetInt32();
            }
        }
        catch (Exception)
        {
            aqi = -1;
        }

        using var wDoc = JsonDocument.Parse(weatherJson);
        var wRoot = wDoc.RootElement;
        var currentElem = wRoot.GetProperty("current");

        var tempC = currentElem.GetProperty("temperature_2m").GetDouble();
        var feelsLike = currentElem.GetProperty("apparent_temperature").GetDouble();
        var humidity = currentElem.GetProperty("relative_humidity_2m").GetInt32();
        var windSpeed = currentElem.GetProperty("wind_speed_10m").GetDouble();
        var weatherCode = currentElem.GetProperty("weather_code").GetInt32();

        var (condition, _) = WeatherGlyphMapper.FromWmoCode(weatherCode);
        var conditionText = WeatherGlyphMapper.ConditionLocalized(condition);

        var hourlyList = new List<WeatherHourlyForecast>();
        if (wRoot.TryGetProperty("hourly", out var hourlyElem))
        {
            var times = hourlyElem.GetProperty("time");
            var temps = hourlyElem.GetProperty("temperature_2m");
            var codes = hourlyElem.GetProperty("weather_code");
            var precip = hourlyElem.GetProperty("precipitation_probability");

            var now = DateTime.Now;
            var count = times.GetArrayLength();
            for (var i = 0; i < count; i++)
            {
                if (DateTime.TryParse(times[i].GetString(), out var t) && t >= now.AddHours(-1) && hourlyList.Count < 24)
                {
                    hourlyList.Add(new WeatherHourlyForecast
                    {
                        Time = t,
                        TemperatureC = temps[i].GetDouble(),
                        Condition = WeatherGlyphMapper.FromWmoCode(codes[i].GetInt32()).Condition,
                        PrecipitationProbability = precip[i].ValueKind == JsonValueKind.Number ? precip[i].GetInt32() : 0
                    });
                }
            }
        }

        var dailyList = new List<WeatherDailyForecast>();
        if (wRoot.TryGetProperty("daily", out var dailyElem))
        {
            var dates = dailyElem.GetProperty("time");
            var maxTemps = dailyElem.GetProperty("temperature_2m_max");
            var minTemps = dailyElem.GetProperty("temperature_2m_min");
            var codes = dailyElem.GetProperty("weather_code");

            var count = dates.GetArrayLength();
            for (var i = 0; i < count; i++)
            {
                if (DateOnly.TryParse(dates[i].GetString(), out var d))
                {
                    var (c, _) = WeatherGlyphMapper.FromWmoCode(codes[i].GetInt32());
                    dailyList.Add(new WeatherDailyForecast
                    {
                        Date = d,
                        TempMaxC = maxTemps[i].GetDouble(),
                        TempMinC = minTemps[i].GetDouble(),
                        Condition = c,
                        ConditionText = WeatherGlyphMapper.ConditionLocalized(c)
                    });
                }
            }
        }

        return new WeatherSnapshot
        {
            City = cityName,
            TemperatureC = tempC,
            FeelsLikeC = feelsLike,
            Humidity = humidity,
            WindSpeedKmh = windSpeed,
            AirQualityIndex = aqi,
            Condition = condition,
            ConditionText = conditionText,
            UpdatedTime = DateTime.Now,
            IsOffline = false,
            Error = WeatherError.None,
            Hourly = hourlyList,
            Daily = dailyList
        };
    }

    private static async Task<(double Lat, double Lon, string City)?> TryLocateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await Http.GetStringAsync(GeoJsUrl, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!TryReadDouble(root, "latitude", out var lat) || !TryReadDouble(root, "longitude", out var lon))
            {
                return null;
            }

            var city = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(city))
            {
                city = root.TryGetProperty("region", out var region) ? region.GetString() ?? "" : "";
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                return null;
            }

            return (lat, lon, city);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<(double Lat, double Lon, string City)?> TryGeocodeAsync(string city, CancellationToken cancellationToken)
    {
        try
        {
            var encoded = Uri.EscapeDataString(city);
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=zh";
            var geoJson = await Http.GetStringAsync(geoUrl, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(geoJson);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var lat = first.GetProperty("latitude").GetDouble();
            var lon = first.GetProperty("longitude").GetDouble();
            var name = first.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? city : city;
            return (lat, lon, name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryReadDouble(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var el))
        {
            return false;
        }

        if (el.ValueKind == JsonValueKind.Number)
        {
            return el.TryGetDouble(out value);
        }

        return el.ValueKind == JsonValueKind.String
               && double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool CitiesMatch(string snapshotCity, WeatherSettings settings)
    {
        if (settings.AutoLocate && string.IsNullOrWhiteSpace(settings.City))
        {
            return !string.IsNullOrWhiteSpace(snapshotCity);
        }

        return string.Equals(snapshotCity, settings.City, StringComparison.OrdinalIgnoreCase);
    }

    private static WeatherSnapshot ErrorSnapshot(WeatherError error, string city) => new()
    {
        City = city,
        AirQualityIndex = -1,
        Condition = WeatherCondition.Unknown,
        ConditionText = WeatherFormat.ErrorText(error),
        UpdatedTime = DateTime.Now,
        Error = error
    };

    private void TryLoadDiskCache()
    {
        try
        {
            if (!File.Exists(_cacheFile))
            {
                return;
            }

            var json = File.ReadAllText(_cacheFile);
            var snapshot = JsonSerializer.Deserialize<WeatherSnapshot>(json);
            if (snapshot is { Error: WeatherError.None })
            {
                lock (_gate)
                {
                    _current = snapshot with { IsOffline = true };
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void SaveDiskCache(WeatherSnapshot snapshot)
    {
        if (snapshot.Error != WeatherError.None)
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_cacheFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(_cacheFile, json);
        }
        catch (Exception)
        {
        }
    }

    public async Task<IReadOnlyList<WeatherCityItem>> SearchCitiesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var localMatches = WeatherCities.Search(query, 8);
        var results = new List<WeatherCityItem>(localMatches);

        try
        {
            var encoded = Uri.EscapeDataString(query.Trim());
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=8&language=zh";
            var geoJson = await Http.GetStringAsync(geoUrl, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(geoJson);
            if (doc.RootElement.TryGetProperty("results", out var remoteResults))
            {
                var count = remoteResults.GetArrayLength();
                for (var i = 0; i < count; i++)
                {
                    var item = remoteResults[i];
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var admin = item.TryGetProperty("admin1", out var a) ? a.GetString() ?? "" : "";
                    var country = item.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(name) && !results.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new WeatherCityItem(name, admin, country));
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return results.Take(8).ToList();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Stop();
    }
}
