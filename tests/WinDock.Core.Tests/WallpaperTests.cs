using WinDock.Core;
using WinDock.Providers;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class WallpaperTests
{
    [Fact]
    public void LocalShufflePicker_returns_empty_on_empty_paths()
    {
        var (current, seen) = LocalShufflePicker.PickNext([], "", []);
        Assert.Equal("", current);
        Assert.Empty(seen);
    }

    [Fact]
    public void LocalShufflePicker_handles_single_item()
    {
        var paths = new[] { @"C:\wallpapers\one.jpg" };
        var (current, seen) = LocalShufflePicker.PickNext(paths, "", []);
        Assert.Equal(@"C:\wallpapers\one.jpg", current);
        Assert.Single(seen);
        Assert.Equal(@"C:\wallpapers\one.jpg", seen[0]);
    }

    [Fact]
    public void LocalShufflePicker_full_cycle_covers_all_items_without_duplicates()
    {
        var paths = new[]
        {
            @"C:\wallpapers\1.jpg",
            @"C:\wallpapers\2.jpg",
            @"C:\wallpapers\3.jpg",
            @"C:\wallpapers\4.jpg",
            @"C:\wallpapers\5.jpg"
        };

        var current = "";
        var seen = new List<string>();
        var picked = new List<string>();

        for (var i = 0; i < paths.Length; i++)
        {
            var result = LocalShufflePicker.PickNext(paths, current, seen);
            current = result.Current;
            seen = result.Seen;
            picked.Add(current);
        }

        // Within one cycle, all 5 items should be picked exactly once.
        Assert.Equal(paths.Length, picked.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(paths.Length, seen.Count);
    }

    [Fact]
    public void LocalShufflePicker_does_not_repeat_last_item_across_cycle_boundary()
    {
        var paths = new[]
        {
            @"C:\wallpapers\a.jpg",
            @"C:\wallpapers\b.jpg",
            @"C:\wallpapers\c.jpg"
        };

        // Simulate that all items were already seen in cycle 1, and the last picked was "b.jpg".
        var lastPicked = @"C:\wallpapers\b.jpg";
        var seen = paths.ToList();

        // Run multiple trials with different random seeds to be sure "b.jpg" is never immediately re-picked.
        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var (next, nextSeen) = LocalShufflePicker.PickNext(paths, lastPicked, seen, rng);
            Assert.NotEqual(lastPicked, next, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(next, paths, StringComparer.OrdinalIgnoreCase);
            Assert.Single(nextSeen);
            Assert.Equal(next, nextSeen[0]);
        }
    }

    [Fact]
    public void LocalShufflePicker_prunes_stale_seen_paths()
    {
        var activePaths = new[] { @"C:\wallpapers\a.jpg", @"C:\wallpapers\b.jpg" };
        var staleSeen = new[] { @"C:\wallpapers\deleted.jpg", @"C:\wallpapers\a.jpg" };

        var (current, seen) = LocalShufflePicker.PickNext(activePaths, @"C:\wallpapers\a.jpg", staleSeen);
        Assert.Equal(@"C:\wallpapers\b.jpg", current);
        Assert.Contains(@"C:\wallpapers\a.jpg", seen);
        Assert.Contains(@"C:\wallpapers\b.jpg", seen);
        Assert.DoesNotContain(@"C:\wallpapers\deleted.jpg", seen);
    }

    [Theory]
    [InlineData(1920, 1.0, 1920)]
    [InlineData(1080, 1.0, 1280)] // Clamped to min 1280
    [InlineData(1440, 1.0, 1920)] // 1440 -> ceil(1440/640)*640 = 3*640 = 1920
    [InlineData(2560, 1.0, 2560)] // 2560 / 640 = 4 -> 2560
    [InlineData(2560, 1.25, 3200)] // 2560 * 1.25 = 3200 -> 3200
    [InlineData(3840, 1.0, 3840)] // Max clamp 3840
    [InlineData(3840, 2.0, 3840)] // Clamped to max 3840
    public void WallpaperResolutionHelper_calculates_stepped_resolution(double screenWidth, double dpiScale, int expected)
    {
        var width = WallpaperResolutionHelper.CalculateTargetWidth(screenWidth, dpiScale);
        Assert.Equal(expected, width);
    }

    [Fact]
    public void UnsplashCuratedCatalog_contains_at_least_50_items()
    {
        Assert.True(UnsplashCuratedCatalog.Count >= 50);

        var all = UnsplashCuratedCatalog.GetAll(2560);
        Assert.Equal(UnsplashCuratedCatalog.Count, all.Count);

        foreach (var item in all)
        {
            Assert.StartsWith("curated:", item.Id);
            Assert.Equal(WallpaperSource.Curated, item.Source);
            Assert.Contains("images.unsplash.com", item.Url);
            Assert.Contains("w=2560", item.Url);
            Assert.False(string.IsNullOrWhiteSpace(item.Author));
            Assert.Contains("utm_source=windock", item.AuthorUrl);
            Assert.Contains("utm_source=windock", item.PageUrl);
            Assert.StartsWith("#", item.DominantColor);
        }
    }

    [Fact]
    public void UnsplashCuratedCatalog_FindById_and_Next_work_correctly()
    {
        var first = UnsplashCuratedCatalog.GetAll()[0];

        var foundWithPrefix = UnsplashCuratedCatalog.FindById(first.Id);
        Assert.NotNull(foundWithPrefix);
        Assert.Equal(first.Id, foundWithPrefix.Id);

        var rawId = first.Id["curated:".Length..];
        var foundWithoutPrefix = UnsplashCuratedCatalog.FindById(rawId);
        Assert.NotNull(foundWithoutPrefix);
        Assert.Equal(first.Id, foundWithoutPrefix.Id);

        var notFound = UnsplashCuratedCatalog.FindById("non-existent-id");
        Assert.Null(notFound);

        var next = UnsplashCuratedCatalog.Next(first.Id);
        Assert.NotEqual(first.Id, next.Id);
    }

    [Fact]
    public void BuiltInWidgets_registers_wallpaper_widget()
    {
        Assert.True(BuiltInWidgets.IsKnown(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.IsOffered(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.IsImplemented(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.CanOpen(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.CanCustomize(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.HasDensityModes(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.IsSingleton(BuiltInWidgets.Wallpaper));

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("壁纸", BuiltInWidgets.DisplayName(BuiltInWidgets.Wallpaper));
            Assert.Contains("壁纸", BuiltInWidgets.Blurb(BuiltInWidgets.Wallpaper));
        }

        using (UiLocale.Push(UiLanguage.English))
        {
            Assert.Equal("Wallpaper", BuiltInWidgets.DisplayName(BuiltInWidgets.Wallpaper));
            Assert.Contains("wallpaper", BuiltInWidgets.Blurb(BuiltInWidgets.Wallpaper), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WidgetCatalog_groups_include_wallpaper_category()
    {
        var group = WidgetCatalog.Groups.FirstOrDefault(g => g.Id == "wallpaper");
        Assert.NotNull(group);
        Assert.Contains(BuiltInWidgets.Wallpaper, group.WidgetIds);
        Assert.Contains(BuiltInWidgets.BingWallpaper, group.WidgetIds);

        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Wallpaper, "壁纸"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Wallpaper, "wallpaper"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Wallpaper, "Unsplash"));
    }

    [Fact]
    public void WallpaperSettings_defaults_and_normalization()
    {
        var def = WallpaperSettings.Default;
        Assert.Equal(WallpaperSource.Curated, def.Source);
        Assert.Equal(WallpaperRotation.Daily, def.Rotation);
        Assert.False(def.AutoApplyDesktop);
        Assert.True(def.LocalRecursive);

        var normalizedNull = WallpaperSettings.Normalize(null);
        Assert.Same(def, normalizedNull);

        var custom = new WallpaperSettings { Source = WallpaperSource.Bing };
        var normalizedCustom = WallpaperSettings.Normalize(custom);
        Assert.Equal(WallpaperSource.Bing, normalizedCustom.Source);
    }
}
