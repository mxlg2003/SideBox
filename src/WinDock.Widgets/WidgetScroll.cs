using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using WinDock.Core;

namespace WinDock.Widgets;

public static partial class WidgetChrome
{
    private static readonly Dictionary<bool, Style> ScrollBarStyles = new();

    private const string ScrollBarXamlTemplate = """
    <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           TargetType="ScrollBar">
      <Setter Property="Stylus.IsPressAndHoldEnabled" Value="False"/>
      <Setter Property="Stylus.IsFlicksEnabled" Value="False"/>
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Width" Value="__SCROLL_THICKNESS__"/>
      <Setter Property="MinWidth" Value="__SCROLL_THICKNESS__"/>
      <Setter Property="SnapsToDevicePixels" Value="True"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ScrollBar">
            <Grid Background="Transparent" SnapsToDevicePixels="True">
              <Track x:Name="PART_Track" IsDirectionReversed="True" Focusable="False">
                <Track.DecreaseRepeatButton>
                  <RepeatButton Command="ScrollBar.PageUpCommand" Focusable="False" IsTabStop="False" OverridesDefaultStyle="True">
                    <RepeatButton.Template>
                      <ControlTemplate TargetType="RepeatButton">
                        <Border Background="Transparent"/>
                      </ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                  <RepeatButton Command="ScrollBar.PageDownCommand" Focusable="False" IsTabStop="False" OverridesDefaultStyle="True">
                    <RepeatButton.Template>
                      <ControlTemplate TargetType="RepeatButton">
                        <Border Background="Transparent"/>
                      </ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Focusable="False" IsTabStop="False" SnapsToDevicePixels="True" OverridesDefaultStyle="True">
                    <Thumb.Template>
                      <ControlTemplate TargetType="Thumb">
                        <Border Background="Transparent" Padding="0,2">
                          <Border x:Name="Pill"
                                  Width="__THUMB_SIZE__"
                                  HorizontalAlignment="Center"
                                  Background="__THUMB__"
                                  CornerRadius="4"
                                  SnapsToDevicePixels="True"/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Pill" Property="Background" Value="__THUMB_HOVER__"/>
                          </Trigger>
                          <Trigger Property="IsDragging" Value="True">
                            <Setter TargetName="Pill" Property="Background" Value="__THUMB_PRESSED__"/>
                          </Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Thumb.Template>
                  </Thumb>
                </Track.Thumb>
              </Track>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
      <Style.Triggers>
        <Trigger Property="Orientation" Value="Horizontal">
          <Setter Property="Width" Value="Auto"/>
          <Setter Property="MinWidth" Value="0"/>
          <Setter Property="Height" Value="__SCROLL_THICKNESS__"/>
          <Setter Property="MinHeight" Value="__SCROLL_THICKNESS__"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ScrollBar">
                <Grid Background="Transparent" SnapsToDevicePixels="True">
                  <Track x:Name="PART_Track" Focusable="False">
                    <Track.DecreaseRepeatButton>
                      <RepeatButton Command="ScrollBar.PageLeftCommand" Focusable="False" IsTabStop="False" OverridesDefaultStyle="True">
                        <RepeatButton.Template>
                          <ControlTemplate TargetType="RepeatButton">
                            <Border Background="Transparent"/>
                          </ControlTemplate>
                        </RepeatButton.Template>
                      </RepeatButton>
                    </Track.DecreaseRepeatButton>
                    <Track.IncreaseRepeatButton>
                      <RepeatButton Command="ScrollBar.PageRightCommand" Focusable="False" IsTabStop="False" OverridesDefaultStyle="True">
                        <RepeatButton.Template>
                          <ControlTemplate TargetType="RepeatButton">
                            <Border Background="Transparent"/>
                          </ControlTemplate>
                        </RepeatButton.Template>
                      </RepeatButton>
                    </Track.IncreaseRepeatButton>
                    <Track.Thumb>
                      <Thumb Focusable="False" IsTabStop="False" SnapsToDevicePixels="True" OverridesDefaultStyle="True">
                        <Thumb.Template>
                          <ControlTemplate TargetType="Thumb">
                            <Border Background="Transparent" Padding="2,0">
                              <Border x:Name="Pill"
                                      Height="__THUMB_SIZE__"
                                      VerticalAlignment="Center"
                                      Background="__THUMB__"
                                      CornerRadius="4"
                                      SnapsToDevicePixels="True"/>
                            </Border>
                            <ControlTemplate.Triggers>
                              <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Pill" Property="Background" Value="__THUMB_HOVER__"/>
                              </Trigger>
                              <Trigger Property="IsDragging" Value="True">
                                <Setter TargetName="Pill" Property="Background" Value="__THUMB_PRESSED__"/>
                              </Trigger>
                            </ControlTemplate.Triggers>
                          </ControlTemplate>
                        </Thumb.Template>
                      </Thumb>
                    </Track.Thumb>
                  </Track>
                </Grid>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Trigger>
      </Style.Triggers>
    </Style>
    """;

    public static Style CreateScrollBarStyle(bool isLight)
    {
        if (ScrollBarStyles.TryGetValue(isLight, out var cached) && (cached.Dispatcher == null || cached.Dispatcher.CheckAccess()))
        {
            return cached;
        }

        var thickness = DockTokens.Settings.ScrollThickness.ToString(CultureInfo.InvariantCulture);
        var thumbSize = DockTokens.Settings.ScrollThumbSize.ToString(CultureInfo.InvariantCulture);
        var thumb = $"#{DockTokens.Settings.ScrollThumb(isLight):X8}";
        var thumbHover = $"#{DockTokens.Settings.ScrollThumbHover(isLight):X8}";
        var thumbPressed = $"#{DockTokens.Settings.ScrollThumbPressed(isLight):X8}";

        var xaml = ScrollBarXamlTemplate
            .Replace("__SCROLL_THICKNESS__", thickness)
            .Replace("__THUMB_SIZE__", thumbSize)
            .Replace("__THUMB__", thumb)
            .Replace("__THUMB_HOVER__", thumbHover)
            .Replace("__THUMB_PRESSED__", thumbPressed);

        var style = (Style)XamlReader.Parse(xaml);
        ScrollBarStyles[isLight] = style;
        return style;
    }

    public static void PaintScrollViewer(FrameworkElement host, bool isLight)
    {
        host.Resources[typeof(ScrollBar)] = CreateScrollBarStyle(isLight);
    }
}
