using WinDock.Core.Permissions;

namespace WinDock.Core;

/// <summary>Strips media metadata unless both global privacy and the active profile allow media.read.</summary>
public sealed class GatedMediaProvider : IMediaProvider
{
    private readonly IPermissionGate _gate;
    private readonly IMediaProvider _inner;

    public GatedMediaProvider(IPermissionGate gate, IMediaProvider inner)
    {
        _gate = gate;
        _inner = inner;
        _inner.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public void Start() => _inner.Start();

    public void Stop() => _inner.Stop();

    public bool CaptureMetadata
    {
        get => _gate.Allows(PermissionIds.MediaRead) && _inner.CaptureMetadata;
        set => _inner.CaptureMetadata = value && _gate.Allows(PermissionIds.MediaRead);
    }

    public MediaSessionSnapshot? Current
    {
        get
        {
            var inner = _inner.Current;
            if (inner is null)
            {
                return null;
            }

            if (_gate.Allows(PermissionIds.MediaRead))
            {
                return inner;
            }

            return new MediaSessionSnapshot
            {
                SourceAppId = inner.SourceAppId,
                IsPlaying = inner.IsPlaying,
                CanPlayPause = inner.CanPlayPause,
                CanSkipNext = inner.CanSkipNext,
                CanSkipPrevious = inner.CanSkipPrevious,
                UsesFallbackTransport = inner.UsesFallbackTransport,
                Position = inner.Position,
                Duration = inner.Duration
            };
        }
    }

    public Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default) =>
        _inner.TryPlayPauseAsync(cancellationToken);

    public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) =>
        _inner.TrySkipNextAsync(cancellationToken);

    public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) =>
        _inner.TrySkipPreviousAsync(cancellationToken);
}

/// <summary>Hides adapter names and rates unless privacy.networkDetails is on.</summary>
public sealed class GatedNetworkProvider : INetworkProvider
{
    private readonly IPermissionGate _gate;
    private readonly INetworkProvider _inner;
    private bool _wantWifi;

    public GatedNetworkProvider(IPermissionGate gate, INetworkProvider inner)
    {
        _gate = gate;
        _inner = inner;
        _inner.Updated += (_, snapshot) => Updated?.Invoke(this, Filter(snapshot));
    }

    public event EventHandler<NetworkSnapshot>? Updated;

    public void Start()
    {
        ApplyGates();
        _inner.Start();
    }

    public void Stop() => _inner.Stop();

    public bool CollectDetails
    {
        get => _inner.CollectDetails;
        set => _inner.CollectDetails = value && Allowed;
    }

    public bool CollectWifi
    {
        get => _inner.CollectWifi;
        set
        {
            _wantWifi = value;
            _inner.CollectWifi = value && Allowed;
        }
    }

    public NetworkSnapshot Current => Filter(_inner.Current);

    private bool Allowed => _gate.Allows("network.details");

    private void ApplyGates()
    {
        _inner.CollectDetails = Allowed;
        _inner.CollectWifi = _wantWifi && Allowed;
    }

    private NetworkSnapshot Filter(NetworkSnapshot snapshot)
    {
        if (Allowed)
        {
            return snapshot;
        }

        return new NetworkSnapshot
        {
            Timestamp = snapshot.Timestamp,
            IsConnected = snapshot.IsConnected
        };
    }
}

/// <summary>Hides process names unless privacy.processNames is on.</summary>
public sealed class GatedProcessListProvider : IProcessListProvider
{
    private readonly IPermissionGate _gate;
    private readonly IProcessListProvider _inner;

    public GatedProcessListProvider(IPermissionGate gate, IProcessListProvider inner)
    {
        _gate = gate;
        _inner = inner;
        _inner.Updated += (_, snapshot) => Updated?.Invoke(this, Filter(snapshot));
    }

    public event EventHandler<ProcessListSnapshot>? Updated;

    public void Start() => _inner.Start();

    public void Stop() => _inner.Stop();

    public void SetLive(bool live) => _inner.SetLive(live);

    public ProcessListSnapshot Current => Filter(_inner.Current);

    private ProcessListSnapshot Filter(ProcessListSnapshot snapshot)
    {
        if (_gate.Allows("process.names"))
        {
            return snapshot with { Allowed = true };
        }

        return new ProcessListSnapshot
        {
            Timestamp = snapshot.Timestamp,
            Allowed = false
        };
    }
}
