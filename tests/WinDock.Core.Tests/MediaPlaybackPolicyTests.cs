using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class MediaPlaybackPolicyTests
{
    [Fact]
    public void Playing_smtc_wins_over_a_local_player()
    {
        Assert.Equal(
            MediaPlaybackSource.GsmPlaying,
            MediaPlaybackPolicy.Choose(gsmPlaying: true, localTitled: true, gsmIdle: true, localIdle: true));
    }

    [Fact]
    public void Titled_local_player_wins_over_paused_smtc()
    {
        Assert.Equal(
            MediaPlaybackSource.LocalTitled,
            MediaPlaybackPolicy.Choose(gsmPlaying: false, localTitled: true, gsmIdle: true, localIdle: true));
    }

    [Fact]
    public void Paused_smtc_wins_over_an_idle_local_process()
    {
        Assert.Equal(
            MediaPlaybackSource.GsmIdle,
            MediaPlaybackPolicy.Choose(gsmPlaying: false, localTitled: false, gsmIdle: true, localIdle: true));
    }

    [Fact]
    public void Idle_local_process_is_used_when_smtc_is_empty()
    {
        Assert.Equal(
            MediaPlaybackSource.LocalIdle,
            MediaPlaybackPolicy.Choose(gsmPlaying: false, localTitled: false, gsmIdle: false, localIdle: true));
    }

    [Fact]
    public void Empty_when_nothing_is_playing()
    {
        Assert.Equal(
            MediaPlaybackSource.None,
            MediaPlaybackPolicy.Choose(false, false, false, false));
    }

    [Fact]
    public void Gsm_commands_follow_controls_not_the_player_name()
    {
        Assert.True(MediaPlaybackPolicy.UseGsmCommands(hasGsmTransport: true));
        Assert.False(MediaPlaybackPolicy.UseGsmCommands(hasGsmTransport: false));
        Assert.True(KnownLocalPlayers.PrefersOwnTransport("cloudmusic"));
        Assert.True(MediaPlaybackPolicy.UseGsmCommands(true));
    }

    [Fact]
    public void Local_snapshot_with_a_window_title_counts_as_titled()
    {
        var titled = new MediaSessionSnapshot
        {
            SourceAppId = KnownLocalPlayers.SourceId("cloudmusic"),
            IsPlaying = true,
            Title = "起风了",
            Artist = "网易云音乐",
            UsesFallbackTransport = true
        };
        var idle = new MediaSessionSnapshot
        {
            SourceAppId = KnownLocalPlayers.SourceId("cloudmusic"),
            IsPlaying = true,
            Title = "网易云音乐",
            Artist = "未接入系统媒体，可用按钮控制",
            UsesFallbackTransport = true
        };

        Assert.True(MediaPlaybackPolicy.IsLocalTitled(titled));
        Assert.False(MediaPlaybackPolicy.IsLocalTitled(idle));
        Assert.False(MediaPlaybackPolicy.IsLocalTitled(null));
    }
}
