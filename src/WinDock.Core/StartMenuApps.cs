namespace WinDock.Core;

public sealed record StartMenuApp
{
    public required string Name { get; init; }

    public required string LnkPath { get; init; }
}

public static class StartMenuApps
{
    public static bool IsUninstallName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        return name.Contains("卸载", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<StartMenuApp> Collect(IEnumerable<StartMenuApp> apps)
    {
        var chosen = new Dictionary<string, StartMenuApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
        {
            if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.LnkPath))
            {
                continue;
            }

            if (IsUninstallName(app.Name) || chosen.ContainsKey(app.Name))
            {
                continue;
            }

            chosen[app.Name] = app;
        }

        return chosen.Values
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
