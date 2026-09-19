namespace WinDock.Core;

public static class TimeProgressPeriods
{
    public const string Year = "year";
    public const string Month = "month";
    public const string Day = "day";

    public static readonly IReadOnlyList<string> All = [Year, Month, Day];

    public static string Normalize(string? period) => period switch
    {
        Month => Month,
        Day => Day,
        _ => Year
    };
}

public static class TimeProgressLayouts
{
    public const string Bars = "bars";
    public const string Percentage = "percentage";
    public const string Ring = "ring";

    public static readonly IReadOnlyList<string> All = [Bars, Percentage, Ring];

    public static string Normalize(string? layout) => layout switch
    {
        Percentage => Percentage,
        Ring => Ring,
        _ => Bars
    };
}

public sealed record TimeProgressSettings
{
    public string Period { get; init; } = TimeProgressPeriods.Year;

    public string Layout { get; init; } = TimeProgressLayouts.Bars;

    public static TimeProgressSettings Default { get; } = new();

    public static TimeProgressSettings Normalize(TimeProgressSettings? settings)
    {
        var value = settings ?? Default;
        return value with
        {
            Period = TimeProgressPeriods.Normalize(value.Period),
            Layout = TimeProgressLayouts.Normalize(value.Layout)
        };
    }
}

public static class TimeProgressFormat
{
    public static string PeriodTitle(string period) => TimeProgressPeriods.Normalize(period) switch
    {
        TimeProgressPeriods.Month => "月",
        TimeProgressPeriods.Day => "日",
        _ => "年"
    };

    public static string LayoutTitle(string layout) => TimeProgressLayouts.Normalize(layout) switch
    {
        TimeProgressLayouts.Percentage => "百分比",
        TimeProgressLayouts.Ring => "圆环",
        _ => "条形"
    };

    public static string Caption(DateTime now, string period) => TimeProgressPeriods.Normalize(period) switch
    {
        TimeProgressPeriods.Month => $"{now.Month}月",
        TimeProgressPeriods.Day => $"{now.Day}日",
        _ => now.Year.ToString()
    };

    public static string PercentLabel(DateTime now, string period) => $"{Percent(now, period)}%";

    public static int Percent(DateTime now, string period) =>
        (int)Math.Clamp(Math.Round(Fraction(now, period) * 100), 0, 100);

    public static double Fraction(DateTime now, string period)
    {
        var (start, end) = Bounds(now, period);
        var span = (end - start).TotalSeconds;
        if (span <= 0)
        {
            return 0;
        }

        return Math.Clamp((now - start).TotalSeconds / span, 0, 1);
    }

    public static (DateTime Start, DateTime End) Bounds(DateTime now, string period)
    {
        var local = DateTime.SpecifyKind(now, now.Kind == DateTimeKind.Unspecified ? DateTimeKind.Local : now.Kind);
        return TimeProgressPeriods.Normalize(period) switch
        {
            TimeProgressPeriods.Month => (new DateTime(local.Year, local.Month, 1, 0, 0, 0, local.Kind),
                new DateTime(local.Year, local.Month, 1, 0, 0, 0, local.Kind).AddMonths(1)),
            TimeProgressPeriods.Day => (local.Date, local.Date.AddDays(1)),
            _ => (new DateTime(local.Year, 1, 1, 0, 0, 0, local.Kind),
                new DateTime(local.Year, 1, 1, 0, 0, 0, local.Kind).AddYears(1))
        };
    }
}
