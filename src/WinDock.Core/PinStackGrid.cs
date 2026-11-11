namespace WinDock.Core;

/// <summary>
/// Packs a collapsed pin stack into rows or columns that stay inside the work area.
/// Top and bottom docks fill each row left to right, then wrap. Side docks fill each column top to bottom, then wrap.
/// When the wrapped grid still exceeds the space beside the island, the cross axis scrolls.
/// </summary>
public static class PinStackGrid
{
    public readonly record struct Viewport(double MaxWidth, double MaxHeight)
    {
        public static Viewport Unbounded { get; } = new(double.PositiveInfinity, double.PositiveInfinity);
    }

    public sealed record Arrangement
    {
        public bool Horizontal { get; init; }

        public IReadOnlyList<IReadOnlyList<int>> Bands { get; init; } = [];

        public double ContentWidth { get; init; }

        public double ContentHeight { get; init; }

        public double InnerWidth { get; init; }

        public double InnerHeight { get; init; }

        public double? ScrollWidth { get; init; }

        public double? ScrollHeight { get; init; }

        public bool ScrollsVertically { get; init; }

        public bool ScrollsHorizontally { get; init; }
    }

    public static Viewport Budget(RectLogical work, RectLogical island, DockEdge edge)
    {
        if (work.Width < 1 || work.Height < 1 || island.Width < 1 || island.Height < 1
            || !double.IsFinite(work.X) || !double.IsFinite(work.Y)
            || !double.IsFinite(island.X) || !double.IsFinite(island.Y))
        {
            return Viewport.Unbounded;
        }

        var margin = DockTokens.IslandMargin;
        var gap = DockTokens.FlyoutOffset;
        var minWidth = DockTokens.ArtSize + 16 + (DockTokens.TilePaddingX * 2);
        var minHeight = DockTokens.TileHeight + (DockTokens.TilePaddingY * 2);
        double width;
        double height;
        switch (edge)
        {
            case DockEdge.Left:
                width = work.Right - margin - island.Right - gap;
                height = work.Height - (margin * 2);
                break;
            case DockEdge.Right:
                width = island.X - gap - (work.X + margin);
                height = work.Height - (margin * 2);
                break;
            case DockEdge.Bottom:
                width = work.Width - (margin * 2);
                height = island.Y - gap - (work.Y + margin);
                break;
            default:
                width = work.Width - (margin * 2);
                height = work.Bottom - margin - island.Bottom - gap;
                break;
        }

        return new Viewport(Math.Max(minWidth, width), Math.Max(minHeight, height));
    }

    public static Arrangement Arrange(
        IReadOnlyList<double> tileWidths,
        bool horizontal,
        Viewport viewport,
        double tileHeight = DockTokens.TileHeight,
        double gap = DockTokens.TileGap,
        double padX = DockTokens.TilePaddingX,
        double padY = DockTokens.TilePaddingY,
        double scrollThickness = DockTokens.Settings.ScrollThickness)
    {
        tileWidths ??= [];
        var widths = new double[tileWidths.Count];
        var widest = 1d;
        for (var i = 0; i < tileWidths.Count; i++)
        {
            var width = double.IsFinite(tileWidths[i]) ? Math.Max(0, tileWidths[i]) : 0;
            widths[i] = width;
            widest = Math.Max(widest, width);
        }

        var innerWidth = Inner(viewport.MaxWidth, padX * 2);
        var innerHeight = Inner(viewport.MaxHeight, padY * 2);
        var primary = horizontal ? innerWidth : innerHeight;
        var bands = Pack(widths, tileHeight, gap, Limit(primary, horizontal ? widest : tileHeight, false, scrollThickness), horizontal);
        var size = Measure(bands, widths, tileHeight, gap, horizontal);
        var crossOverflow = horizontal
            ? size.Height > innerHeight + 0.5
            : size.Width > innerWidth + 0.5;
        if (crossOverflow && double.IsFinite(primary))
        {
            bands = Pack(widths, tileHeight, gap, Limit(primary, horizontal ? widest : tileHeight, true, scrollThickness), horizontal);
            size = Measure(bands, widths, tileHeight, gap, horizontal);
        }

        var scrollsVertically = double.IsFinite(innerHeight) && size.Height > innerHeight + 0.5;
        var scrollsHorizontally = double.IsFinite(innerWidth) && size.Width > innerWidth + 0.5;
        double? scrollWidth = null;
        double? scrollHeight = null;
        if (scrollsVertically || scrollsHorizontally)
        {
            if (scrollsVertically && scrollsHorizontally)
            {
                scrollWidth = Math.Max(1, innerWidth);
                scrollHeight = Math.Max(tileHeight, innerHeight);
            }
            else if (scrollsVertically)
            {
                scrollWidth = size.Width + scrollThickness;
                scrollHeight = Math.Max(tileHeight, Math.Min(size.Height, innerHeight));
            }
            else
            {
                scrollWidth = Math.Max(1, Math.Min(size.Width, innerWidth));
                scrollHeight = size.Height + scrollThickness;
            }
        }

        return new Arrangement
        {
            Horizontal = horizontal,
            Bands = bands,
            ContentWidth = size.Width,
            ContentHeight = size.Height,
            InnerWidth = innerWidth,
            InnerHeight = innerHeight,
            ScrollWidth = scrollWidth,
            ScrollHeight = scrollHeight,
            ScrollsVertically = scrollsVertically,
            ScrollsHorizontally = scrollsHorizontally
        };
    }

