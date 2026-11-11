namespace WinDock.Core;

/// <summary>
/// Keeps a dragged island on the dock's long axis and inside the visible dock bounds.
/// </summary>
public static class DockDragConstraints
{
    public const double DragStartSlop = 8;

    public const double CrossAxisSlack = 16;

    public const double IslandMonitorDragSlop = 48;

    public static bool IsIslandMonitorDrag(double deltaX, double deltaY)
    {
        var slop = IslandMonitorDragSlop;
        return (deltaX * deltaX) + (deltaY * deltaY) >= slop * slop;
    }

    /// <summary>
    /// A click outside the island, such as dismissing a flyout, is not a drag.
    /// </summary>
    public static bool IsCrossAxisOutside(double cross, double extent, double slack = CrossAxisSlack) =>
        extent > 1 && (cross < -slack || cross > extent + slack);

    public static bool IsDragStart(double deltaX, double deltaY) =>
        Math.Abs(deltaX) >= DragStartSlop || Math.Abs(deltaY) >= DragStartSlop;

    public static (double X, double Y) ConstrainLift(
        double layoutX,
        double layoutY,
        double desiredX,
        double desiredY,
        double slotWidth,
        double slotHeight,
        RectLogical viewport,
        bool horizontal)
    {
        if (horizontal)
        {
            var min = viewport.X;
            var max = viewport.X + Math.Max(0, viewport.Width - Math.Max(0, slotWidth));
            var x = Math.Clamp(desiredX, min, Math.Max(min, max));
            return (x - layoutX, 0);
        }

        var minY = viewport.Y;
        var maxY = viewport.Y + Math.Max(0, viewport.Height - Math.Max(0, slotHeight));
        var y = Math.Clamp(desiredY, minY, Math.Max(minY, maxY));
        return (0, y - layoutY);
    }
}
