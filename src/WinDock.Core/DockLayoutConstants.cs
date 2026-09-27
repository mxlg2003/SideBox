namespace WinDock.Core;

/// <summary>Pure domain layout and timing constants without platform or visual rendering dependencies.</summary>
public static class DockLayoutConstants
{
    public const double IslandMargin = 10;
    public const double IslandPaddingX = 5;
    public const double IslandPaddingY = 5;
    public const double MinIslandLength = 160;

    public const double TileHeight = 50;
    public const double TileGap = 3;
    public const double TilePaddingX = 10;
    public const double TilePaddingY = 6;

    public const double AutoHideStrip = 4;
    public const int AutoHideIdleMs = 800;
    public const int WakeHoldMs = 2000;
    public const double FlyoutOffset = 8;

    public const int DefaultIslandTransparency = 50;
    public const int FrostedIslandTransparency = 0;
    public const int GlassIslandTransparency = 80;
    public const int MinIslandAlpha = 12;
    public const int MaxIslandAlpha = 220;

    public static int TransparencyFor(DockBackdrop backdrop) => backdrop switch
    {
        DockBackdrop.Frosted => FrostedIslandTransparency,
        DockBackdrop.Glass => GlassIslandTransparency,
        _ => DefaultIslandTransparency
    };

    public static int EffectiveTransparency(DockBackdrop backdrop, int transparency)
    {
        var t = Math.Clamp(transparency, 0, 100);
        return backdrop switch
        {
            DockBackdrop.Mica => t * 55 / 100,
            DockBackdrop.Frosted => t * 78 / 100,
            _ => t
        };
    }
}
