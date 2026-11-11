using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinDock.App;

internal static class AppIcon
{
    private static bool _attached;

    public static ImageSource Image { get; } = Load();

    public static void AttachToWindows()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window && window.Icon is null)
        {
            window.Icon = Image;
        }
    }

    private static ImageSource Load()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/WinDock.png", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
