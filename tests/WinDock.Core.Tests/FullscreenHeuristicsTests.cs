using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class FullscreenHeuristicsTests
{
    [Fact]
    public void Maximized_windows_do_not_yield()
    {
        Assert.False(FullscreenHeuristics.YieldsForCoveringWindow(1920, 1080, 1920, 1080, maximized: true));
    }

    [Fact]
    public void Near_cover_yields_when_not_maximized()
    {
        Assert.True(FullscreenHeuristics.YieldsForCoveringWindow(1920, 1080, 1920, 1080, maximized: false));
        Assert.False(FullscreenHeuristics.YieldsForCoveringWindow(800, 600, 1920, 1080, maximized: false));
    }
}
