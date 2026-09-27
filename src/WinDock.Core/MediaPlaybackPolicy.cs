namespace WinDock.Core;

public enum MediaPlaybackSource
{
    None,
    GsmPlaying,
    LocalTitled,
    GsmIdle,
    LocalIdle
}

/// <summary>
/// Picks now-playing source and whether transport should go through GSMTC.
/// Playing SMTC wins; a local player with a real window title beats a paused SMTC
/// session; paused SMTC beats an idle local process.
/// </summary>
public static class MediaPlaybackPolicy
{
    public static MediaPlaybackSource Choose(
        bool gsmPlaying,
        bool localTitled,
        bool gsmIdle,
        bool localIdle)
    {
        if (gsmPlaying)
        {
            return MediaPlaybackSource.GsmPlaying;
        }

        if (localTitled)
        {
            return MediaPlaybackSource.LocalTitled;
        }

        if (gsmIdle)
        {
            return MediaPlaybackSource.GsmIdle;
        }

        if (localIdle)
        {
            return MediaPlaybackSource.LocalIdle;
        }

        return MediaPlaybackSource.None;
    }

    public static bool UseGsmCommands(bool hasGsmTransport) => hasGsmTransport;

    public static bool IsLocalTitled(MediaSessionSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.UsesFallbackTransport is false)
        {
            return false;
        }

        var display = KnownLocalPlayers.DisplayNameForSource(snapshot.SourceAppId);
        return PlayerWindowTitle.LooksLikeMediaTitle(snapshot.Title, display);
    }
}
