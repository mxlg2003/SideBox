using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class NetworkWidgetTests
{
    [Fact]
    public void Settings_normalize_unknown_show_to_both()
    {
        var settings = NetworkWidgetSettings.Normalize(new NetworkWidgetSettings
        {
            Show = "wifi",
            ChartInDock = false,
            ChartInPopup = false
        });
        Assert.Equal(NetworkShows.Both, settings.Show);
        Assert.False(settings.ChartInDock);
        Assert.False(settings.ChartInPopup);
        Assert.True(settings.ShowsDownload);
        Assert.True(settings.ShowsUpload);
        Assert.Equal("下载", NetworkFormat.ShowTitle(NetworkShows.Download));
        Assert.Equal("上传", NetworkFormat.ShowTitle(NetworkShows.Upload));
        Assert.Equal("全部", NetworkFormat.ShowTitle("nope"));
        Assert.False(NetworkWidgetSettings.Normalize(new NetworkWidgetSettings { Show = NetworkShows.Upload }).ShowsDownload);
        Assert.False(NetworkWidgetSettings.Normalize(new NetworkWidgetSettings { Show = NetworkShows.Download }).ShowsUpload);
    }

    [Fact]
    public void History_is_capped_at_sixty()
    {
        IReadOnlyList<double> history = [];
        for (var i = 0; i < 80; i++)
        {
            history = NetworkHistory.Push(history, i);
        }

        Assert.Equal(NetworkHistory.Length, history.Count);
        Assert.Equal(20, history[0]);
        Assert.Equal(79, history[^1]);
    }
}
