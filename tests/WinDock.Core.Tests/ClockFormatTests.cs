using System.Globalization;
using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class ClockFormatTests
{
    private static readonly DateTime Sample = new(2026, 9, 14, 1, 31, 22);

    [Fact]
    public void Digital_times_use_leading_zeros()
    {
        Assert.Equal("01:31", ClockFormat.Time(Sample));
        Assert.Equal("01:31:22", ClockFormat.TimeWithSeconds(Sample));
    }

    [Fact]
    public void Dates_match_dockset_layout()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("Sep 14", ClockFormat.CompactDate(Sample, en));
        Assert.Equal("Mon, Sep 14", ClockFormat.ExpandedDate(Sample, en));
        Assert.Equal("Monday, September 14", ClockFormat.FullDate(Sample, en));
    }

    [Fact]
    public void China_zone_line_uses_shanghai()
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }

        Assert.Equal("上海", ClockFormat.PlaceName(zone));
        Assert.Equal("上海 · GMT+08:00", ClockFormat.ZoneLine(zone, Sample));
    }

    [Fact]
    public void Second_zone_converts_from_local()
    {
        Assert.Contains(ClockFormat.ZoneChoices, choice => choice.Place == "纽约");
        var utc = ClockFormat.Resolve("UTC");
        Assert.NotNull(utc);
        var local = new DateTime(2026, 9, 14, 16, 0, 0, DateTimeKind.Unspecified);
        var converted = ClockFormat.ConvertLocal(local, utc);
        Assert.Equal(converted, TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, utc));
    }
}
