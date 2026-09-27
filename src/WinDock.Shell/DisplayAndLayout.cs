using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.Shell;

public sealed class Win32DisplayService : IDisplayService
{
    private readonly object _gate = new();
    private readonly System.Threading.Timer _debounce;
    private DisplayTopologySnapshot _current;
    private bool _disposed;

    public Win32DisplayService()
    {
        _current = Capture();
        _debounce = new System.Threading.Timer(_ => Publish(), null, Timeout.Infinite, Timeout.Infinite);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public DisplayTopologySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<DisplayTopologySnapshot>? Changed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _debounce.Dispose();
    }

    public void RequestRefresh()
    {
        if (_disposed)
        {
            return;
        }

        _debounce.Change(TimeSpan.FromMilliseconds(400), Timeout.InfiniteTimeSpan);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private void Publish()
    {
        var snapshot = Capture();
        lock (_gate)
        {
            _current = snapshot;
        }

        Changed?.Invoke(this, snapshot);
    }

    public static DisplayTopologySnapshot Capture()
    {
        var entries = NativeMonitors.Enumerate();
        if (entries.Count == 0)
        {
            var work = SystemParameters.WorkArea;
            var monitor = new MonitorInfo
            {
                Id = "primary",
                DevicePath = @"\\.\DISPLAY1",
                IsPrimary = true,
                Bounds = new RectLogical(work.Left, work.Top, work.Width, work.Height),
                WorkArea = new RectLogical(work.Left, work.Top, work.Width, work.Height),
                PixelWorkArea = new RectLogical(work.Left, work.Top, work.Width, work.Height),
                DpiScale = 1
            };
            return new DisplayTopologySnapshot { Monitors = [monitor] };
        }

        var monitors = entries.Select(entry =>
        {
            var dpi = entry.Dpi;
            return new MonitorInfo
            {
                Id = entry.DeviceName,
                DevicePath = DisplayConfig.DevicePathFor(entry.DeviceName) ?? entry.DeviceName,
                IsPrimary = entry.IsPrimary,
                Bounds = DisplayMetrics.PixelRectToDip(entry.PixelLeft, entry.PixelTop, entry.PixelWidth, entry.PixelHeight, dpi),
                WorkArea = DisplayMetrics.PixelRectToDip(entry.WorkLeft, entry.WorkTop, entry.WorkWidth, entry.WorkHeight, dpi),
                PixelWorkArea = new RectLogical(entry.WorkLeft, entry.WorkTop, entry.WorkWidth, entry.WorkHeight),
                DpiScale = DisplayMetrics.ScaleFromDpi(dpi)
            };
        }).ToArray();

        return new DisplayTopologySnapshot { Monitors = monitors };
    }
}

public sealed class CoreDockLayoutService : IDockLayoutService
{
    public RectLogical GetEdgeBounds(MonitorInfo monitor, DockEdge edge, double thicknessLogical) =>
        DockLayoutCalculator.GetEdgeBounds(monitor, edge, thicknessLogical);

    public RectLogical GetIslandBounds(
        MonitorInfo monitor,
        DockEdge edge,
        double thicknessLogical,
        double contentWidth,
        double contentHeight,
        double? preferredLength = null) =>
        DockLayoutCalculator.GetIslandBounds(monitor, edge, thicknessLogical, contentWidth, contentHeight, preferredLength);

    public MonitorInfo ResolveMonitor(DisplayTopologySnapshot topology, string? monitorRef) =>
        DockLayoutCalculator.ResolveMonitor(topology, monitorRef);
}

public sealed class StubAppBarService : IAppBarService
{
    public bool IsRegistered { get; private set; }

    public AppBarRegistrationResult TryRegister(AppBarRequest request)
    {
        IsRegistered = false;
        return AppBarRegistrationResult.Fail("AppBar mode is not implemented in this milestone; falling back to overlay.");
    }

    public void Remove()
    {
        IsRegistered = false;
    }
}

[Obsolete("Use Win32DisplayService instead.")]
public sealed class WinFormsDisplayService : IDisplayService
{
    private readonly Win32DisplayService _inner = new();

    public DisplayTopologySnapshot Current => _inner.Current;

    public event EventHandler<DisplayTopologySnapshot>? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    public void RequestRefresh() => _inner.RequestRefresh();

    public void Dispose() => _inner.Dispose();

    public static DisplayTopologySnapshot Capture() => Win32DisplayService.Capture();
}
