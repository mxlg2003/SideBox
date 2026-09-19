namespace WinDock.Core;

/// <summary>
/// Keeps a dragged island on the dock's long axis and inside the visible dock bounds.
/// </summary>
public static class DockDragConstraints
{
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
