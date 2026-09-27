using WinDock.Core;
using WinDock.Shell;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class VisualThemeAndTokensTests
{
    [Fact]
    public void DockLayoutConstants_transparency_evaluates_consistently()
    {
        Assert.Equal(50, DockLayoutConstants.DefaultIslandTransparency);
        Assert.Equal(0, DockLayoutConstants.FrostedIslandTransparency);
        Assert.Equal(80, DockLayoutConstants.GlassIslandTransparency);

        Assert.Equal(0, DockLayoutConstants.TransparencyFor(DockBackdrop.Frosted));
        Assert.Equal(80, DockLayoutConstants.TransparencyFor(DockBackdrop.Glass));
        Assert.Equal(50, DockLayoutConstants.TransparencyFor(DockBackdrop.Mica));

        // Effective transparency damping
        Assert.Equal(55, DockLayoutConstants.EffectiveTransparency(DockBackdrop.Mica, 100));
        Assert.Equal(78, DockLayoutConstants.EffectiveTransparency(DockBackdrop.Frosted, 100));
        Assert.Equal(100, DockLayoutConstants.EffectiveTransparency(DockBackdrop.Glass, 100));
    }

    [Fact]
    public void DockVisualTheme_colors_resolve_non_zero_alpha()
    {
        var darkMica = DockVisualTheme.ResolveIslandFill(isLight: false, backdropOk: true, DockBackdrop.Mica, 50);
        var (a, r, g, b) = DockVisualTheme.Split(darkMica);
        Assert.True(a > 0);

        var lightMenu = DockVisualTheme.ResolveMenuFill(isLight: true, backdropOk: true, DockBackdrop.Mica);
        var (menuA, _, _, _) = DockVisualTheme.Split(lightMenu);
        Assert.Equal(255, menuA);

        var tileDark = DockVisualTheme.ResolveTileFill(isLight: false, DockBackdrop.Glass);
        Assert.Equal(DockVisualTheme.Dark.TileGlass, tileDark);
    }
}
