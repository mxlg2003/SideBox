using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class PinStackGridTests
{
    private const double Tile = 56;
    private const double Gap = DockTokens.TileGap;
    private const double PadX = DockTokens.TilePaddingX;
    private const double PadY = DockTokens.TilePaddingY;

    [Fact]
    public void Arrange_keeps_one_row_when_the_line_fits()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(3), horizontal: true, Room(3, rows: 2));

        Assert.Single(arrangement.Bands);
        Assert.Equal([0, 1, 2], arrangement.Bands[0]);
        Assert.False(arrangement.ScrollsVertically);
        Assert.False(arrangement.ScrollsHorizontally);
        Assert.Null(arrangement.ScrollWidth);
        Assert.Null(arrangement.ScrollHeight);
    }

    [Fact]
    public void Arrange_wraps_rows_in_pin_order_when_the_width_runs_out()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(4), horizontal: true, Room(3, rows: 3));

        Assert.Equal(2, arrangement.Bands.Count);
        Assert.Equal([0, 1, 2], arrangement.Bands[0]);
        Assert.Equal([3], arrangement.Bands[1]);
        Assert.False(arrangement.ScrollsVertically);
    }

    [Fact]
    public void Arrange_reserves_a_scrollbar_and_fits_fewer_icons_per_row()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(4), horizontal: true, Room(3, rows: 1));

        Assert.Equal([0, 1], arrangement.Bands[0]);
        Assert.Equal([2, 3], arrangement.Bands[1]);
        Assert.True(arrangement.ScrollsVertically);
        Assert.False(arrangement.ScrollsHorizontally);
        Assert.Equal(Row(2) + DockTokens.Settings.ScrollThickness, arrangement.ScrollWidth);
        Assert.Equal(DockTokens.TileHeight, arrangement.ScrollHeight);
    }

    [Fact]
    public void Arrange_packs_variable_widths_without_splitting_an_icon()
    {
        var arrangement = PinStackGrid.Arrange([80, 56, 56], horizontal: true, new PinStackGrid.Viewport(150 + (PadX * 2), 400));

        Assert.Equal([0, 1], arrangement.Bands[0]);
        Assert.Equal([2], arrangement.Bands[1]);
        Assert.False(arrangement.ScrollsVertically);
    }

    [Fact]
    public void Arrange_wraps_side_docks_into_columns_and_scrolls_sideways()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(5), horizontal: false, ColumnRoom(perColumn: 2, columns: 2));

        Assert.Equal(5, arrangement.Bands.Count);
        Assert.All(arrangement.Bands, band => Assert.Single(band));
        Assert.Equal([0], arrangement.Bands[0]);
        Assert.Equal([4], arrangement.Bands[4]);
        Assert.True(arrangement.ScrollsHorizontally);
        Assert.False(arrangement.ScrollsVertically);
    }

    [Fact]
    public void Arrange_keeps_one_column_when_the_side_dock_has_room()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(2), horizontal: false, ColumnRoom(perColumn: 4, columns: 3));

        Assert.Equal([0, 1], arrangement.Bands[0]);
        Assert.False(arrangement.ScrollsHorizontally);
        Assert.False(arrangement.ScrollsVertically);
    }

    [Fact]
    public void Arrange_stays_one_line_when_the_monitor_is_unknown()
    {
        var arrangement = PinStackGrid.Arrange(Tiles(8), horizontal: true, PinStackGrid.Viewport.Unbounded);

        Assert.Single(arrangement.Bands);
        Assert.Equal(8, arrangement.Bands[0].Count);
        Assert.False(arrangement.ScrollsVertically);
    }

    [Fact]
    public void Budget_for_a_top_dock_uses_the_width_and_the_space_below_the_island()
    {
        var work = new RectLogical(0, 0, 1000, 800);
        var island = new RectLogical(100, 10, 400, 62);
        var budget = PinStackGrid.Budget(work, island, DockEdge.Top);

        Assert.Equal(980, budget.MaxWidth);
        Assert.Equal(710, budget.MaxHeight);
    }

    [Fact]
    public void Budget_for_a_right_dock_uses_the_space_to_the_left_of_the_island()
    {
        var work = new RectLogical(0, 0, 1000, 800);
        var island = new RectLogical(928, 40, 62, 300);
        var budget = PinStackGrid.Budget(work, island, DockEdge.Right);

        Assert.Equal(910, budget.MaxWidth);
        Assert.Equal(780, budget.MaxHeight);
    }

    [Fact]
    public void Budget_keeps_one_icon_visible_when_the_island_fills_the_edge()
    {
        var work = new RectLogical(0, 0, 200, 80);
        var island = new RectLogical(10, 10, 180, 62);
        var budget = PinStackGrid.Budget(work, island, DockEdge.Top);

        Assert.Equal(180, budget.MaxWidth);
        Assert.Equal(DockTokens.TileHeight + (PadY * 2), budget.MaxHeight);
    }

    private static PinStackGrid.Viewport Room(int iconsPerRow, int rows)
    {
        var width = (PadX * 2) + Row(iconsPerRow);
        var height = (PadY * 2) + (rows * DockTokens.TileHeight) + (Math.Max(0, rows - 1) * Gap);
        return new PinStackGrid.Viewport(width, height);
    }

    private static PinStackGrid.Viewport ColumnRoom(int perColumn, int columns)
    {
        var height = (PadY * 2) + (perColumn * DockTokens.TileHeight) + (Math.Max(0, perColumn - 1) * Gap);
        var width = (PadX * 2) + (columns * Tile) + (Math.Max(0, columns - 1) * Gap);
        return new PinStackGrid.Viewport(width, height);
    }

    private static double Row(int count) => (count * Tile) + (Math.Max(0, count - 1) * Gap);

    private static double[] Tiles(int count)
    {
        var widths = new double[count];
        Array.Fill(widths, Tile);
        return widths;
    }
}
