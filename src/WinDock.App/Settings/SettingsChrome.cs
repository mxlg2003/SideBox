using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinDock.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace WinDock.App;

internal static class SettingsChrome
{
    private static readonly Uri ThemeUri = new("pack://application:,,,/Settings/SettingsTheme.xaml", UriKind.Absolute);

    public static SolidColorBrush Brush(uint argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)argb));
        brush.Freeze();
        return brush;
    }

    public static void EnsureTheme(FrameworkElement root)
    {
        foreach (var dict in root.Resources.MergedDictionaries)
        {
            if (dict.Source is not null
                && dict.Source.OriginalString.EndsWith("SettingsTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        root.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeUri });
    }

    public static void Publish(ResourceDictionary resources, bool isLight)
    {
        Write(resources, isLight);
        foreach (var merged in resources.MergedDictionaries)
        {
            Write(merged, isLight);
        }
    }

    public static void ApplyWindow(Window window, bool isLight)
    {
        EnsureTheme(window);
        Publish(window.Resources, isLight);
        window.Background = Brush(DockTokens.Settings.Page(isLight));
        window.Foreground = Brush(DockTokens.Settings.Text(isLight));
    }

    public static void ApplySearch(Border host, TextBlock? placeholder, TextBox? box, bool isLight)
    {
        host.Background = Brush(DockTokens.Settings.BgSecondary(isLight));
        if (placeholder is not null)
        {
            placeholder.Foreground = Brush(DockTokens.Settings.Muted(isLight));
        }

        if (box is not null)
        {
            box.Foreground = Brush(DockTokens.Settings.Text(isLight));
            box.CaretBrush = Brush(DockTokens.Settings.Text(isLight));
            box.Background = Brushes.Transparent;
            box.BorderThickness = new Thickness(0);
        }
    }

    public static Style? Style(FrameworkElement root, string key) =>
        root.TryFindResource(key) as Style;

    private static void Write(ResourceDictionary resources, bool isLight)
    {
        Set(resources, "Settings.Page", DockTokens.Settings.Page(isLight));
        Set(resources, "Settings.BgPrimary", DockTokens.Settings.BgPrimary(isLight));
        Set(resources, "Settings.Card", DockTokens.Settings.Card(isLight));
        Set(resources, "Settings.Stroke", DockTokens.Settings.Stroke(isLight));
        Set(resources, "Settings.BorderDefault", DockTokens.Settings.BorderDefault(isLight));
        Set(resources, "Settings.BorderStrong", DockTokens.Settings.BorderStrong(isLight));
        Set(resources, "Settings.Text", DockTokens.Settings.Text(isLight));
        Set(resources, "Settings.Muted", DockTokens.Settings.Muted(isLight));
        Set(resources, "Settings.FgDisabled", DockTokens.Settings.FgDisabled(isLight));
        Set(resources, "Settings.NavPane", DockTokens.Settings.NavPane(isLight));
        Set(resources, "Settings.NavSelected", DockTokens.Settings.NavSelected(isLight));
        Set(resources, "Settings.Catalog", DockTokens.Settings.Catalog(isLight));
        Set(resources, "Settings.Preview", DockTokens.Settings.Preview(isLight));
        Set(resources, "Settings.Search", DockTokens.Settings.Search(isLight));
        Set(resources, "Settings.BgSecondary", DockTokens.Settings.BgSecondary(isLight));
        Set(resources, "Settings.Accent", DockTokens.Settings.Accent);
        Set(resources, "Settings.AccentHover", DockTokens.Settings.AccentHover);
        Set(resources, "Settings.OnAccent", DockTokens.Settings.OnAccent);
        Set(resources, "Settings.FocusRing", DockTokens.Settings.FocusRing);
        Set(resources, "Settings.Danger", DockTokens.Danger);
        Set(resources, "Settings.DangerSubtle", DockTokens.Settings.DangerSubtle(isLight));
    }

    private static void Set(ResourceDictionary resources, string key, uint argb) =>
        resources[key] = Brush(argb);
}
