using System.Net.NetworkInformation;
using Windows.Media.Control;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class NetworkInterfaceSampler : INetworkProvider, IDisposable
{
    private readonly object _gate = new();
    private readonly System.Threading.Timer _timer;
    private Dictionary<string, (long Recv, long Sent, long Ticks)> _last = new(StringComparer.Ordinal);
    private IReadOnlyList<double> _recvHistory = [];
    private IReadOnlyList<double> _sendHistory = [];
    private bool _disposed;
    private int _started;
    private int _busy;

    public NetworkInterfaceSampler()
    {
        Current = new NetworkSnapshot { Timestamp = DateTimeOffset.UtcNow };
        _timer = new System.Threading.Timer(_ => Publish(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public NetworkSnapshot Current { get; private set; }

    public event EventHandler<NetworkSnapshot>? Updated;

    private bool _collectDetails = true;

    public bool CollectDetails
    {
        get => _collectDetails;
        set
        {
            _collectDetails = value;
            ApplySamplingMode();
        }
    }

    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            NetworkChange.NetworkAvailabilityChanged += OnChanged;
            NetworkChange.NetworkAddressChanged += OnChanged;
        }

        ApplySamplingMode();
        Publish();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0 || _disposed)
        {
            return;
        }

        NetworkChange.NetworkAvailabilityChanged -= OnChanged;
        NetworkChange.NetworkAddressChanged -= OnChanged;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Volatile.Read(ref _started) != 0)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnChanged;
            NetworkChange.NetworkAddressChanged -= OnChanged;
        }

        _timer.Dispose();
    }

    private void OnChanged(object? sender, EventArgs e) => Publish();

    private void ApplySamplingMode()
    {
        if (Volatile.Read(ref _started) == 0 || _disposed)
        {
            return;
        }

        if (CollectDetails)
        {
            _timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            return;
        }

        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Publish()
    {
        if (Volatile.Read(ref _started) == 0 || _disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _busy, 1) != 0)
        {
            return;
        }

        try
        {
            var snapshot = Sample();
            lock (_gate)
            {
                Current = snapshot;
            }

            Updated?.Invoke(this, snapshot);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private NetworkSnapshot Sample()
    {
        if (!CollectDetails)
        {
            return new NetworkSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                IsConnected = NetworkInterface.GetIsNetworkAvailable()
            };
        }

        try
        {
            var nowTicks = Environment.TickCount64;
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsCandidate)
                .Select(nic =>
                {
                    try
                    {
                        var stats = nic.GetIPStatistics();
                        return new AdapterSample(nic.Name, nic.Description, stats.BytesReceived, stats.BytesSent);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                })
                .Where(sample => sample is not null)
                .Select(sample => sample!)
                .ToArray();

            var connected = NetworkInterface.GetIsNetworkAvailable() || candidates.Length > 0;
            AdapterSample? best = null;
            double recvRate = 0;
            double sendRate = 0;

            var next = new Dictionary<string, (long Recv, long Sent, long Ticks)>(StringComparer.Ordinal);
            foreach (var sample in candidates)
            {
                next[sample.Name] = (sample.Recv, sample.Sent, nowTicks);
                if (_last.TryGetValue(sample.Name, out var previous))
                {
                    var dt = Math.Max(0.2, (nowTicks - previous.Ticks) / 1000.0);
                    var recv = Math.Max(0, (sample.Recv - previous.Recv) / dt);
                    var send = Math.Max(0, (sample.Sent - previous.Sent) / dt);
                    if (best is null || recv + send > recvRate + sendRate)
                    {
                        best = sample;
                        recvRate = recv;
                        sendRate = send;
                    }
                }
                else if (best is null)
                {
                    best = sample;
                }
            }

            _last = next;
            best ??= candidates.FirstOrDefault();
            _recvHistory = NetworkHistory.Push(_recvHistory, recvRate);
            _sendHistory = NetworkHistory.Push(_sendHistory, sendRate);

            return new NetworkSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                IsConnected = connected,
                AdapterName = best?.Name,
                ReceiveBytesPerSecond = recvRate,
                SendBytesPerSecond = sendRate,
                ReceiveHistory = _recvHistory,
                SendHistory = _sendHistory
            };
        }
        catch (Exception)
        {
            return new NetworkSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                IsConnected = NetworkInterface.GetIsNetworkAvailable(),
                ReceiveHistory = _recvHistory,
                SendHistory = _sendHistory
            };
        }
    }

    private static bool IsCandidate(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return false;
        }

        var label = nic.Name + " " + nic.Description;
        return !label.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
               && !label.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
               && !label.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AdapterSample(string Name, string Description, long Recv, long Sent);
}

