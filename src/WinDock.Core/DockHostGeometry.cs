namespace WinDock.Core;

public static class DockHostGeometry
{
    public const int HotzonePixels = 8;

    public static bool InEdgeHotzone(int x, int y, RectLogical pixelWorkArea, DockEdge edge, int thickness = HotzonePixels)
    {
        if (pixelWorkArea.Width <= 0 || pixelWorkArea.Height <= 0 || thickness <= 0)
        {
            return false;
        }

        var left = pixelWorkArea.X;
        var top = pixelWorkArea.Y;
        var right = pixelWorkArea.Right;
        var bottom = pixelWorkArea.Bottom;
        return edge switch
        {
            DockEdge.Left => x >= left && x < left + thickness && y >= top && y < bottom,
            DockEdge.Right => x >= right - thickness && x < right && y >= top && y < bottom,
            DockEdge.Bottom => y >= bottom - thickness && y < bottom && x >= left && x < right,
            _ => y >= top && y < top + thickness && x >= left && x < right
        };
    }

    /// <summary>
    /// Hotzone aligned to the island span, including the inset corridor from the work-area edge
    /// so the pointer can travel from the hotzone strip onto a centered island.
    /// </summary>
    public static bool InIslandApproachHotzone(
        int x,
        int y,
        RectLogical pixelWorkArea,
        DockEdge edge,
        RectLogical islandPixels,
        int thickness = HotzonePixels)
    {
        if (islandPixels.Width <= 0 || islandPixels.Height <= 0)
        {
            return InEdgeHotzone(x, y, pixelWorkArea, edge, thickness);
        }

        const int slack = DockTokens.TrapezoidBuffer;
        return edge switch
        {
            DockEdge.Left =>
                y >= islandPixels.Y - slack && y < islandPixels.Bottom + slack
                && x >= pixelWorkArea.X && x < islandPixels.Right + slack,
            DockEdge.Right =>
                y >= islandPixels.Y - slack && y < islandPixels.Bottom + slack
                && x >= islandPixels.X - slack && x < pixelWorkArea.Right,
            DockEdge.Bottom =>
                x >= islandPixels.X - slack && x < islandPixels.Right + slack
                && y >= islandPixels.Y - slack && y < pixelWorkArea.Bottom,
            _ =>
                x >= islandPixels.X - slack && x < islandPixels.Right + slack
                && y >= pixelWorkArea.Y && y < islandPixels.Bottom + slack
        };
    }

    /// <summary>
    /// The 4px auto-hide strip is proximity, not “pointer on the island”.
    /// At 150–200% DPI the strip covers the 8px hotzone, so counting strip hover as
    /// <c>PointerInDock</c> would make <c>PeekOnPointer=false</c> a no-op.
    /// </summary>
    public static bool IsPointerOnIsland(bool overWindow, bool stripMode, bool overFlyout) =>
        (overWindow && !stripMode) || overFlyout;
}
