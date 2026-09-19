using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WinDock.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace WinDock.Shell;

internal static class DockMenu
{
    public static ContextMenu Create(bool isLight, DockBackdrop backdrop, bool backdropOk)
    {
        var fill = DockTokens.ResolveMenuFill(isLight, backdropOk, backdrop);
        var stroke = isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke;
        var text = isLight ? DockTokens.Light.Text : DockTokens.Dark.Text;
        var hover = DockTokens.ResolveTileFill(isLight, backdrop);
        var menu = new ContextMenu
        {
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 13,
            HasDropShadow = true,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinWidth = 176,
            OverridesDefaultStyle = true,
            Template = Parse($"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ContextMenu">
                  <Border Background="{Hex(fill)}" BorderBrush="{Hex(stroke)}" BorderThickness="1"
                          CornerRadius="{DockTokens.IslandCornerRadius}" Padding="6"
                          SnapsToDevicePixels="True" UseLayoutRounding="True">
                    <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle"/>
                  </Border>
                </ControlTemplate>
                """)
        };
        menu.Resources[typeof(MenuItem)] = ItemStyle(text, hover);
        menu.Opened += (_, _) =>
        {
            if (VisualTreeHelper.GetParent(menu) is Popup popup)
            {
                popup.AllowsTransparency = true;
            }
        };
        return menu;
    }

    public static MenuItem Item(string header, bool checkable = false, bool isChecked = false) => new()
    {
        Header = header,
        IsCheckable = checkable,
        IsChecked = isChecked,
        StaysOpenOnClick = false,
        Cursor = Cursors.Hand
    };

    public static MenuItem DangerItem(string header)
    {
        var item = Item(header);
        item.Foreground = TokenBrushes.From(DockTokens.Danger);
        return item;
    }

    public static Separator Divider(bool isLight)
    {
        var stroke = Hex(isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke);
        return new Separator
        {
            Margin = new Thickness(8, 5, 8, 5),
            Template = Parse($"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Separator">
                  <Border Height="1" Background="{stroke}" Opacity="0.45" SnapsToDevicePixels="True"/>
                </ControlTemplate>
                """)
        };
    }

    private static Style ItemStyle(uint text, uint hover)
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, TokenBrushes.From(text)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(Control.TemplateProperty, Parse($"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="MenuItem">
              <Border x:Name="Bd" Background="Transparent" CornerRadius="{DockTokens.TileCornerRadius}" Padding="10,7" SnapsToDevicePixels="True">
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="22"/>
                    <ColumnDefinition Width="*"/>
                  </Grid.ColumnDefinitions>
                  <TextBlock x:Name="Check" Grid.Column="0" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets"
                             FontSize="11" VerticalAlignment="Center" HorizontalAlignment="Center" Visibility="Collapsed"/>
                  <ContentPresenter Grid.Column="1" ContentSource="Header" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                </Grid>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsHighlighted" Value="True">
                  <Setter TargetName="Bd" Property="Background" Value="{Hex(hover)}"/>
                </Trigger>
                <Trigger Property="IsChecked" Value="True">
                  <Setter TargetName="Check" Property="Visibility" Value="Visible"/>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                  <Setter Property="Opacity" Value="0.4"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        return style;
    }

    private static ControlTemplate Parse(string xaml) => (ControlTemplate)XamlReader.Parse(xaml);

    private static string Hex(uint argb) => $"#{argb:X8}";
}
