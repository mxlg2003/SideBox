namespace WinDock.Core;

public static class ProcessSorts
{
    public const string Cpu = "cpu";
    public const string Memory = "memory";

    public static readonly IReadOnlyList<string> All = [Cpu, Memory];

    public static string Normalize(string? sort) => sort switch
    {
        Memory => Memory,
        _ => Cpu
    };
}

public sealed record ProcessRow
{
    public required int Pid { get; init; }

    public required string Name { get; init; }

    public string? ImagePath { get; init; }

    public string? IconPath { get; init; }

    public double CpuPercent { get; init; }

    public ulong WorkingSetBytes { get; init; }
}

public sealed record ProcessListSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public bool Allowed { get; init; } = true;

    public IReadOnlyList<ProcessRow> Rows { get; init; } = [];
}

public sealed record ProcessWidgetSettings
{
    public string Sort { get; init; } = ProcessSorts.Cpu;

    public static ProcessWidgetSettings Default { get; } = new();

    public static ProcessWidgetSettings Normalize(ProcessWidgetSettings? settings)
    {
        var value = settings ?? Default;
        return value with { Sort = ProcessSorts.Normalize(value.Sort) };
    }
}

public static class ProcessFormat
{
    public const int ListLength = 5;

    public static string SortTitle(string sort) => ProcessSorts.Normalize(sort) switch
    {
        ProcessSorts.Memory => UiText.Memory,
        _ => "CPU"
    };

    public static string Cpu(double percent) => $"{Math.Clamp(percent, 0, 999):0.0}%";

    public static string Memory(ulong workingSetBytes)
    {
        var mb = workingSetBytes / (1024.0 * 1024.0);
        return mb >= 1024
            ? $"{mb / 1024.0:0.0} GB"
            : $"{mb:0} MB";
    }

    public static string Metric(ProcessRow row, string sort) =>
        ProcessSorts.Normalize(sort) == ProcessSorts.Memory
            ? Memory(row.WorkingSetBytes)
            : Cpu(row.CpuPercent);

    public static IReadOnlyList<ProcessRow> Rank(IReadOnlyList<ProcessRow> rows, string sort)
    {
        var key = ProcessSorts.Normalize(sort);
        var ordered = key == ProcessSorts.Memory
            ? rows.OrderByDescending(row => row.WorkingSetBytes)
            : rows.OrderByDescending(row => row.CpuPercent);
        return ordered.Take(ListLength).ToArray();
    }
}
