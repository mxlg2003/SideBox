namespace WinDock.Core;

public static class NetworkShows
{
    public const string Both = "both";
    public const string Download = "download";
    public const string Upload = "upload";

    public static readonly IReadOnlyList<string> All = [Both, Download, Upload];

    public static string Normalize(string? show) => show switch
    {
        Download => Download,
        Upload => Upload,
        _ => Both
    };
}

public sealed record NetworkWidgetSettings
{
    public string Show { get; init; } = NetworkShows.Both;

    public bool ChartInDock { get; init; } = true;

    public bool ChartInPopup { get; init; } = true;

    public bool ShowWifiSignal { get; init; }

    public static NetworkWidgetSettings Default { get; } = new();

    public static NetworkWidgetSettings Normalize(NetworkWidgetSettings? settings)
    {
        var value = settings ?? Default;
        return value with { Show = NetworkShows.Normalize(value.Show) };
    }

    public bool ShowsDownload => NetworkShows.Normalize(Show) is NetworkShows.Both or NetworkShows.Download;

    public bool ShowsUpload => NetworkShows.Normalize(Show) is NetworkShows.Both or NetworkShows.Upload;
}

public static class NetworkFormat
{
    public static string ShowTitle(string show) => NetworkShows.Normalize(show) switch
    {
        NetworkShows.Download => UiText.Download,
        NetworkShows.Upload => UiText.Upload,
        _ => UiLocale.T("全部", "Both")
    };

    public static string Footer => UiLocale.T("最近 60 秒 · 每秒更新", "Last 60 seconds · updates every second");
}

public static class NetworkHistory
{
    public const int Length = 60;

    public static IReadOnlyList<double> Push(IReadOnlyList<double> history, double value, int length = Length)
    {
        var count = history.Count;
        if (count < length)
        {
            var grown = new double[count + 1];
            for (var i = 0; i < count; i++)
            {
                grown[i] = history[i];
            }

            grown[count] = value;
            return grown;
        }

        var next = new double[length];
        for (var i = 0; i < length - 1; i++)
        {
            next[i] = history[i + 1];
        }

        next[length - 1] = value;
        return next;
    }
}
