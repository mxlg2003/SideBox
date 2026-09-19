using System.Windows.Threading;

namespace WinDock.Widgets;

internal static class UiPump
{
    public static EventHandler Coalesce(Dispatcher dispatcher, Action render)
    {
        var pending = 0;
        return (_, _) =>
        {
            if (Interlocked.Exchange(ref pending, 1) != 0)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref pending, 0);
                render();
            }, DispatcherPriority.Background);
        };
    }

    public static EventHandler<T> Coalesce<T>(Dispatcher dispatcher, Action<T> render)
    {
        T? latest = default;
        var pending = 0;
        return (_, value) =>
        {
            latest = value;
            if (Interlocked.Exchange(ref pending, 1) != 0)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref pending, 0);
                render(latest!);
            }, DispatcherPriority.Background);
        };
    }
}
