using System.Windows;
using System.Windows.Controls;
using WinDock.Core;

namespace WinDock.App;

internal static class TextPrompt
{
    public static bool TryAsk(Window? owner, string title, string label, string initial, out string value, bool isLight = true)
    {
        value = initial;
        var window = new Window
        {
            Title = title,
            Width = 360,
            Height = 176,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI")
        };
        SettingsChrome.ApplyWindow(window, isLight);
        var box = new TextBox { Text = initial, MinWidth = 280, Margin = new Thickness(0, DockTokens.Settings.FieldGap, 0, 0) };
        if (SettingsChrome.Style(window, "Settings.TextBox") is { } boxStyle)
        {
            box.Style = boxStyle;
        }

        var ok = false;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var cancel = new Button { Content = UiText.Cancel, MinWidth = 80, Margin = new Thickness(0, 0, DockTokens.Settings.FieldGap, 0), IsCancel = true };
        var confirm = new Button { Content = UiText.Confirm, MinWidth = 80, IsDefault = true };
        if (SettingsChrome.Style(window, "Settings.Button") is { } secondary)
        {
            cancel.Style = secondary;
        }

        if (SettingsChrome.Style(window, "Settings.PrimaryButton") is { } primary)
        {
            confirm.Style = primary;
        }

        confirm.Click += (_, _) =>
        {
            ok = true;
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        var caption = new TextBlock { Text = label };
        if (SettingsChrome.Style(window, "Settings.Label") is { } labelStyle)
        {
            caption.Style = labelStyle;
        }

        window.Content = new DockPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                buttons,
                new StackPanel
                {
                    Children =
                    {
                        caption,
                        box
                    }
                }
            }
        };
        box.SelectAll();
        box.Focus();
        window.ShowDialog();
        if (!ok)
        {
            return false;
        }

        value = box.Text.Trim();
        return value.Length > 0;
    }
}
