namespace WinDock.Core;

public static class SysActivityLayouts
{
    public const string Numbers = "numbers";
    public const string Rings = "rings";
    public const string Bars = "bars";

    public static readonly IReadOnlyList<string> All = [Numbers, Rings, Bars];

    public static string Normalize(string? layout) => layout switch
    {
        Rings => Rings,
        Bars => Bars,
        _ => Numbers
    };
}

public sealed record SysActivitySettings
{
    public string Layout { get; init; } = SysActivityLayouts.Numbers;

    public static SysActivitySettings Default { get; } = new();

    public static SysActivitySettings Normalize(SysActivitySettings? settings)
    {
        var value = settings ?? Default;
        return value with { Layout = SysActivityLayouts.Normalize(value.Layout) };
    }
}

public static class SysActivityFormat
{
    private const double Gib = 1024.0 * 1024.0 * 1024.0;

    public static string LayoutTitle(string layout) => SysActivityLayouts.Normalize(layout) switch
    {
        SysActivityLayouts.Rings => UiLocale.T("圆环", "Rings"),
        SysActivityLayouts.Bars => UiLocale.T("条形", "Bars"),
        _ => UiLocale.T("数字", "Numbers")
    };

    public static string Percent(double percent, bool available) =>
        available ? $"{Math.Clamp(Math.Round(percent), 0, 100):0}%" : "—";

    public static double Fraction(double percent, bool available) =>
        available ? Math.Clamp(percent / 100.0, 0, 1) : 0;

    public static string MemoryUsed(ulong usedBytes, ulong totalBytes)
    {
        if (totalBytes == 0)
        {
            return "—";
        }

        var used = usedBytes / Gib;
        var total = totalBytes / Gib;
        var totalText = Math.Abs(total - Math.Round(total)) < 0.05
            ? Math.Round(total).ToString("0")
            : total.ToString("0.0");
        return $"{used:0.0} / {totalText} GiB";
    }
}
