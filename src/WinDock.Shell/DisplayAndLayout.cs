using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.Shell;

public sealed class WinFormsDisplayService : IDisplayService
{
    private readonly object _gate = new();
    private readonly System.Threading.Timer _debounce;
    private DisplayTopologySnapshot _current;
    private bool _disposed;

    public WinFormsDisplayService()
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
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0)
        {
            var work = SystemParameters.WorkArea;
            var monitor = new MonitorInfo
            {
                Id = "primary",
                DevicePath = @"\\.\DISPLAY1",
                IsPrimary = true,
                Bounds = new RectLogical(work.Left, work.Top, work.Width, work.Height),
                WorkArea = new RectLogical(work.Left, work.Top, work.Width, work.Height),
                DpiScale = 1
            };
            return new DisplayTopologySnapshot { Monitors = [monitor] };
        }

        var monitors = screens.Select(screen =>
        {
            var dpi = EffectiveDpi(screen);
            return new MonitorInfo
            {
                Id = screen.DeviceName,
                DevicePath = DisplayConfig.DevicePathFor(screen.DeviceName) ?? screen.DeviceName,
                IsPrimary = screen.Primary,
                Bounds = ToDip(screen.Bounds, dpi),
                WorkArea = ToDip(screen.WorkingArea, dpi),
                PixelWorkArea = ToRect(screen.WorkingArea),
                DpiScale = DisplayMetrics.ScaleFromDpi(dpi)
            };
        }).ToArray();

        return new DisplayTopologySnapshot { Monitors = monitors };
    }

    private static RectLogical ToRect(System.Drawing.Rectangle rectangle) =>
        new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private static RectLogical ToDip(System.Drawing.Rectangle rectangle, double dpi) =>
        DisplayMetrics.PixelRectToDip(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, dpi);

    private static double EffectiveDpi(System.Windows.Forms.Screen screen)
    {
        try
        {
            var point = new NativePoint
            {
                X = screen.Bounds.Left + (screen.Bounds.Width / 2),
                Y = screen.Bounds.Top + (screen.Bounds.Height / 2)
            };
            var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero
                && GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX;
            }
        }
        catch (Exception)
        {
        }

        return DisplayMetrics.DefaultDpi;
    }

    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
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
