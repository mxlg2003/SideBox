using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class DockLayoutTests
{
    [Fact]
    public void Top_island_is_centered_and_inset_from_work_area()
    {
        var monitor = Monitor(1920, 1040);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Top, 68, 320, 52);
        Assert.Equal(320, bounds.Width);
        Assert.Equal(68, bounds.Height);
        Assert.Equal(monitor.WorkArea.Y + DockTokens.IslandMargin, bounds.Y);
        Assert.Equal(monitor.WorkArea.X + ((1920 - 320) / 2), bounds.X);
    }

    [Fact]
    public void Left_island_is_vertically_centered()
    {
        var monitor = Monitor(1920, 1080);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Left, 84, 80, 240);
        Assert.Equal(84, bounds.Width);
        Assert.Equal(240, bounds.Height);
        Assert.Equal(monitor.WorkArea.X + DockTokens.IslandMargin, bounds.X);
        Assert.Equal(monitor.WorkArea.Y + ((1080 - 240) / 2), bounds.Y);
    }

    [Fact]
    public void Right_island_sits_on_the_right_margin()
    {
        var monitor = Monitor(1920, 1080);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Right, 84, 90, 200);
        Assert.Equal(monitor.WorkArea.Right - bounds.Width - DockTokens.IslandMargin, bounds.X);
        Assert.Equal(90, bounds.Width);
    }

    [Fact]
    public void Oversized_content_is_clamped_with_side_margins()
    {
        var monitor = Monitor(800, 600);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Top, 68, 4000, 40);
        Assert.Equal(800 - (2 * DockTokens.IslandMargin), bounds.Width);
        Assert.Equal(monitor.WorkArea.X + DockTokens.IslandMargin, bounds.X);
    }

    [Fact]
    public void Tiny_content_grows_to_minimum_island_length()
    {
        var monitor = Monitor(1920, 1080);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Top, 68, 40, 40);
        Assert.Equal(DockTokens.MinIslandLength, bounds.Width);
    }

    [Fact]
    public void Preferred_length_is_used_when_narrower_than_content()
    {
        var monitor = Monitor(1920, 1080);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Top, 68, 900, 52, 400);
        Assert.Equal(400, bounds.Width);
        Assert.Equal(monitor.WorkArea.X + ((1920 - 400) / 2), bounds.X);
    }

    [Fact]
    public void Preferred_length_is_clamped_to_the_work_area()
    {
        var monitor = Monitor(800, 600);
        var bounds = DockLayoutCalculator.GetIslandBounds(monitor, DockEdge.Top, 68, 200, 40, 4000);
        Assert.Equal(800 - (2 * DockTokens.IslandMargin), bounds.Width);
    }

    private static MonitorInfo Monitor(double width, double height) => new()
    {
        Id = "primary",
        DevicePath = @"\\.\DISPLAY1",
        IsPrimary = true,
        Bounds = new RectLogical(0, 0, width, height),
        WorkArea = new RectLogical(0, 0, width, height),
        DpiScale = 1
    };
}
