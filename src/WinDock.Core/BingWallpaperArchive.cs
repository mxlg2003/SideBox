using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinDock.Core;

/// <summary>Pure helpers for Bing's HPImageArchive JSON (no HTTP, no Win32).</summary>
public static class BingWallpaperArchive
{
    public const string Host = "https://www.bing.com";

    public static string ResolveMarket(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return "en-US";
        }

        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }

        return cultureName;
    }

    public static string CatalogUrl(string market, int count = 8)
    {
        var n = Math.Clamp(count, 1, 8);
        return $"{Host}/HPImageArchive.aspx?format=js&idx=0&n={n}&mkt={Uri.EscapeDataString(market)}";
    }

    public static string ToAbsoluteUrl(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            throw new ArgumentException("Image URL is required.", nameof(urlOrPath));
        }

        if (urlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || urlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || !IsBingHost(uri.Host))
            {
                throw new ArgumentException("Image URL must be a bing.com address.", nameof(urlOrPath));
            }

            return uri.ToString();
        }

        return urlOrPath.StartsWith('/') ? Host + urlOrPath : Host + "/" + urlOrPath;
    }

    public static bool IsBingHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Equals("bing.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".bing.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToUhdUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image URL is required.", nameof(imageUrl));
        }

        var fromId = Regex.Replace(
            imageUrl,
            @"(id=[^&]*?)_\d{3,5}x\d{3,5}(?=\.jpg)",
            "$1_UHD",
            RegexOptions.IgnoreCase);
        if (!string.Equals(fromId, imageUrl, StringComparison.Ordinal))
        {
            return fromId;
        }

        return Regex.Replace(
            imageUrl,
            @"_\d{3,5}x\d{3,5}(?=\.jpg)",
            "_UHD",
            RegexOptions.IgnoreCase);
    }

    public static string DisplayTitle(string? title, string? copyright)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        if (string.IsNullOrWhiteSpace(copyright))
        {
            return "Bing 壁纸";
        }

        var cut = copyright.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? copyright[..cut].Trim() : copyright.Trim();
    }

    public static string FormatDate(string? startDate)
    {
        if (!string.IsNullOrWhiteSpace(startDate)
            && startDate.Length >= 8
            && DateTime.TryParseExact(
                startDate[..8],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return $"{date.Month}月{date.Day}日";
        }

        return "今日";
    }

    public static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    public static string FileStem(BingWallpaperImage image)
    {
        var date = Sanitize(image.StartDate);
        var key = Sanitize(image.Key);
        return string.IsNullOrEmpty(key) ? date : $"{date}-{key}";
    }

    public static IReadOnlyList<BingWallpaperImage> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<BingWallpaperImage>();
        foreach (var item in images.EnumerateArray())
        {
            var url = GetString(item, "url");
            var urlBase = GetString(item, "urlbase");
            var relative = !string.IsNullOrWhiteSpace(url)
                ? url
                : string.IsNullOrWhiteSpace(urlBase) ? null : urlBase + "_1920x1080.jpg";
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            string imageUrl;
            try
            {
                imageUrl = ToAbsoluteUrl(relative);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var copyright = GetString(item, "copyright");
            var title = DisplayTitle(GetString(item, "title"), copyright);
            var startDate = GetString(item, "startdate") ?? "";
            var key = GetString(item, "hsh") ?? startDate;
            var wallpaperUrl = !string.IsNullOrWhiteSpace(urlBase)
                ? ToAbsoluteUrl(urlBase + "_UHD.jpg")
                : ToUhdUrl(imageUrl);
            list.Add(new BingWallpaperImage
            {
                StartDate = startDate,
                Title = title,
                Copyright = copyright ?? title,
                ImageUrl = imageUrl,
                WallpaperUrl = wallpaperUrl,
                Key = string.IsNullOrWhiteSpace(key) ? startDate : key
            });
        }

        return list;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "bing";
        }

        var chars = value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray();
        return chars.Length == 0 ? "bing" : new string(chars);
    }
}
