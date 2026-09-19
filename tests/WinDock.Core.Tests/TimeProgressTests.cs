using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class TimeProgressTests
{
    [Fact]
    public void Year_progress_on_september_14_is_about_70_percent()
    {
        var now = new DateTime(2026, 9, 14, 12, 0, 0);
        Assert.Equal(70, TimeProgressFormat.Percent(now, TimeProgressPeriods.Year));
        Assert.Equal("2026", TimeProgressFormat.Caption(now, TimeProgressPeriods.Year));
        Assert.Equal("70%", TimeProgressFormat.PercentLabel(now, TimeProgressPeriods.Year));
        Assert.Equal("年", TimeProgressFormat.PeriodTitle("nope"));
        Assert.Equal("条形", TimeProgressFormat.LayoutTitle("nope"));
    }

    [Fact]
    public void Month_and_day_use_their_own_bounds()
    {
        var noon = new DateTime(2026, 9, 14, 12, 0, 0);
        Assert.Equal("9月", TimeProgressFormat.Caption(noon, TimeProgressPeriods.Month));
        Assert.Equal("14日", TimeProgressFormat.Caption(noon, TimeProgressPeriods.Day));
        Assert.Equal(45, TimeProgressFormat.Percent(noon, TimeProgressPeriods.Month));
        Assert.Equal(50, TimeProgressFormat.Percent(noon, TimeProgressPeriods.Day));
        Assert.Equal(0, TimeProgressFormat.Percent(noon.Date, TimeProgressPeriods.Day));
        Assert.Equal(0, TimeProgressFormat.Percent(new DateTime(2026, 1, 1), TimeProgressPeriods.Year));
    }

    [Fact]
    public void Settings_normalize_unknown_period_and_layout()
    {
        var settings = TimeProgressSettings.Normalize(new TimeProgressSettings
        {
            Period = "week",
            Layout = "wave"
        });
        Assert.Equal(TimeProgressPeriods.Year, settings.Period);
        Assert.Equal(TimeProgressLayouts.Bars, settings.Layout);
        Assert.Equal(TimeProgressPeriods.Month, TimeProgressPeriods.Normalize("month"));
        Assert.Equal(TimeProgressLayouts.Ring, TimeProgressLayouts.Normalize("ring"));
        Assert.Equal("百分比", TimeProgressFormat.LayoutTitle(TimeProgressLayouts.Percentage));
        Assert.Equal("圆环", TimeProgressFormat.LayoutTitle(TimeProgressLayouts.Ring));
        Assert.Equal("月", TimeProgressFormat.PeriodTitle(TimeProgressPeriods.Month));
        Assert.Equal("日", TimeProgressFormat.PeriodTitle(TimeProgressPeriods.Day));
    }
}
