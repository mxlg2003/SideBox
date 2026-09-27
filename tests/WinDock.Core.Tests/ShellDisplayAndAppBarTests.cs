using WinDock.Core;
using WinDock.Shell;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class ShellDisplayAndAppBarTests
{
    [Fact]
    public void NativeMonitors_enumerate_returns_at_least_one_display()
    {
        var monitors = NativeMonitors.Enumerate();
        Assert.NotEmpty(monitors);

        var primary = monitors.FirstOrDefault(m => m.IsPrimary);
        Assert.NotNull(primary);
        Assert.True(primary.PixelWidth > 0);
        Assert.True(primary.PixelHeight > 0);
        Assert.True(primary.Dpi >= 96);
    }

    [Fact]
    public void Win32DisplayService_capture_produces_valid_topology()
    {
        var topology = Win32DisplayService.Capture();
        Assert.NotNull(topology);
        Assert.NotEmpty(topology.Monitors);

        var primary = topology.Monitors.FirstOrDefault(m => m.IsPrimary);
        Assert.NotNull(primary);
        Assert.True(primary.Bounds.Width > 0);
        Assert.True(primary.Bounds.Height > 0);
        Assert.True(primary.DpiScale >= 1.0);
    }

    [Fact]
    public void AppBarService_zero_handle_fails_gracefully()
    {
        var appBar = new Win32AppBarService();
        Assert.False(appBar.IsRegistered);

        var result = appBar.TryRegister(new AppBarRequest
        {
            WindowHandle = nint.Zero,
            Edge = DockEdge.Bottom,
            Proposed = new RectLogical(0, 0, 800, 60)
        });

        Assert.False(result.Succeeded);
        Assert.False(appBar.IsRegistered);
        Assert.NotNull(result.FailureReason);

        // Safe remove should never throw
        appBar.Remove();
        Assert.False(appBar.IsRegistered);
    }

    [Fact]
    public void StubAppBarService_always_returns_failure()
    {
        var stub = new StubAppBarService();
        Assert.False(stub.IsRegistered);

        var result = stub.TryRegister(new AppBarRequest
        {
            WindowHandle = new nint(12345),
            Edge = DockEdge.Top,
            Proposed = new RectLogical(0, 0, 1920, 50)
        });

        Assert.False(result.Succeeded);
        Assert.False(stub.IsRegistered);
        Assert.Contains("falling back to overlay", result.FailureReason);

        stub.Remove();
        Assert.False(stub.IsRegistered);
    }

    [Fact]
    public void CoreDockLayoutService_resolves_primary_monitor()
    {
        var layout = new CoreDockLayoutService();
        var topology = Win32DisplayService.Capture();
        var monitor = layout.ResolveMonitor(topology, "primary");

        Assert.NotNull(monitor);
        Assert.True(monitor.IsPrimary);

        var edgeBounds = layout.GetEdgeBounds(monitor, DockEdge.Bottom, 60);
        Assert.True(edgeBounds.Width > 0);
        Assert.Equal(60, edgeBounds.Height);

        var islandBounds = layout.GetIslandBounds(monitor, DockEdge.Bottom, 60, 400, 50);
        Assert.True(islandBounds.Width >= DockLayoutConstants.MinIslandLength);
        Assert.Equal(60, islandBounds.Height);
    }
}
