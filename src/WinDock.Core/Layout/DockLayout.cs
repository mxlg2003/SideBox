namespace WinDock.Core;

public static class DockLayoutCalculator
{
    public const double DefaultThickness = DockLayoutConstants.TileHeight + (2 * DockLayoutConstants.IslandPaddingY) + 2;

    public const double SideThickness = DefaultThickness;

    public static double ThicknessFor(DockEdge edge) =>
        edge is DockEdge.Left or DockEdge.Right ? SideThickness : DefaultThickness;

    public static RectLogical GetEdgeBounds(MonitorInfo monitor, DockEdge edge, double thicknessLogical)
    {
        var work = monitor.WorkArea;
        var thickness = Math.Max(1, thicknessLogical);
        return edge switch
        {
            DockEdge.Left => new RectLogical(work.X, work.Y, thickness, work.Height),
            DockEdge.Right => new RectLogical(work.Right - thickness, work.Y, thickness, work.Height),
            DockEdge.Bottom => new RectLogical(work.X, work.Bottom - thickness, work.Width, thickness),
            _ => new RectLogical(work.X, work.Y, work.Width, thickness)
        };
    }

    public static RectLogical GetIslandBounds(
        MonitorInfo monitor,
        DockEdge edge,
        double thicknessLogical,
        double contentWidth,
        double contentHeight,
        double? preferredLength = null)
    {
        var work = monitor.WorkArea;
        var margin = DockLayoutConstants.IslandMargin;
        var thickness = Math.Max(1, thicknessLogical);
        var maxWidth = Math.Max(1, work.Width - (2 * margin));
        var maxHeight = Math.Max(1, work.Height - (2 * margin));

        if (edge is DockEdge.Left or DockEdge.Right)
        {
            var width = Math.Min(Math.Max(thickness, contentWidth), maxWidth);
            var height = ResolveIslandLength(contentHeight, preferredLength, DockLayoutConstants.MinIslandLength, maxHeight);
            var y = work.Y + Math.Max(margin, (work.Height - height) / 2);
            var x = edge == DockEdge.Left
                ? work.X + margin
                : work.Right - width - margin;
            return new RectLogical(x, y, width, height);
        }

        var islandWidth = ResolveIslandLength(contentWidth, preferredLength, DockLayoutConstants.MinIslandLength, maxWidth);
        var islandHeight = Math.Min(Math.Max(thickness, contentHeight), maxHeight);
        var xPos = work.X + Math.Max(margin, (work.Width - islandWidth) / 2);
        var yPos = edge == DockEdge.Bottom
            ? work.Bottom - islandHeight - margin
            : work.Y + margin;
        return new RectLogical(xPos, yPos, islandWidth, islandHeight);
    }

    public static double ResolveIslandLength(double content, double? preferred, double min, double max)
    {
        var desired = preferred is > 0 ? preferred.Value : Math.Max(content, min);
        return Math.Clamp(desired, min, Math.Max(min, max));
    }

    public static MonitorInfo ResolveMonitor(DisplayTopologySnapshot topology, string? monitorRef)
    {
        if (topology.Monitors.Count == 0)
        {
            throw new InvalidOperationException("Display topology has no monitors.");
        }

        if (string.IsNullOrWhiteSpace(monitorRef) || string.Equals(monitorRef, "primary", StringComparison.OrdinalIgnoreCase))
        {
            return topology.Primary;
        }

        return topology.Monitors.FirstOrDefault(m =>
                   string.Equals(m.Id, monitorRef, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(m.DevicePath, monitorRef, StringComparison.OrdinalIgnoreCase))
               ?? topology.Primary;
    }

    public static WidgetPlacement RelocateToPrimary(WidgetPlacement placement) => new()
    {
        Id = placement.Id,
        Monitor = "primary",
        X = 40,
        Y = 80,
        Visible = placement.Visible
    };
}

public interface IZorderPolicy
{
    DockZorderState Evaluate(DockHostContext context);
}

public sealed class DefaultZorderPolicy : IZorderPolicy
{
    public DockZorderState Evaluate(DockHostContext context)
    {
        if (context.IsPresentationOrFullscreen)
        {
            return DockZorderState.FullscreenYield;
        }

        if (!context.SafeMode && context.DockMode == DockMode.AppBar && context.AppBarHealthy)
        {
            return DockZorderState.AppBarReserved;
        }

        if (context.AutoHide)
        {
            if (context.KeepVisible || context.PointerInDock)
            {
                return DockZorderState.OverlayTopmost;
            }

            if (context.PeekOnPointer && context.PointerInHotzone)
            {
                return DockZorderState.Peeking;
            }

            if (context.IsIdle)
            {
                return DockZorderState.AutoHidden;
            }
        }

        return DockZorderState.OverlayTopmost;
    }
}
