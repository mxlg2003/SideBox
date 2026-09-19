using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class SysActivityTests
{
    [Fact]
    public void Settings_normalize_unknown_layout_to_numbers()
    {
        var settings = SysActivitySettings.Normalize(new SysActivitySettings { Layout = "wave" });
        Assert.Equal(SysActivityLayouts.Numbers, settings.Layout);
        Assert.Equal(SysActivityLayouts.Rings, SysActivityLayouts.Normalize("rings"));
        Assert.Equal(SysActivityLayouts.Bars, SysActivityLayouts.Normalize("bars"));
        Assert.Equal("数字", SysActivityFormat.LayoutTitle("nope"));
        Assert.Equal("圆环", SysActivityFormat.LayoutTitle(SysActivityLayouts.Rings));
        Assert.Equal("条形", SysActivityFormat.LayoutTitle(SysActivityLayouts.Bars));
    }

    [Fact]
    public void Percent_and_memory_used_match_dockset_copy()
    {
        Assert.Equal("24%", SysActivityFormat.Percent(24.4, true));
        Assert.Equal("—", SysActivityFormat.Percent(0, false));
        Assert.Equal(0.63, SysActivityFormat.Fraction(63, true), 3);
        Assert.Equal(0, SysActivityFormat.Fraction(80, false));
        Assert.Equal("—", SysActivityFormat.MemoryUsed(0, 0));
        var used = (ulong)Math.Round(10.1 * 1024d * 1024d * 1024d);
        const ulong total = 16UL * 1024 * 1024 * 1024;
        Assert.Equal("10.1 / 16 GiB", SysActivityFormat.MemoryUsed(used, total));
    }
}
