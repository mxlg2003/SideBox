using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace WinDock.Shell;

public static class PopupTopmost
{
    private static readonly IntPtr HwndInsertAfterTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    public static void TryElevate(Popup popup)
    {
        try
        {
            if (popup.Child is not Visual child)
            {
                return;
            }

            _ = child.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (PresentationSource.FromVisual(child) is not HwndSource source || source.Handle == IntPtr.Zero)
                    {
                        return;
                    }

                    if (source.CompositionTarget is { } target)
                    {
                        target.BackgroundColor = Colors.Transparent;
                    }

                    SetWindowPos(source.Handle, HwndInsertAfterTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
                }
                catch (Exception)
                {
                }
            }, DispatcherPriority.Loaded);
        }
        catch (Exception)
        {
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
