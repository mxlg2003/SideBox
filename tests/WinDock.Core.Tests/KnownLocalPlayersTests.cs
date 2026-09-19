using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class KnownLocalPlayersTests
{
    [Fact]
    public void NetEase_is_first_known_player()
    {
        Assert.Equal("cloudmusic", KnownLocalPlayers.All[0].ProcessName);
        Assert.Equal("网易云音乐", KnownLocalPlayers.All[0].DisplayName);
    }

    [Fact]
    public void Source_id_round_trips_process_name()
    {
        var id = KnownLocalPlayers.SourceId("cloudmusic");
        Assert.Equal("local:cloudmusic", id);
        Assert.Equal("cloudmusic", KnownLocalPlayers.ProcessNameFromSourceId(id));
    }

    [Fact]
    public void Gsm_aumid_is_not_a_local_source()
    {
        Assert.Null(KnownLocalPlayers.ProcessNameFromSourceId("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic"));
        Assert.Null(KnownLocalPlayers.ProcessNameFromSourceId(null));
        Assert.Null(KnownLocalPlayers.ProcessNameFromSourceId("local:"));
    }

    [Fact]
    public void PotPlayer_exe_path_resolves_to_mini64_and_siblings()
    {
        var names = KnownLocalPlayers.ProcessNamesForSource(
            @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe");
        Assert.Contains("PotPlayerMini64", names);
        Assert.Contains("PotPlayerMini", names);
        Assert.Equal("PotPlayer", KnownLocalPlayers.DisplayNameForSource(names[0]));
        Assert.True(KnownLocalPlayers.PrefersOwnTransport("PotPlayerMini64.exe"));
    }

    [Fact]
    public void PotPlayer_window_title_strips_player_suffix()
    {
        Assert.Equal("RISE.mkv", PlayerWindowTitle.Sanitize("RISE.mkv - PotPlayer Mini (64-bit)"));
        Assert.Equal("episode 03", PlayerWindowTitle.Sanitize("episode 03 - PotPlayer"));
        Assert.True(PlayerWindowTitle.LooksLikeMediaTitle("RISE.mkv", "PotPlayer"));
        Assert.False(PlayerWindowTitle.LooksLikeMediaTitle("PotPlayer", "PotPlayer"));
    }
}
