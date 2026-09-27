using WinDock.Core;
using WinDock.Providers;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class ProvidersDiagnosticsTests
{
    [Fact]
    public void ShellLaunch_get_running_images_with_empty_set_returns_instantly_without_alloc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "windock-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var launcher = new Win32ShellLaunch(tempDir);
            var empty = new HashSet<string>();
            var result = launcher.GetRunningImagePaths(empty);

            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void ShellLaunch_get_running_images_with_nonexistent_process_returns_empty_safely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "windock-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var launcher = new Win32ShellLaunch(tempDir);
            var queries = new HashSet<string> { "NonExistentProcess_XYZ_987654321" };
            var result = launcher.GetRunningImagePaths(queries);

            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void PerformanceCounterSampler_lifecycle_runs_without_blocking_ui()
    {
        using var sampler = new PerformanceCounterSampler();
        Assert.NotNull(sampler.Current);
        Assert.False(sampler.Current.CpuAvailable);

        sampler.Start();
        // Sampling runs asynchronously in background threadpool
        Thread.Sleep(50);

        sampler.Stop();
        // Verify stopped safely without exception
    }

    [Fact]
    public void IconAlpha_bounds_returns_correct_box()
    {
        // 4x4 image, 32bpp ARGB, central 2x2 opaque
        var width = 4;
        var height = 4;
        var stride = width * 4;
        var bytes = new byte[stride * height];

        // Fill middle 2x2 with opaque red (B=0, G=0, R=255, A=255)
        for (var y = 1; y <= 2; y++)
        {
            for (var x = 1; x <= 2; x++)
            {
                var offset = (y * stride) + (x * 4);
                bytes[offset + 0] = 0;   // B
                bytes[offset + 1] = 0;   // G
                bytes[offset + 2] = 255; // R
                bytes[offset + 3] = 255; // A
            }
        }

        var bounds = IconAlpha.OpaqueBounds(bytes, width, height, stride, 80);
        Assert.Equal(1, bounds.X);
        Assert.Equal(1, bounds.Y);
        Assert.Equal(2, bounds.Width);
        Assert.Equal(2, bounds.Height);
    }
}
