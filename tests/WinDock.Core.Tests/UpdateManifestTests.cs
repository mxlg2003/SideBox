using System.Net;
using System.Net.Http;
using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class UpdateManifestTests
{
    [Theory]
    [InlineData("v1.0.1", "1.0.1")]
    [InlineData("V2.3.4", "2.3.4")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("  v3.0.0  ", "3.0.0")]
    public void CleanVersion_StripsLeadingVAndTrims(string raw, string expected)
    {
        var cleaned = UpdateManifestParser.CleanVersion(raw);
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void TryParseLatestJson_StandardFlatFormat_ParsesSuccessfully()
    {
        var json = """
        {
            "version": "v1.2.0",
            "min_supported_version": "1.0.0",
            "download_url": "https://github.com/mxlg2003/WinDock/releases/download/v1.2.0/Sidebox-setup.exe",
            "sha256": "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945",
            "changelog": "- 优化窗口\n- 修复问题",
            "release_page": "https://github.com/mxlg2003/WinDock/releases/tag/v1.2.0",
            "mandatory": false
        }
        """;

        var ok = UpdateManifestParser.TryParseLatestJson(json, out var info);
        Assert.True(ok);
        Assert.NotNull(info);
        Assert.Equal("1.2.0", info.Version);
        Assert.Equal("https://github.com/mxlg2003/WinDock/releases/download/v1.2.0/Sidebox-setup.exe", info.DownloadUrl);
        Assert.Equal("4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945", info.Sha256);
        Assert.Equal("https://github.com/mxlg2003/WinDock/releases/tag/v1.2.0", info.ReleasePageUrl);
        Assert.False(info.Mandatory);
    }

    [Fact]
    public void TryParseLatestJson_TauriPlatformsFormat_ParsesSuccessfully()
    {
        var json = """
        {
            "version": "1.3.0",
            "notes": "Tauri-style release notes",
            "platforms": {
                "windows-x86_64": {
                    "url": "https://github.com/mxlg2003/WinDock/releases/download/v1.3.0/Sidebox-setup.exe",
                    "sha256": "abcdef123456"
                }
            }
        }
        """;

        var ok = UpdateManifestParser.TryParseLatestJson(json, out var info);
        Assert.True(ok);
        Assert.NotNull(info);
        Assert.Equal("1.3.0", info.Version);
        Assert.Equal("https://github.com/mxlg2003/WinDock/releases/download/v1.3.0/Sidebox-setup.exe", info.DownloadUrl);
        Assert.Equal("abcdef123456", info.Sha256);
        Assert.Equal("Tauri-style release notes", info.Changelog);
    }

    [Fact]
    public void TryParseGitHubReleaseJson_ValidRelease_ExtractsInstallerAndSha()
    {
        var sha = "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
        var json = $$"""
        {
            "tag_name": "v1.1.0",
            "name": "Sidebox v1.1.0",
            "html_url": "https://github.com/mxlg2003/WinDock/releases/tag/v1.1.0",
            "body": "更新说明如下：\n- 新增天气模块\n\nSHA256: {{sha}}",
            "assets": [
                {
                    "name": "Sidebox-setup.exe",
                    "size": 52428800,
                    "browser_download_url": "https://github.com/mxlg2003/WinDock/releases/download/v1.1.0/Sidebox-setup.exe"
                },
                {
                    "name": "checksums.txt",
                    "size": 128,
                    "browser_download_url": "https://github.com/mxlg2003/WinDock/releases/download/v1.1.0/checksums.txt"
                }
            ]
        }
        """;

        var ok = UpdateManifestParser.TryParseGitHubReleaseJson(json, out var info);
        Assert.True(ok);
        Assert.NotNull(info);
        Assert.Equal("1.1.0", info.Version);
        Assert.Equal("https://github.com/mxlg2003/WinDock/releases/download/v1.1.0/Sidebox-setup.exe", info.DownloadUrl);
        Assert.Equal(sha, info.Sha256);
        Assert.Equal(52428800, info.AssetSize);
        Assert.Equal("https://github.com/mxlg2003/WinDock/releases/tag/v1.1.0", info.ReleasePageUrl);
    }

    [Fact]
    public void TryParseGitHubReleaseJson_NoExeAsset_ReturnsFalse()
    {
        var json = """
        {
            "tag_name": "v1.1.0",
            "assets": [
                {
                    "name": "source.zip",
                    "browser_download_url": "https://github.com/.../source.zip"
                }
            ]
        }
        """;

        var ok = UpdateManifestParser.TryParseGitHubReleaseJson(json, out var info);
        Assert.False(ok);
        Assert.Null(info);
    }

    [Fact]
    public async Task UpdateChecker_LatestJsonNewer_ReturnsHasUpdateTrue()
    {
        var manifestJson = """
        {
            "version": "1.2.0",
            "download_url": "https://example.com/Sidebox-setup.exe",
            "changelog": "New version notes"
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("latest.json") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifestJson)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var result = await UpdateChecker.CheckUpdateAsync(
            currentVersion: new Version(1, 0, 0),
            httpClient: client);

        Assert.True(result.Success);
        Assert.True(result.HasUpdate);
        Assert.NotNull(result.Info);
        Assert.Equal("1.2.0", result.Info.Version);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task UpdateChecker_LatestJsonSameOrOlder_ReturnsHasUpdateFalse()
    {
        var manifestJson = """
        {
            "version": "1.0.0",
            "download_url": "https://example.com/Sidebox-setup.exe"
        }
        """;

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(manifestJson)
        });

        using var client = new HttpClient(handler);
        var result = await UpdateChecker.CheckUpdateAsync(
            currentVersion: new Version(1, 0, 0),
            httpClient: client);

        Assert.True(result.Success);
        Assert.False(result.HasUpdate);
        Assert.NotNull(result.Info);
    }

    [Fact]
    public async Task UpdateChecker_LatestJsonFails_FallbackToGitHubApi_Works()
    {
        var apiJson = """
        {
            "tag_name": "v1.5.0",
            "html_url": "https://github.com/mxlg2003/WinDock/releases/tag/v1.5.0",
            "body": "Fallback update notes",
            "assets": [
                {
                    "name": "Sidebox-setup.exe",
                    "browser_download_url": "https://github.com/mxlg2003/WinDock/releases/download/v1.5.0/Sidebox-setup.exe"
                }
            ]
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("latest.json") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(apiJson)
            };
        });

        using var client = new HttpClient(handler);
        var result = await UpdateChecker.CheckUpdateAsync(
            currentVersion: new Version(1, 0, 0),
            httpClient: client);

        Assert.True(result.Success);
        Assert.True(result.HasUpdate);
        Assert.NotNull(result.Info);
        Assert.Equal("1.5.0", result.Info.Version);
    }

    [Fact]
    public async Task UpdateChecker_RateLimitedOnApi_ReturnsRateLimitedError()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("latest.json") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        });

        using var client = new HttpClient(handler);
        var result = await UpdateChecker.CheckUpdateAsync(
            currentVersion: new Version(1, 0, 0),
            httpClient: client);

        Assert.False(result.Success);
        Assert.False(result.HasUpdate);
        Assert.Equal(UiText.UpdateRateLimited, result.Error);
    }

    [Theory]
    [InlineData("1.0.3", "1.0.0.0", true)]
    [InlineData("v1.0.3", "1.0.2.0", true)]
    [InlineData("1.0.2", "1.0.2.0", false)]
    [InlineData("v1.0.2", "1.0.2.0", false)]
    [InlineData("1.0.2.0", "1.0.2.0", false)]
    [InlineData("1.0.1", "1.0.2.0", false)]
    [InlineData("1.1.0", "1.0.9.0", true)]
    [InlineData("2.0.0", "1.9.9.0", true)]
    [InlineData("invalid", "1.0.0.0", false)]
    public void IsNewerVersion_AccurateAcrossDifferentPartCounts(string remote, string current, bool expected)
    {
        var curVer = Version.Parse(current);
        var actual = UpdateChecker.IsNewerVersion(remote, curVer);
        Assert.Equal(expected, actual);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
