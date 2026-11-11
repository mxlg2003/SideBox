namespace WinDock.Core;

public readonly record struct KnownLocalPlayer(string ProcessName, string DisplayName);

/// <summary>
/// Win32 players that often skip GSMTC or publish a session without titles or transport.
/// Sidebox can still read their window title and send documented media commands.
/// </summary>
public static class KnownLocalPlayers
{
    public const string SourcePrefix = "local:";

    public static readonly IReadOnlyList<KnownLocalPlayer> All =
    [
        new("cloudmusic", "网易云音乐"),
        new("PotPlayerMini64", "PotPlayer"),
        new("PotPlayerMini", "PotPlayer"),
        new("PotPlayer64", "PotPlayer"),
        new("PotPlayer", "PotPlayer"),
        new("spotify", "Spotify"),
        new("QQMusic", "QQ音乐"),
        new("kugou", "酷狗音乐"),
        new("kwmusic", "酷我音乐")
    ];

    public static string SourceId(string processName) => SourcePrefix + processName;

    public static string? ProcessNameFromSourceId(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId)
            || !sourceAppId.StartsWith(SourcePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = sourceAppId[SourcePrefix.Length..];
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public static IReadOnlyList<string> ProcessNamesForSource(string? sourceAppId)
    {
        var local = ProcessNameFromSourceId(sourceAppId);
        if (local is not null)
        {
            return [local];
        }

        if (string.IsNullOrWhiteSpace(sourceAppId))
        {
            return [];
        }

        if (IsPotPlayerSource(sourceAppId))
        {
            return PotPlayerProcessNames();
        }

        foreach (var player in All)
        {
            if (sourceAppId.Contains(player.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return [player.ProcessName];
            }
        }

        return [];
    }

    public static string? DisplayNameForSource(string? sourceAppId)
    {
        var names = ProcessNamesForSource(sourceAppId);
        if (names.Count == 0)
        {
            return IsPotPlayerSource(sourceAppId) ? "PotPlayer" : null;
        }

        foreach (var player in All)
        {
            if (string.Equals(player.ProcessName, names[0], StringComparison.OrdinalIgnoreCase))
            {
                return player.DisplayName;
            }
        }

        return null;
    }

    public static bool PrefersOwnTransport(string? sourceAppId)
    {
        if (IsPotPlayerSource(sourceAppId))
        {
            return true;
        }

        foreach (var name in ProcessNamesForSource(sourceAppId))
        {
            if (IsPotPlayerProcess(name) || string.Equals(name, "cloudmusic", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPotPlayerSource(string? sourceAppId) =>
        !string.IsNullOrWhiteSpace(sourceAppId)
        && sourceAppId.Contains("potplayer", StringComparison.OrdinalIgnoreCase);

    public static bool IsPotPlayerProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName)
        && processName.Contains("potplayer", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> PotPlayerProcessNames()
    {
        var names = new List<string>();
        foreach (var player in All)
        {
            if (IsPotPlayerProcess(player.ProcessName))
            {
                names.Add(player.ProcessName);
            }
        }

        return names;
    }
}
