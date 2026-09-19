using WinDock.Core;
using WinDock.Core.Permissions;
using Xunit;

namespace WinDock.Core.Tests;

public class GatedMediaTests
{
    [Fact]
    public void CaptureMetadata_is_forced_off_when_gate_denies()
    {
        var inner = new StubMedia { CaptureMetadata = true };
        var gate = new PermissionGate(new PrivacySettings { MediaRead = false });
        var gated = new GatedMediaProvider(gate, inner);
        gated.CaptureMetadata = true;
        Assert.False(gated.CaptureMetadata);
        Assert.False(inner.CaptureMetadata);
    }

    [Fact]
    public void CaptureMetadata_requires_profile_permission()
    {
        var inner = new StubMedia { CaptureMetadata = false };
        var gate = new PermissionGate(new PrivacySettings { MediaRead = true });
        var gated = new GatedMediaProvider(gate, inner);
        gated.CaptureMetadata = true;
        Assert.False(gated.CaptureMetadata);

        gate.SetActiveProfile([PermissionIds.MediaRead]);
        gated.CaptureMetadata = true;
        Assert.True(gated.CaptureMetadata);
        Assert.True(inner.CaptureMetadata);
    }

    private sealed class StubMedia : IMediaProvider
    {
        public MediaSessionSnapshot? Current { get; set; }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool CaptureMetadata { get; set; }

        public Task<bool> TryPlayPauseAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
