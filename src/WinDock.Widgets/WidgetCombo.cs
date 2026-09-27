using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

internal sealed record ComboChoice(string Id, string Title)
{
    public override string ToString() => Title;
}

public static partial class WidgetChrome
{
    private static readonly DependencyProperty ComboPaintKeyProperty =
        DependencyProperty.RegisterAttached(
            "ComboPaintKey",
            typeof(string),
            typeof(WidgetChrome));

    private static readonly DependencyProperty ComboHookedProperty =
        DependencyProperty.RegisterAttached(
            "ComboHooked",
            typeof(bool),
            typeof(WidgetChrome));

    private static readonly Dictionary<string, ControlTemplate> ComboTemplates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Style> ComboItemStyles = new(StringComparer.Ordinal);

    public static void PaintCombo(ComboBox box, bool isLight) =>
        PaintCombo(box, isLight, _themeBackdrop);

    public static void PaintCombo(ComboBox box, bool isLight, DockBackdrop backdrop)
    {
        var text = isLight ? DockTokens.Light.Text : DockTokens.Dark.Text;
        var stroke = isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke;
        box.FontFamily = WidgetFonts.Ui;
        box.FontSize = 13;
        box.Height = DockTokens.Settings.ControlHeight;
        box.MinHeight = DockTokens.Settings.ControlHeight;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Center;
        box.Padding = new Thickness(DockTokens.Settings.ControlPad, 0, 8, 0);
        box.BorderThickness = new Thickness(1);
        box.SnapsToDevicePixels = true;
        box.OverridesDefaultStyle = true;
        box.FocusVisualStyle = null;
        box.Foreground = TokenBrushes.From(text);
        box.Background = TokenBrushes.From(DockTokens.ResolveFieldFill(isLight, backdrop));
        box.BorderBrush = TokenBrushes.From(stroke);
        TextOptions.SetTextRenderingMode(box, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(box, TextFormattingMode.Display);

        var key = $"{isLight}|{backdrop}";
        if (!Equals(box.GetValue(ComboPaintKeyProperty), key) || box.Template is null)
        {
            box.SetValue(ComboPaintKeyProperty, key);
            box.ItemContainerStyle = ComboItemStyle(text, DockTokens.ResolveTileFill(isLight, backdrop));
            box.Template = ComboTemplate(DockTokens.ResolveMenuFill(isLight, true, backdrop));
        }

        if (Equals(box.GetValue(ComboHookedProperty), true))
        {
            return;
        }

        box.SetValue(ComboHookedProperty, true);
        box.DropDownOpened += (_, _) => ElevateComboPopup(box);
    }

    internal static void SelectChoice(ComboBox box, string? id)
    {
        foreach (var choice in box.Items.OfType<ComboChoice>())
        {
            if (string.Equals(choice.Id, id ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = choice;
                return;
            }
        }
    }

    private static void ElevateComboPopup(ComboBox box)
    {
        if (box.Template?.FindName("PART_Popup", box) is not Popup popup)
        {
            return;
        }

        popup.AllowsTransparency = true;
        WidgetFlyout.Elevate?.Invoke(popup);
    }

    private static Style ComboItemStyle(uint text, uint hover)
    {
        var key = $"{text:X8}|{hover:X8}";
        if (ComboItemStyles.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, TokenBrushes.From(text)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(new Setter(Control.OverridesDefaultStyleProperty, true));
        style.Setters.Add(new Setter(Control.TemplateProperty, Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="ComboBoxItem">
              <Border x:Name="Bd" Background="Transparent" CornerRadius="__TILE__"
                      Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
                <ContentPresenter VerticalAlignment="Center"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsHighlighted" Value="True">
                  <Setter TargetName="Bd" Property="Background" Value="__HOVER__"/>
                </Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True">
                  <Setter TargetName="Bd" Property="Background" Value="__HOVER__"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """.Replace("__TILE__", Invariant(DockTokens.TileCornerRadius))
            .Replace("__HOVER__", Hex(hover)))));
        ComboItemStyles[key] = style;
        return style;
    }

    private static ControlTemplate ComboTemplate(uint menuFill)
    {
        var key = menuFill.ToString("X8", CultureInfo.InvariantCulture);
        if (ComboTemplates.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var template = Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         TargetType="ComboBox">
          <Grid SnapsToDevicePixels="True">
            <Border x:Name="Bd"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="__TILE__">
              <DockPanel>
                <Path DockPanel.Dock="Right"
                      Margin="0,0,12,0"
                      VerticalAlignment="Center"
                      Data="M0,0 L4,4 L8,0"
                      Stroke="{TemplateBinding Foreground}"
                      StrokeThickness="1.5"
                      Opacity="0.55"
                      StrokeStartLineCap="Round"
                      StrokeEndLineCap="Round"
                      StrokeLineJoin="Round"/>
                <ContentPresenter IsHitTestVisible="False"
                                  Margin="12,0,8,0"
                                  VerticalAlignment="Center"
                                  HorizontalAlignment="Left"
                                  Content="{TemplateBinding SelectionBoxItem}"
                                  ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                  ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
              </DockPanel>
            </Border>
            <ToggleButton IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                          Focusable="False"
                          ClickMode="Press">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border Background="Transparent"/>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <Popup x:Name="PART_Popup"
                   AllowsTransparency="True"
                   Focusable="False"
                   IsOpen="{TemplateBinding IsDropDownOpen}"
                   Placement="Bottom"
                   PopupAnimation="Fade">
              <Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
                      Margin="0,4,0,8"
                      Padding="4"
                      Background="__MENU__"
                      BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="1"
                      CornerRadius="__FLYOUT__"
                      SnapsToDevicePixels="True"
                      UseLayoutRounding="True">
                <ScrollViewer CanContentScroll="True" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """.Replace("__TILE__", Invariant(DockTokens.TileCornerRadius))
        .Replace("__FLYOUT__", Invariant(DockTokens.IslandCornerRadius))
        .Replace("__MENU__", Hex(menuFill)));
        ComboTemplates[key] = template;
        return template;
    }

    private static ControlTemplate Parse(string xaml) => (ControlTemplate)XamlReader.Parse(xaml);

    private static string Hex(uint argb) => $"#{argb:X8}";

    private static string Invariant(double value) => value.ToString(CultureInfo.InvariantCulture);
}
