using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class DockDragConstraintsTests
{
    private static readonly RectLogical Viewport = new(0, 0, 400, 54);

    [Fact]
    public void Horizontal_drag_ignores_vertical_mouse_motion()
    {
        var lift = DockDragConstraints.ConstrainLift(40, 5, 80, -30, 80, 44, Viewport, horizontal: true);
        Assert.Equal(40, lift.X);
        Assert.Equal(0, lift.Y);
    }

    [Fact]
    public void Horizontal_drag_stops_at_the_dock_ends()
    {
        var pastLeft = DockDragConstraints.ConstrainLift(40, 5, -80, 5, 80, 44, Viewport, horizontal: true);
        Assert.Equal(-40, pastLeft.X);
        Assert.Equal(0, pastLeft.Y);

        var pastRight = DockDragConstraints.ConstrainLift(40, 5, 500, 5, 80, 44, Viewport, horizontal: true);
        Assert.Equal(280, pastRight.X);
        Assert.Equal(0, pastRight.Y);
    }

    [Fact]
    public void Vertical_drag_stays_on_the_dock_axis()
    {
        var lift = DockDragConstraints.ConstrainLift(5, 40, 90, 120, 44, 80, new RectLogical(0, 0, 54, 400), horizontal: false);
        Assert.Equal(0, lift.X);
        Assert.Equal(80, lift.Y);
    }

    [Fact]
    public void Slot_wider_than_the_dock_pins_to_the_start()
    {
        var lift = DockDragConstraints.ConstrainLift(0, 0, 40, 0, 500, 44, Viewport, horizontal: true);
        Assert.Equal(0, lift.X);
        Assert.Equal(0, lift.Y);
    }

    [Fact]
    public void Click_below_a_horizontal_dock_is_not_a_drag()
    {
        Assert.True(DockDragConstraints.IsCrossAxisOutside(cross: 320, extent: 54));
        Assert.False(DockDragConstraints.IsDragStart(0, 0));
    }

    [Fact]
    public void Click_on_the_island_can_still_start_a_drag()
    {
        Assert.False(DockDragConstraints.IsCrossAxisOutside(cross: 20, extent: 54));
        Assert.True(DockDragConstraints.IsDragStart(12, 2));
    }

    [Fact]
    public void Click_beside_a_vertical_dock_is_not_a_drag()
    {
        Assert.True(DockDragConstraints.IsCrossAxisOutside(cross: 240, extent: 54));
    }

    [Fact]
    public void Click_on_the_island_does_not_move_it_to_another_monitor()
    {
        Assert.False(DockDragConstraints.IsIslandMonitorDrag(4, 2));
        Assert.True(DockDragConstraints.IsIslandMonitorDrag(80, 0));
    }
}
