using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class BingWallpaperArchiveTests
{
    [Fact]
    public void Parses_relative_url_title_and_copyright()
    {
        const string json = """
            {"images":[{"startdate":"20260913","url":"/th?id=OHR.Lake_ZH-CN1_1920x1080.jpg","urlbase":"/th?id=OHR.Lake_ZH-CN1","copyright":"Dawn Lake (© Photographer)","title":"Dawn Lake","hsh":"abc123"}]}
            """;

        var images = BingWallpaperArchive.Parse(json);
        var image = Assert.Single(images);
        Assert.Equal("20260913", image.StartDate);
        Assert.Equal("Dawn Lake", image.Title);
        Assert.Equal("https://www.bing.com/th?id=OHR.Lake_ZH-CN1_1920x1080.jpg", image.ImageUrl);
        Assert.Equal("https://www.bing.com/th?id=OHR.Lake_ZH-CN1_UHD.jpg", image.WallpaperUrl);
        Assert.Equal(
            "https://www.bing.com/th?id=OHR.Lake_ZH-CN1_UHD.jpg&rf=LaDigue_1920x1080.jpg",
            BingWallpaperArchive.ToUhdUrl("https://www.bing.com/th?id=OHR.Lake_ZH-CN1_1920x1080.jpg&rf=LaDigue_1920x1080.jpg"));
        Assert.Throws<ArgumentException>(() => BingWallpaperArchive.ToAbsoluteUrl("https://evil.example/x.jpg"));
        Assert.Equal("abc123", image.Key);
        Assert.Equal("20260913-abc123", BingWallpaperArchive.FileStem(image));
    }

    [Fact]
    public void Title_falls_back_to_copyright_before_credit()
    {
        Assert.Equal("Dawn Lake", BingWallpaperArchive.DisplayTitle(null, "Dawn Lake (© Photographer)"));
        Assert.Equal("Bing 壁纸", BingWallpaperArchive.DisplayTitle(" ", " "));
    }

    [Fact]
    public void Chinese_culture_maps_to_zh_CN_catalog()
    {
        Assert.Equal("zh-CN", BingWallpaperArchive.ResolveMarket("zh-Hans-CN"));
        Assert.Contains("mkt=zh-CN", BingWallpaperArchive.CatalogUrl("zh-CN"));
        Assert.Contains("n=8", BingWallpaperArchive.CatalogUrl("en-US"));
    }

    [Fact]
    public void WrapIndex_handles_negative_and_empty()
    {
        Assert.Equal(0, BingWallpaperArchive.WrapIndex(-1, 0));
        Assert.Equal(7, BingWallpaperArchive.WrapIndex(-1, 8));
        Assert.Equal(0, BingWallpaperArchive.WrapIndex(8, 8));
    }

    [Fact]
    public void FormatDate_reads_bing_startdate()
    {
        Assert.Equal("9月13日", BingWallpaperArchive.FormatDate("20260913"));
        Assert.Equal("今日", BingWallpaperArchive.FormatDate("nope"));
    }

    [Fact]
    public void Parses_multi_day_catalog_correctly()
    {
        const string json = """
            {
                "images": [
                    {"startdate":"20261008","url":"/th?id=OHR.Today_1920x1080.jpg","title":"Today Pic","hsh":"key0"},
                    {"startdate":"20261007","url":"/th?id=OHR.Yesterday_1920x1080.jpg","title":"Yesterday Pic","hsh":"key1"},
                    {"startdate":"20261006","url":"/th?id=OHR.TwoDaysAgo_1920x1080.jpg","title":"Two Days Ago Pic","hsh":"key2"}
                ]
            }
            """;

        var images = BingWallpaperArchive.Parse(json);
        Assert.Equal(3, images.Count);
        Assert.Equal("Today Pic", images[0].Title);
        Assert.Equal("Yesterday Pic", images[1].Title);
        Assert.Equal("Two Days Ago Pic", images[2].Title);
        Assert.True(DockTokens.BingFlyoutWidth > 300);
        Assert.True(DockTokens.BingHeroHeight > 100);
    }

    [Fact]
    public void BingWallpaperSettings_defaults_and_normalization()
    {
        var settings = BingWallpaperSettings.Normalize(null);
        Assert.False(settings.AutoApplyDaily);
        Assert.Null(settings.LastAppliedKey);

        var custom = new BingWallpaperSettings { AutoApplyDaily = true, LastAppliedKey = "20261008" };
        var normalized = BingWallpaperSettings.Normalize(custom);
        Assert.True(normalized.AutoApplyDaily);
        Assert.Equal("20261008", normalized.LastAppliedKey);
    }
}
