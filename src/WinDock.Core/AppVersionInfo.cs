using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinDock.Core;

public sealed record AppVersionInfo(
    string Version,
    string MinSupportedVersion,
    string DownloadUrl,
    string Sha256,
    string Changelog,
    bool Mandatory,
    string? ReleasePageUrl = null,
    long? AssetSize = null);

public readonly record struct UpdateCheckResult(
    bool Success,
    bool HasUpdate,
    AppVersionInfo? Info,
    string? Error);

public static class UpdateManifestParser
{
    public static string CleanVersion(string raw)
    {
        var trimmed = (raw ?? "").Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        return trimmed;
    }

    public static bool TryParseLatestJson(string json, out AppVersionInfo? info)
    {
        info = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;

            var version = Text(data, "version") ?? "";
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            var minSupported = Text(data, "min_supported_version") ?? "1.0.0";
            var downloadUrl = Text(data, "download_url") ?? "";
            var sha256 = Text(data, "sha256") ?? "";
            var changelog = Text(data, "changelog") ?? Text(data, "notes") ?? "";
            var releasePage = Text(data, "release_page") ?? Text(data, "html_url") ?? ProductInfo.GitHubLatestReleaseUrl;
            var mandatory = data.TryGetProperty("mandatory", out var m) && m.ValueKind == JsonValueKind.True;

            if (string.IsNullOrWhiteSpace(downloadUrl) && data.TryGetProperty("platforms", out var platforms))
            {
                if (platforms.TryGetProperty("windows-x86_64", out var win) ||
                    platforms.TryGetProperty("windows", out win) ||
                    platforms.TryGetProperty("win64", out win))
                {
                    downloadUrl = Text(win, "url") ?? "";
                    if (string.IsNullOrWhiteSpace(sha256))
                    {
                        sha256 = Text(win, "sha256") ?? Text(win, "signature") ?? "";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return false;
            }

            info = new AppVersionInfo(
                Version: CleanVersion(version),
                MinSupportedVersion: CleanVersion(minSupported),
                DownloadUrl: downloadUrl,
                Sha256: sha256,
                Changelog: changelog,
                Mandatory: mandatory,
                ReleasePageUrl: releasePage);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseGitHubReleaseJson(string json, out AppVersionInfo? info)
    {
        info = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = Text(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            var version = CleanVersion(tagName);
            var changelog = Text(root, "body") ?? "";
            var htmlUrl = Text(root, "html_url") ?? ProductInfo.GitHubLatestReleaseUrl;

            string downloadUrl = "";
            long? assetSize = null;
            string sha256 = "";

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                JsonElement? chosenAsset = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = Text(asset, "name") ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (chosenAsset is null ||
                            name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains(ProductInfo.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            chosenAsset = asset;
                        }
                    }
                }

                if (chosenAsset.HasValue)
                {
                    downloadUrl = Text(chosenAsset.Value, "browser_download_url") ?? "";
                    if (chosenAsset.Value.TryGetProperty("size", out var s) && s.TryGetInt64(out var sizeVal))
                    {
                        assetSize = sizeVal;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return false;
            }

            var shaMatch = Regex.Match(changelog, @"\b[0-9a-fA-F]{64}\b");
            if (shaMatch.Success)
            {
                sha256 = shaMatch.Value;
            }

            info = new AppVersionInfo(
                Version: version,
                MinSupportedVersion: "1.0.0",
                DownloadUrl: downloadUrl,
                Sha256: sha256,
                Changelog: changelog,
                Mandatory: false,
                ReleasePageUrl: htmlUrl,
                AssetSize: assetSize);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public static class UpdateChecker
{
    private static readonly HttpClient DefaultHttp = CreateDefaultClient();

    public static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Sidebox-Updater", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public static async Task<UpdateCheckResult> CheckUpdateAsync(
        Version currentVersion,
        HttpClient? httpClient = null,
        string? manifestUrl = null,
        string? apiUrl = null,
        CancellationToken cancellationToken = default)
    {
        var http = httpClient ?? DefaultHttp;
        manifestUrl ??= ProductInfo.GitHubLatestJsonUrl;
        apiUrl ??= ProductInfo.GitHubLatestApiUrl;

        // 1. Primary: Try fetching static latest.json (No GitHub API rate limits)
        try
        {
            using var manifestReq = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            using var manifestResp = await http.SendAsync(manifestReq, cancellationToken).ConfigureAwait(false);

            if (manifestResp.IsSuccessStatusCode)
            {
                var text = await manifestResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (UpdateManifestParser.TryParseLatestJson(text, out var manifestInfo) && manifestInfo is not null)
                {
                    var hasUpdate = Version.TryParse(UpdateManifestParser.CleanVersion(manifestInfo.Version), out var parsedRemote) && parsedRemote > currentVersion;
                    return new UpdateCheckResult(true, hasUpdate, manifestInfo, null);
                }
            }
        }
        catch (Exception)
        {
            // Silently fall back to GitHub Releases API
        }

        // 2. Secondary: Fallback to GitHub Releases API
        try
        {
            using var apiReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            apiReq.Headers.Accept.Clear();
            apiReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var apiResp = await http.SendAsync(apiReq, cancellationToken).ConfigureAwait(false);
            if (apiResp.StatusCode == HttpStatusCode.Forbidden)
            {
                return new UpdateCheckResult(false, false, null, UiText.UpdateRateLimited);
            }

            if (!apiResp.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, false, null, $"{UiText.UpdateCheckFailed} (HTTP {(int)apiResp.StatusCode})");
            }

            var json = await apiResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!UpdateManifestParser.TryParseGitHubReleaseJson(json, out var apiInfo) || apiInfo is null)
            {
                return new UpdateCheckResult(false, false, null, UiText.UpdateCheckFailed);
            }

            var hasUpdate = Version.TryParse(UpdateManifestParser.CleanVersion(apiInfo.Version), out var parsedRemote) && parsedRemote > currentVersion;
            return new UpdateCheckResult(true, hasUpdate, apiInfo, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, false, null, $"{UiText.UpdateCheckFailed} ({ex.Message})");
        }
    }
}