public sealed class GsmtcMediaProvider : IMediaProvider, IDisposable
{
    private readonly System.Threading.Timer _poll;
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private readonly string _thumbDirectory;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool? _transportPlaying;
    private bool _disposed;
    private int _started;

    public GsmtcMediaProvider(string? thumbDirectory = null)
    {
        _poll = new System.Threading.Timer(_ => _ = TickAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _thumbDirectory = thumbDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDock", "media-thumbs");
    }

    public MediaSessionSnapshot? Current { get; private set; }

    public bool CaptureMetadata { get; set; } = true;

    public event EventHandler? Changed;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0 || _disposed)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ => _ = InitializeAsync());
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _poll.Change(Timeout.Infinite, Timeout.Infinite);
        ClearGsmSession();
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default) =>
        SendAsync(MediaTransportCommand.PlayPause, TogglePlayPauseAsync, cancellationToken);

    public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) =>
        SendAsync(MediaTransportCommand.Next, async session => await session.TrySkipNextAsync(), cancellationToken);

    public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) =>
        SendAsync(MediaTransportCommand.Previous, async session => await session.TrySkipPreviousAsync(), cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _poll.Dispose();
        ClearGsmSession();
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().ConfigureAwait(false);
            if (_disposed)
            {
                return;
            }

            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionsChanged;
        }
        catch (Exception)
        {
            _manager = null;
        }

        if (_disposed)
        {
            return;
        }

        await TickAsync().ConfigureAwait(false);
        if (Volatile.Read(ref _started) != 0 && !_disposed)
        {
            _poll.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        QueueTick();

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) =>
        QueueTick();

    private async Task TickAsync()
    {
        if (_disposed || Volatile.Read(ref _started) == 0)
        {
            return;
        }

        if (!await _tickLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var session = PickPlayingSession();
            if (session is not null)
            {
                if (!SameSession(_session, session))
                {
                    Bind(session);
                    return;
                }

                await UpdateTimelineAsync(session).ConfigureAwait(false);
                return;
            }

            ClearGsmSession();
            Publish(MediaTransportFallback.TrySnapshot(_transportPlaying));
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private GlobalSystemMediaTransportControlsSession? PickPlayingSession()
    {
        if (_manager is null)
        {
            return null;
        }

        IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions;
        try
        {
            sessions = _manager.GetSessions();
        }
        catch (Exception)
        {
            sessions = [];
        }

        foreach (var session in sessions)
        {
            try
            {
                if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    return session;
                }
            }
            catch (Exception)
            {
                // Skip a session that cannot report playback.
            }
        }

        if (MediaTransportFallback.TrySnapshot() is not null)
        {
            return null;
        }

        try
        {
            return _manager.GetCurrentSession() ?? sessions.FirstOrDefault();
        }
        catch (Exception)
        {
            return sessions.FirstOrDefault();
        }
    }

    private void Bind(GlobalSystemMediaTransportControlsSession session)
    {
        ClearGsmSession();
        _transportPlaying = null;
        _session = session;
        _session.MediaPropertiesChanged += OnSessionChanged;
        _session.PlaybackInfoChanged += OnSessionChanged;
        _session.TimelinePropertiesChanged += OnTimelineChanged;
        _ = RefreshAsync();
    }

    private void ClearGsmSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.MediaPropertiesChanged -= OnSessionChanged;
        _session.PlaybackInfoChanged -= OnSessionChanged;
        _session.TimelinePropertiesChanged -= OnTimelineChanged;
        _session = null;
    }

    private void Publish(MediaSessionSnapshot? snapshot)
    {
        if (SameSnapshot(Current, snapshot))
        {
            return;
        }

        Current = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        ThreadPool.QueueUserWorkItem(_ => _ = RefreshAsync());

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        QueueTick();

    private void QueueTick() => ThreadPool.QueueUserWorkItem(_ => _ = TickAsync());

    private async Task UpdateTimelineAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var current = Current;
        if (current is null)
        {
            await RefreshAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            var isPlaying = _transportPlaying
                            ?? (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            var position = current.Position;
            var duration = current.Duration;
            try
            {
                var timeline = session.GetTimelineProperties();
                position = timeline.Position;
                duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : timeline.MaxSeekTime;
            }
            catch (Exception)
            {
            }

            Publish(new MediaSessionSnapshot
            {
                SourceAppId = current.SourceAppId,
                IsPlaying = isPlaying,
                CanPlayPause = current.CanPlayPause,
                CanSkipNext = current.CanSkipNext,
                CanSkipPrevious = current.CanSkipPrevious,
                Title = current.Title,
                Artist = current.Artist,
                ThumbnailPath = current.ThumbnailPath,
                UsesFallbackTransport = current.UsesFallbackTransport,
                Position = position,
                Duration = duration
            });
        }
        catch (Exception)
        {
        }
    }

    private async Task RefreshAsync()
    {
        var session = _session;
        if (session is null || _disposed)
        {
            return;
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            var aumid = session.SourceAppUserModelId ?? string.Empty;
            string? title = null;
            string? artist = null;
            string? thumbnailPath = null;
            if (CaptureMetadata)
            {
                var properties = await session.TryGetMediaPropertiesAsync().AsTask().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(aumid))
                {
                    var processName = MediaTransportFallback.TryRunningPotPlayerProcessName();
                    if (processName is not null)
                    {
                        aumid = KnownLocalPlayers.SourceId(processName);
                    }
                }

                title = FirstNonEmpty(properties?.Title, properties?.Subtitle, properties?.AlbumTitle)
                        ?? MediaTransportFallback.TryReadTitle(aumid)
                        ?? MediaTransportFallback.TryReadTitle(null);
                artist = FirstNonEmpty(properties?.Artist, properties?.AlbumArtist)
                         ?? KnownLocalPlayers.DisplayNameForSource(aumid)
                         ?? FriendlySource(aumid);
                thumbnailPath = await TrySaveThumbnailAsync(properties?.Thumbnail).ConfigureAwait(false);
            }

            var fallbackTransport = KnownLocalPlayers.PrefersOwnTransport(aumid) || !HasGsmTransport(playback);
            if (fallbackTransport is false)
            {
                _transportPlaying = null;
            }

            var isPlaying = _transportPlaying
                            ?? (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

            var position = TimeSpan.Zero;
            var duration = TimeSpan.Zero;
            try
            {
                var timeline = session.GetTimelineProperties();
                position = timeline.Position;
                duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : timeline.MaxSeekTime;
            }
            catch (Exception)
            {
                // Timeline is optional; keep zeros and still publish metadata.
            }

            Publish(new MediaSessionSnapshot
            {
                SourceAppId = aumid,
                IsPlaying = isPlaying,
                CanPlayPause = true,
                CanSkipNext = true,
                CanSkipPrevious = true,
                Title = title,
                Artist = artist,
                ThumbnailPath = thumbnailPath,
                UsesFallbackTransport = fallbackTransport,
                Position = position,
                Duration = duration
            });
        }
        catch (Exception)
        {
            // Keep last snapshot.
        }
    }

    private async Task<bool> SendAsync(
        MediaTransportCommand fallback,
        Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command,
        CancellationToken cancellationToken)
    {
        var session = _session ?? PickPlayingSession();
        if (session is null && Current is null)
        {
            return false;
        }

        if (session is not null && CanSendGsm(session, fallback))
        {
            try
            {
                var ok = await command(session).WaitAsync(cancellationToken).ConfigureAwait(false);
                await RefreshAsync().ConfigureAwait(false);
                if (ok)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Fall through to local player / media keys.
            }
        }

        var playing = Current?.IsPlaying == true;
        var sent = MediaTransportFallback.TrySend(fallback, Current?.SourceAppId, playing);
        if (sent && fallback == MediaTransportCommand.PlayPause && Current is not null)
        {
            _transportPlaying = !playing;
            Publish(Current.WithIsPlaying(_transportPlaying.Value));
        }

        return sent;
    }

    private bool CanSendGsm(GlobalSystemMediaTransportControlsSession session, MediaTransportCommand command)
    {
        if (Current?.UsesFallbackTransport == true)
        {
            return false;
        }
        string? aumid = null;
        try
        {
            aumid = session.SourceAppUserModelId;
        }
        catch (Exception)
        {
            // Treat as unknown source.
        }

        if (KnownLocalPlayers.PrefersOwnTransport(aumid))
        {
            return false;
        }

        try
        {
            var controls = session.GetPlaybackInfo().Controls;
            return command switch
            {
                MediaTransportCommand.Next => controls.IsNextEnabled,
                MediaTransportCommand.Previous => controls.IsPreviousEnabled,
                _ => controls.IsPlayEnabled || controls.IsPauseEnabled || controls.IsPlayPauseToggleEnabled
            };
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool HasGsmTransport(GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
    {
        try
        {
            var controls = playback.Controls;
            return controls.IsPlayEnabled
                   || controls.IsPauseEnabled
                   || controls.IsPlayPauseToggleEnabled
                   || controls.IsNextEnabled
                   || controls.IsPreviousEnabled;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<bool> TogglePlayPauseAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var playing = false;
        try
        {
            playing = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch (Exception)
        {
            // Fall through to toggle.
        }

        if (playing)
        {
            return await session.TryPauseAsync() || await session.TryTogglePlayPauseAsync();
        }

        return await session.TryPlayAsync() || await session.TryTogglePlayPauseAsync();
    }

    private static bool SameSession(
        GlobalSystemMediaTransportControlsSession? left,
        GlobalSystemMediaTransportControlsSession? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.SourceAppUserModelId, right.SourceAppUserModelId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> TrySaveThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_thumbDirectory);
            var path = Path.Combine(_thumbDirectory, "nowplaying.jpg");
            using var content = await thumbnail.OpenReadAsync().AsTask().ConfigureAwait(false);
            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            var reader = new Windows.Storage.Streams.DataReader(content);
            var remaining = content.Size;
            while (remaining > 0)
            {
                var request = (uint)Math.Min(16 * 1024, (long)remaining);
                var loaded = await reader.LoadAsync(request).AsTask().ConfigureAwait(false);
                if (loaded == 0)
                {
                    break;
                }

                var chunk = new byte[loaded];
                reader.ReadBytes(chunk);
                await output.WriteAsync(chunk.AsMemory()).ConfigureAwait(false);
                remaining -= loaded;
            }

            return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool SameSnapshot(MediaSessionSnapshot? left, MediaSessionSnapshot? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.SourceAppId, right.SourceAppId, StringComparison.Ordinal)
               && left.IsPlaying == right.IsPlaying
               && left.UsesFallbackTransport == right.UsesFallbackTransport
               && string.Equals(left.Title, right.Title, StringComparison.Ordinal)
               && string.Equals(left.Artist, right.Artist, StringComparison.Ordinal)
               && string.Equals(left.ThumbnailPath, right.ThumbnailPath, StringComparison.Ordinal)
               && left.Position == right.Position
               && left.Duration == right.Duration;
    }

    private static string? FriendlySource(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
        {
            return null;
        }

        if (aumid.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase)
            || aumid.Contains("CloudMusic", StringComparison.OrdinalIgnoreCase)
            || aumid.Contains("163", StringComparison.Ordinal))
        {
            return "网易云音乐";
        }

        if (KnownLocalPlayers.IsPotPlayerSource(aumid))
        {
            return "PotPlayer";
        }

        var bang = aumid.IndexOf('!');
        return bang > 0 ? aumid[..bang] : aumid;
    }
}