    private static double Inner(double outer, double padding)
    {
        if (!double.IsFinite(outer))
        {
            return double.PositiveInfinity;
        }

        return Math.Max(0, outer - padding);
    }

    private static double Limit(double room, double floor, bool reserveBar, double bar)
    {
        if (!double.IsFinite(room))
        {
            return double.PositiveInfinity;
        }

        if (reserveBar)
        {
            room -= bar;
        }

        return Math.Max(Math.Max(1, floor), room);
    }

    private static List<IReadOnlyList<int>> Pack(
        IReadOnlyList<double> widths,
        double tileHeight,
        double gap,
        double limit,
        bool horizontal)
    {
        var bands = new List<IReadOnlyList<int>>();
        var current = new List<int>();
        var extent = 0d;
        for (var i = 0; i < widths.Count; i++)
        {
            var item = horizontal ? widths[i] : tileHeight;
            var next = current.Count == 0 ? item : extent + gap + item;
            if (current.Count > 0 && next > limit + 0.5)
            {
                bands.Add(current.ToArray());
                current = [i];
                extent = item;
                continue;
            }

            current.Add(i);
            extent = next;
        }

        if (current.Count > 0)
        {
            bands.Add(current.ToArray());
        }

        return bands;
    }

    private readonly record struct GridSize(double Width, double Height);

    private static GridSize Measure(
        IReadOnlyList<IReadOnlyList<int>> bands,
        IReadOnlyList<double> widths,
        double tileHeight,
        double gap,
        bool horizontal)
    {
        if (bands.Count == 0)
        {
            return new GridSize(0, 0);
        }

        if (horizontal)
        {
            var width = 0d;
            foreach (var band in bands)
            {
                var row = 0d;
                for (var i = 0; i < band.Count; i++)
                {
                    row += widths[band[i]];
                    if (i > 0)
                    {
                        row += gap;
                    }
                }

                width = Math.Max(width, row);
            }

            var height = (bands.Count * tileHeight) + (Math.Max(0, bands.Count - 1) * gap);
            return new GridSize(width, height);
        }

        var totalWidth = 0d;
        var columnHeight = 0d;
        for (var bandIndex = 0; bandIndex < bands.Count; bandIndex++)
        {
            var band = bands[bandIndex];
            var columnWidth = 0d;
            foreach (var index in band)
            {
                columnWidth = Math.Max(columnWidth, widths[index]);
            }

            totalWidth += columnWidth;
            if (bandIndex > 0)
            {
                totalWidth += gap;
            }

            var height = (band.Count * tileHeight) + (Math.Max(0, band.Count - 1) * gap);
            columnHeight = Math.Max(columnHeight, height);
        }

        return new GridSize(totalWidth, columnHeight);
    }
}
