namespace WinDock.Core;

public static class WhiteNoiseTracks
{
    public const string Rain = "rain";
    public const string Thunder = "thunder";
    public const string Waves = "waves";
    public const string Campfire = "campfire";
    public const string Wind = "wind";
    public const string WhiteNoise = "whitenoise";
    public const string PinkNoise = "pinknoise";
    public const string BrownNoise = "brownnoise";
    public const string Cafe = "cafe";
    public const string Keyboard = "keyboard";
    public const string Clock = "clock";

    public static readonly IReadOnlyList<string> All =
    [
        Rain,
        Thunder,
        Waves,
        Campfire,
        Wind,
        WhiteNoise,
        PinkNoise,
        BrownNoise,
        Cafe,
        Keyboard,
        Clock
    ];

    public static string DisplayName(string trackId) => trackId switch
    {
        Rain => UiText.SoundRain,
        Thunder => UiText.SoundThunder,
        Waves => UiText.SoundWaves,
        Campfire => UiText.SoundCampfire,
        Wind => UiText.SoundWind,
        WhiteNoise => UiText.SoundWhiteNoise,
        PinkNoise => UiText.SoundPinkNoise,
        BrownNoise => UiText.SoundBrownNoise,
        Cafe => UiText.SoundCafe,
        Keyboard => UiText.SoundKeyboard,
        Clock => UiText.SoundClock,
        _ => trackId
    };

    public static string Glyph(string trackId) => trackId switch
    {
        Rain => PhosphorIcon.Drop,
        Thunder => PhosphorIcon.Lightning,
        Waves => PhosphorIcon.Wind,
        Campfire => PhosphorIcon.Sun,
        Wind => PhosphorIcon.Wind,
        WhiteNoise => PhosphorIcon.Headphones,
        PinkNoise => PhosphorIcon.Headphones,
        BrownNoise => PhosphorIcon.Headphones,
        Cafe => PhosphorIcon.Notepad,
        Keyboard => PhosphorIcon.Keyboard,
        Clock => PhosphorIcon.Clock,
        _ => PhosphorIcon.Headphones
    };
}

public static class WhiteNoisePresets
{
    public const string RainyStudy = "rainy_study";
    public const string DeepForest = "deep_forest";
    public const string WarmFire = "warm_fire";
    public const string OceanBreeze = "ocean_breeze";
    public const string PureWhite = "pure_white";
    public const string DeepBrown = "deep_brown";
    public const string CoffeeShop = "coffee_shop";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All =
    [
        RainyStudy,
        DeepForest,
        WarmFire,
        OceanBreeze,
        PureWhite,
        DeepBrown,
        CoffeeShop
    ];

    public static string DisplayName(string presetId) => presetId switch
    {
        RainyStudy => UiText.PresetRainyStudy,
        DeepForest => UiText.PresetDeepForest,
        WarmFire => UiText.PresetWarmFire,
        OceanBreeze => UiText.PresetOceanBreeze,
        PureWhite => UiText.PresetPureFocus,
        DeepBrown => UiText.PresetDeepBrown,
        CoffeeShop => UiText.PresetCoffeeShop,
        Custom => UiText.WhiteNoiseCustom,
        _ => string.IsNullOrWhiteSpace(presetId) ? UiText.WhiteNoiseCustom : presetId
    };

    public static IReadOnlyList<WhiteNoiseTrackSettings> GetTracksForPreset(string presetId) => presetId switch
    {
        RainyStudy =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = 0.8, Enabled = true },
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Thunder, Volume = 0.35, Enabled = true }
        ],
        DeepForest =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Wind, Volume = 0.7, Enabled = true },
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = 0.3, Enabled = true }
        ],
        WarmFire =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Campfire, Volume = 0.8, Enabled = true },
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Wind, Volume = 0.2, Enabled = true }
        ],
        OceanBreeze =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Waves, Volume = 0.85, Enabled = true },
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Wind, Volume = 0.25, Enabled = true }
        ],
        PureWhite =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.WhiteNoise, Volume = 0.65, Enabled = true }
        ],
        DeepBrown =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.BrownNoise, Volume = 0.75, Enabled = true }
        ],
        CoffeeShop =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Cafe, Volume = 0.75, Enabled = true },
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Keyboard, Volume = 0.3, Enabled = true }
        ],
        _ =>
        [
            new WhiteNoiseTrackSettings { Id = WhiteNoiseTracks.Rain, Volume = 0.8, Enabled = true }
        ]
    };
}

public sealed record WhiteNoiseTrackSettings
{
    public required string Id { get; init; }

    public double Volume { get; init; } = 0.5;

    public bool Enabled { get; init; }
}

public sealed record WhiteNoiseSettings
{
    public bool IsPlaying { get; init; }

    public double MasterVolume { get; init; } = 0.75;

    public string ActivePreset { get; init; } = WhiteNoisePresets.RainyStudy;

    public int TimerMinutes { get; init; }

    public bool LinkWithFocusTimer { get; init; } = true;

    public bool FadeInOut { get; init; } = true;

    public IReadOnlyList<WhiteNoiseTrackSettings> Tracks { get; init; } = [];

    public static WhiteNoiseSettings Default { get; } = CreateDefault();

    public static WhiteNoiseSettings CreateDefault() => new()
    {
        IsPlaying = false,
        MasterVolume = 0.75,
        ActivePreset = WhiteNoisePresets.RainyStudy,
        TimerMinutes = 0,
        LinkWithFocusTimer = true,
        FadeInOut = true,
        Tracks = WhiteNoisePresets.GetTracksForPreset(WhiteNoisePresets.RainyStudy)
    };

    public static WhiteNoiseSettings Normalize(WhiteNoiseSettings? settings)
    {
        var value = settings ?? Default;
        var isValidPreset = string.Equals(value.ActivePreset, WhiteNoisePresets.Custom, StringComparison.Ordinal)
            || WhiteNoisePresets.All.Contains(value.ActivePreset, StringComparer.Ordinal);
        var preset = isValidPreset
            ? value.ActivePreset
            : (string.IsNullOrEmpty(value.ActivePreset) && value.Tracks?.Count > 0 ? WhiteNoisePresets.Custom : WhiteNoisePresets.RainyStudy);

        var tracks = (value.Tracks ?? []).Where(t => WhiteNoiseTracks.All.Contains(t.Id, StringComparer.Ordinal))
            .Select(t => t with
            {
                Volume = Math.Clamp(t.Volume, 0.0, 1.0)
            })
            .ToList();

        if (tracks.Count == 0)
        {
            tracks = WhiteNoisePresets.GetTracksForPreset(preset == WhiteNoisePresets.Custom ? WhiteNoisePresets.RainyStudy : preset).ToList();
            if (preset == WhiteNoisePresets.Custom)
            {
                preset = WhiteNoisePresets.RainyStudy;
            }
        }

        return value with
        {
            MasterVolume = Math.Clamp(value.MasterVolume, 0.0, 1.0),
            ActivePreset = preset,
            TimerMinutes = Math.Max(0, value.TimerMinutes),
            Tracks = tracks
        };
    }
}
