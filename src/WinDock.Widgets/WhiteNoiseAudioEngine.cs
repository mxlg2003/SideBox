using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class WhiteNoiseAudioEngine : IDisposable
{
    private readonly Dictionary<string, MediaPlayer> _channels = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _fadeTimer;
    private readonly DispatcherTimer _sleepTimer;
    private double _fadeMultiplier;
    private double _targetFadeMultiplier;
    private DateTimeOffset? _sleepDeadline;
    private bool _isDisposed;

    public WhiteNoiseAudioEngine()
    {
        Settings = WhiteNoiseSettings.CreateDefault();

        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _fadeTimer.Tick += OnFadeTick;

        _sleepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sleepTimer.Tick += OnSleepTick;

        FocusTimerEvents.StateChanged += OnFocusTimerStateChanged;
    }

    public WhiteNoiseSettings Settings { get; private set; }

    public bool IsPlaying { get; private set; }

    public TimeSpan RemainingSleepTime
    {
        get
        {
            if (_sleepDeadline is { } deadline && IsPlaying)
            {
                var rem = deadline - DateTimeOffset.Now;
                return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
            }

            return TimeSpan.Zero;
        }
    }

    public event EventHandler? PlaybackStateChanged;

    public event EventHandler? SleepTimerTick;

    public void ApplySettings(WhiteNoiseSettings settings)
    {
        Settings = WhiteNoiseSettings.Normalize(settings);
        UpdateChannels();
        if (Settings.TimerMinutes > 0 && IsPlaying && _sleepDeadline is null)
        {
            StartSleepTimer(Settings.TimerMinutes);
        }
    }

    public void Play()
    {
        if (_isDisposed)
        {
            return;
        }

        IsPlaying = true;
        UpdateChannels();

        if (Settings.TimerMinutes > 0 && _sleepDeadline is null)
        {
            StartSleepTimer(Settings.TimerMinutes);
        }

        if (Settings.FadeInOut)
        {
            _targetFadeMultiplier = 1.0;
            _fadeTimer.Start();
        }
        else
        {
            _fadeMultiplier = 1.0;
            ApplyVolumes();
        }

        foreach (var player in _channels.Values)
        {
            player.Play();
        }

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_isDisposed || !IsPlaying)
        {
            return;
        }

        if (Settings.FadeInOut && _fadeMultiplier > 0.05)
        {
            _targetFadeMultiplier = 0.0;
            _fadeTimer.Start();
        }
        else
        {
            StopImmediately();
        }
    }

    public void Toggle()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void SetMasterVolume(double volume)
    {
        Settings = Settings with { MasterVolume = Math.Clamp(volume, 0.0, 1.0) };
        ApplyVolumes();
    }

    public void SetTrackVolume(string trackId, double volume)
    {
        var found = false;
        var clamped = Math.Clamp(volume, 0.0, 1.0);
        var tracks = Settings.Tracks.Select(t =>
        {
            if (string.Equals(t.Id, trackId, StringComparison.Ordinal))
            {
                found = true;
                return t with { Volume = clamped, Enabled = clamped > 0.01 };
            }
            return t;
        }).ToList();

        if (!found)
        {
            tracks.Add(new WhiteNoiseTrackSettings { Id = trackId, Volume = clamped, Enabled = clamped > 0.01 });
        }

        Settings = Settings with { Tracks = tracks, ActivePreset = WhiteNoisePresets.Custom };
        UpdateChannels();
        ApplyVolumes();
    }

    public void ToggleTrack(string trackId)
    {
        var found = false;
        var tracks = Settings.Tracks.Select(t =>
        {
            if (string.Equals(t.Id, trackId, StringComparison.Ordinal))
            {
                found = true;
                var nextEnabled = !t.Enabled;
                var nextVol = nextEnabled && t.Volume <= 0.01 ? 0.5 : t.Volume;
                return t with { Enabled = nextEnabled, Volume = nextVol };
            }
            return t;
        }).ToList();

        if (!found)
        {
            tracks.Add(new WhiteNoiseTrackSettings { Id = trackId, Volume = 0.5, Enabled = true });
        }

        Settings = Settings with { Tracks = tracks, ActivePreset = WhiteNoisePresets.Custom };
        UpdateChannels();
        ApplyVolumes();
    }

    public void ApplyPreset(string presetId)
    {
        var presetTracks = WhiteNoisePresets.GetTracksForPreset(presetId);
        Settings = Settings with
        {
            ActivePreset = presetId,
            Tracks = presetTracks
        };

        UpdateChannels();
        ApplyVolumes();
        if (IsPlaying)
        {
            foreach (var player in _channels.Values)
            {
                player.Play();
            }
        }
    }

    public void SetSleepTimer(int minutes)
    {
        Settings = Settings with { TimerMinutes = minutes };
        if (minutes <= 0)
        {
            _sleepDeadline = null;
            _sleepTimer.Stop();
        }
        else
        {
            StartSleepTimer(minutes);
        }

        SleepTimerTick?.Invoke(this, EventArgs.Empty);
    }

    private void StartSleepTimer(int minutes)
    {
        _sleepDeadline = DateTimeOffset.Now.AddMinutes(minutes);
        _sleepTimer.Start();
    }

    private void UpdateChannels()
    {
        var enabledTracks = Settings.Tracks
            .Where(t => t.Enabled && t.Volume > 0.01)
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Remove stopped channels
        var toRemove = _channels.Keys.Where(id => !enabledTracks.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (_channels.TryGetValue(id, out var player))
            {
                player.Stop();
                player.Close();
                _channels.Remove(id);
            }
        }

        // Add newly enabled channels
        foreach (var trackId in enabledTracks)
        {
            if (!_channels.ContainsKey(trackId))
            {
                var filePath = ProceduralAudioGenerator.GetOrGenerateAudioFile(trackId);
                var player = new MediaPlayer();
                try
                {
                    player.Open(new Uri(filePath, UriKind.Absolute));
                    player.MediaEnded += (_, _) =>
                    {
                        player.Position = TimeSpan.Zero;
                        player.Play();
                    };
                    _channels[trackId] = player;
                    if (IsPlaying)
                    {
                        player.Play();
                    }
                }
                catch
                {
                }
            }
        }

        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        var master = Settings.MasterVolume * _fadeMultiplier;
        foreach (var track in Settings.Tracks)
        {
            if (_channels.TryGetValue(track.Id, out var player))
            {
                var targetVol = track.Enabled ? track.Volume * master : 0.0;
                player.Volume = Math.Clamp(targetVol, 0.0, 1.0);
            }
        }
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var delta = 0.05;
        if (_fadeMultiplier < _targetFadeMultiplier)
        {
            _fadeMultiplier = Math.Min(_targetFadeMultiplier, _fadeMultiplier + delta);
            ApplyVolumes();
            if (_fadeMultiplier >= _targetFadeMultiplier)
            {
                _fadeTimer.Stop();
            }
        }
        else if (_fadeMultiplier > _targetFadeMultiplier)
        {
            _fadeMultiplier = Math.Max(_targetFadeMultiplier, _fadeMultiplier - delta);
            ApplyVolumes();
            if (_fadeMultiplier <= _targetFadeMultiplier)
            {
                _fadeTimer.Stop();
                if (_fadeMultiplier <= 0.001)
                {
                    StopImmediately();
                }
            }
        }
        else
        {
            _fadeTimer.Stop();
        }
    }

    private void StopImmediately()
    {
        _isDisposed = false;
        IsPlaying = false;
        _fadeMultiplier = 0.0;
        _sleepDeadline = null;
        _sleepTimer.Stop();

        foreach (var player in _channels.Values)
        {
            player.Pause();
        }

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSleepTick(object? sender, EventArgs e)
    {
        if (_sleepDeadline is { } deadline)
        {
            if (DateTimeOffset.Now >= deadline)
            {
                _sleepDeadline = null;
                _sleepTimer.Stop();
                Pause();
            }

            SleepTimerTick?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFocusTimerStateChanged(bool isRunning, FocusTimerPhase phase)
    {
        if (!Settings.LinkWithFocusTimer || _isDisposed)
        {
            return;
        }

        if (isRunning && phase == FocusTimerPhase.Focus)
        {
            if (!IsPlaying)
            {
                Play();
            }
        }
        else
        {
            if (IsPlaying)
            {
                Pause();
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        FocusTimerEvents.StateChanged -= OnFocusTimerStateChanged;
        _fadeTimer.Stop();
        _sleepTimer.Stop();

        foreach (var player in _channels.Values)
        {
            try
            {
                player.Stop();
                player.Close();
            }
            catch
            {
            }
        }

        _channels.Clear();
    }
}
