using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WinDock.Core;

namespace WinDock.Widgets;

public static class WidgetFlyout
{
    private static readonly List<WeakReference<Popup>> Attached = [];

    public static DockEdge Edge { get; set; } = DockEdge.Top;

    public static Action<Popup>? Elevate { get; set; }

    public static event EventHandler? OpenStateChanged;

    public static event EventHandler<FlyoutWheelEventArgs>? Wheel;

    private static bool _delivering;

    public static void Attach(Popup popup)
    {
        Track(popup);
        popup.Opened += (_, _) =>
        {
            CloseOthers(popup);
            Apply(popup);
            Elevate?.Invoke(popup);
            OpenStateChanged?.Invoke(null, EventArgs.Empty);
        };
        popup.Closed += (_, _) => OpenStateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool AnyOpen()
    {
        Prune();
        foreach (var weak in Attached)
        {
            if (weak.TryGetTarget(out var popup) && popup.IsOpen)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPointerOverOpen()
    {
        Prune();
        foreach (var weak in Attached)
        {
            if (!weak.TryGetTarget(out var popup) || !popup.IsOpen)
            {
                continue;
            }

            if (popup.IsMouseOver || (popup.Child is UIElement child && child.IsMouseOver))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryDeliverMouseWheel(MouseWheelEventArgs source, Point screen)
    {
        if (_delivering)
        {
            source.Handled = true;
            return true;
        }

        if (!AnyOpen())
        {
            return false;
        }

        _delivering = true;
        try
        {
            Wheel?.Invoke(null, new FlyoutWheelEventArgs(screen, source.Delta));
        }
        catch (Exception)
        {
        }
        finally
        {
            _delivering = false;
        }

        source.Handled = true;
        return true;
    }

    public static void RelayoutOpen()
    {
        Prune();
        foreach (var weak in Attached)
        {
            if (!weak.TryGetTarget(out var popup) || !popup.IsOpen)
            {
                continue;
            }

            Apply(popup);
            Elevate?.Invoke(popup);
        }
    }

    private static void CloseOthers(Popup keep)
    {
        Prune();
        for (var i = Attached.Count - 1; i >= 0; i--)
        {
            if (!Attached[i].TryGetTarget(out var popup) || ReferenceEquals(popup, keep) || !popup.IsOpen)
            {
                continue;
            }

            popup.IsOpen = false;
        }
    }

    private static void Track(Popup popup)
    {
        Prune();
        foreach (var weak in Attached)
        {
            if (weak.TryGetTarget(out var existing) && ReferenceEquals(existing, popup))
            {
                return;
            }
        }

        Attached.Add(new WeakReference<Popup>(popup));
    }

    private static void Prune()
    {
        for (var i = Attached.Count - 1; i >= 0; i--)
        {
            if (!Attached[i].TryGetTarget(out _))
            {
                Attached.RemoveAt(i);
            }
        }
    }

    public static void Apply(Popup popup)
    {
        popup.AllowsTransparency = true;
        popup.HorizontalOffset = 0;
        popup.VerticalOffset = 0;
        switch (Edge)
        {
            case DockEdge.Bottom:
                popup.Placement = PlacementMode.Top;
                popup.VerticalOffset = -DockTokens.FlyoutOffset;
                break;
            case DockEdge.Left:
                popup.Placement = PlacementMode.Right;
                popup.HorizontalOffset = DockTokens.FlyoutOffset;
                break;
            case DockEdge.Right:
                popup.Placement = PlacementMode.Left;
                popup.HorizontalOffset = -DockTokens.FlyoutOffset;
                break;
            default:
                popup.Placement = PlacementMode.Bottom;
                popup.VerticalOffset = DockTokens.FlyoutOffset;
                break;
        }
    }
}

public sealed class FlyoutWheelEventArgs : EventArgs
{
    public FlyoutWheelEventArgs(Point screen, int delta)
    {
        Screen = screen;
        Delta = delta;
    }

    public Point Screen { get; }

    public int Delta { get; }
}
