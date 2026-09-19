using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class PowerWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IPowerWidgetConfigurable, IWidgetThemeAware, IIslandLayoutAware
{
    private const uint Accent = 0xFF0078D4;
    private const uint QuietFill = 0x14000000;
    private const double DockRing = 26;
    private const double PercentRing = 34;
    private const double IconRing = 40;
    private const double FlyoutRing = 32;

    private readonly IPowerProvider _power;
    private readonly EventHandler<PowerSnapshot> _updated;
    private readonly StackPanel _dockDevices = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };
    private readonly TextBlock _empty = WidgetCopy.Secondary();
    private readonly TextBlock _emptyBattery = EmptyBatteryIcon();
    private readonly StackPanel _list = new();
    private readonly StackPanel _layoutOptions = new();
    private readonly StackPanel _deviceChecks = new();
    private readonly List<(string Layout, Border Row, TextBlock Check)> _layoutMarks = [];
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private PowerWidgetSettings _settings = PowerWidgetSettings.Default;
    private IReadOnlyList<PowerDeviceSnapshot> _devices = [];
    private bool _applying;
    private bool _isLight;
    private bool _hidden;
    private bool _hasBattery;

    public event EventHandler? IslandLayoutChanged;

    public PowerWidget(IPowerProvider power)
    {
        _power = power;
        _empty.Text = PowerFormat.NoDevices;
        _empty.FontSize = 13;
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _updated = UiPump.Coalesce<PowerSnapshot>(Dispatcher, Render);

        _tile = WidgetChrome.Wrap(new Grid
        {
            Children = { _dockDevices, _empty, _emptyBattery }
        }, 0);
        _tile.Height = DockTokens.TileHeight;
        _tile.MaxHeight = DockTokens.TileHeight;
        _tile.MinHeight = DockTokens.TileHeight;
        _tile.Padding = new Thickness(8, 4, 8, 4);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        _editorPage = BuildEditor();
        _customizePage = BuildCustomize();
        _customizePage.Visibility = Visibility.Collapsed;
        var body = new Grid();
        body.Children.Add(_editorPage);
        body.Children.Add(_customizePage);
        _panel = WidgetChrome.Flyout(body, 320);
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = _panel
        };
        WidgetFlyout.Attach(_popup);
        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)_panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        Loaded += (_, _) =>
        {
            _power.Updated += _updated;
            Render(_power.Current);
        };
        Unloaded += (_, _) =>
        {
            _power.Updated -= _updated;
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        Render(power.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<PowerWidgetSettings>? SettingsChanged;

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = TokenBrushes.From(isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
    }

    public void ApplySettings(PowerWidgetSettings settings)
    {
        _applying = true;
        _settings = PowerWidgetSettings.Normalize(settings);
        Render(_power.Current);
        if (_customizePage.Visibility == Visibility.Visible)
        {
            PaintCustomize();
        }

        _applying = false;
    }

    public void Open()
    {
        ShowEditor();
        _power.Refresh();
        Render(_power.Current);
        _popup.IsOpen = true;
    }

    public void OpenCustomize()
    {
        ShowCustomize();
        _popup.IsOpen = true;
    }

    private void Persist()
    {
        if (_applying)
        {
            return;
        }

        SettingsChanged?.Invoke(this, _settings);
    }

    private void Render(PowerSnapshot snapshot)
    {
        _hasBattery = snapshot.HasBattery;
        _devices = PowerFormat.WithLevel(snapshot.Devices);
        var visible = PowerWidgetSettings.VisibleOnDock(_devices, _settings);
        _dockDevices.Children.Clear();
        foreach (var device in visible)
        {
            _dockDevices.Children.Add(DockCell(device, _settings.Layout));
        }

        PaintDockEmpty(_devices.Count == 0, visible.Count == 0);
        _dockDevices.Visibility = visible.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var wasHidden = _hidden;
        _hidden = false;
        Visibility = Visibility.Visible;
        if (wasHidden)
        {
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        _list.Children.Clear();
        if (_devices.Count == 0)
        {
            var empty = WidgetCopy.Secondary();
            empty.Text = EmptyCopy();
            empty.Margin = new Thickness(0, 12, 0, 4);
            _list.Children.Add(empty);
        }
        else
        {
            foreach (var device in _devices)
            {
                _list.Children.Add(ListRow(device));
            }
        }
    }

    private UIElement DockCell(PowerDeviceSnapshot device, string layout)
    {
        var mode = PowerLayouts.Normalize(layout);
        var iconOnly = mode == PowerLayouts.IconOnly;
        var percentRing = mode == PowerLayouts.PercentageRing;
        var ringSize = iconOnly ? IconRing : percentRing ? PercentRing : DockRing;
        var percent = PercentLabel(device, 11);
        var icon = DeviceGlyph(device.Kind, iconOnly ? 16 : percentRing ? 9 : 12);
        var ring = ChargeRing(ringSize, device);
        var face = new Grid
        {
            Width = ringSize,
            Height = ringSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { ring }
        };

        if (percentRing)
        {
            percent.HorizontalAlignment = HorizontalAlignment.Center;
            percent.VerticalAlignment = VerticalAlignment.Center;
            percent.Margin = new Thickness(0);
            face.Children.Add(percent);
            var stack = new StackPanel
            {
                Width = PercentRing + 4,
                Margin = new Thickness(3, 0, 3, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { face, ChargingIcon(device, icon) }
            };
            return stack;
        }

        face.Children.Add(ChargingIcon(device, icon));
        if (iconOnly)
        {
            return new Border
            {
                Width = IconRing + 4,
                Margin = new Thickness(2, 0, 2, 0),
                Child = face
            };
        }

        percent.HorizontalAlignment = HorizontalAlignment.Center;
        percent.Margin = new Thickness(0, 2, 0, 0);
        return new StackPanel
        {
            Width = 40,
            Margin = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { face, percent }
        };
    }

    private UIElement ListRow(PowerDeviceSnapshot device)
    {
        var ring = ChargeRing(FlyoutRing, device);
        var icon = DeviceGlyph(device.Kind, 13);
        var face = new Grid
        {
            Width = FlyoutRing,
            Height = FlyoutRing,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { ring, ChargingIcon(device, icon) }
        };
        var name = WidgetCopy.Primary();
        name.Text = device.Name;
        name.FontSize = 14;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var status = WidgetCopy.Secondary();
        status.Text = PowerFormat.Status(device);
        status.Margin = new Thickness(0);
        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { name, status }
        };
        var percent = WidgetCopy.Primary();
        percent.Text = PowerFormat.Percent(device.Percent);
        percent.FontSize = 16;
        percent.VerticalAlignment = VerticalAlignment.Center;
        percent.Margin = new Thickness(12, 0, 0, 0);
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(face);
        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);
        Grid.SetColumn(percent, 2);
        row.Children.Add(percent);
        return row;
    }

    private Grid BuildEditor()
    {
        var title = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Power));
        var customize = HeaderGlyph("\uE8FD", OpenCustomize);
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(title, customize, close);
        var page = new StackPanel
        {
            Children =
            {
                header,
                new ScrollViewer
                {
                    MaxHeight = 360,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _list
                }
            }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Grid BuildCustomize()
    {
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                HeaderGlyph("\uE76B", ShowEditor, 12),
                HeaderLabel("返回")
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        var title = HeaderLabel("自定义");
        title.Margin = new Thickness(8, 0, 0, 0);
        title.FontWeight = FontWeights.SemiBold;
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, title }
        }, close);
        var layoutCaption = HeaderLabel("布局");
        layoutCaption.Opacity = 0.62;
        layoutCaption.Margin = new Thickness(0, 8, 0, 8);
        foreach (var layout in PowerLayouts.All)
        {
            _layoutOptions.Children.Add(LayoutRow(layout));
        }

        var dockCaption = HeaderLabel("在 Dock 上显示");
        dockCaption.Opacity = 0.62;
        dockCaption.Margin = new Thickness(0, 16, 0, 8);
        var checks = new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _deviceChecks
        };
        var showAll = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Background = TokenBrushes.From(QuietFill),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 12, 0, 0),
            Child = Centered("显示全部设备")
        };
        showAll.MouseLeftButtonUp += (_, e) =>
        {
            ShowAllDevices();
            e.Handled = true;
        };

        var page = new StackPanel
        {
            Children =
            {
                header,
                layoutCaption,
                _layoutOptions,
                dockCaption,
                checks,
                showAll
            }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Border LayoutRow(string layout)
    {
        var check = new TextBlock
        {
            Text = "\uE73E",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            Tag = "tint",
            Foreground = TokenBrushes.From(Accent),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var glyph = new TextBlock
        {
            Text = LayoutGlyph(layout),
            FontFamily = layout == PowerLayouts.PercentageRing ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = layout == PowerLayouts.PercentageRing ? 14 : 14,
            Width = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var title = HeaderLabel(PowerFormat.LayoutTitle(layout));
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { glyph, title }
        };
        content.Children.Add(left);
        Grid.SetColumn(check, 1);
        content.Children.Add(check);
        var row = new Border
        {
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = Cursors.Hand,
            Child = content
        };
        row.MouseLeftButtonUp += (_, e) =>
        {
            SetLayout(layout);
            e.Handled = true;
        };
        _layoutMarks.Add((layout, row, check));
        return row;
    }

    private void SetLayout(string layout)
    {
        _settings = _settings with { Layout = PowerLayouts.Normalize(layout) };
        Persist();
        PaintCustomize();
        Render(_power.Current);
    }

    private void ShowAllDevices()
    {
        _settings = _settings with { HiddenDeviceIds = [] };
        Persist();
        PaintCustomize();
        Render(_power.Current);
    }

    private void SetDeviceVisible(string id, bool visible)
    {
        var hidden = _settings.HiddenDeviceIds.ToHashSet(StringComparer.Ordinal);
        if (visible)
        {
            hidden.Remove(id);
        }
        else
        {
            hidden.Add(id);
        }

        _settings = _settings with { HiddenDeviceIds = hidden.ToArray() };
        Persist();
        Render(_power.Current);
    }

    private void PaintCustomize()
    {
        foreach (var (layout, row, check) in _layoutMarks)
        {
            var selected = string.Equals(layout, _settings.Layout, StringComparison.Ordinal);
            row.Background = selected ? TokenBrushes.From(QuietFill) : Brushes.Transparent;
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        _deviceChecks.Children.Clear();
        if (_devices.Count == 0)
        {
            var empty = WidgetCopy.Secondary();
            empty.Text = EmptyCopy();
            _deviceChecks.Children.Add(empty);
            return;
        }

        foreach (var device in _devices)
        {
            var box = new CheckBox
            {
                Content = device.Name,
                IsChecked = _settings.IsVisibleOnDock(device.Id),
                Margin = new Thickness(0, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            var id = device.Id;
            box.Checked += (_, _) =>
            {
                if (!_applying)
                {
                    SetDeviceVisible(id, true);
                }
            };
            box.Unchecked += (_, _) =>
            {
                if (!_applying)
                {
                    SetDeviceVisible(id, false);
                }
            };
            _deviceChecks.Children.Add(box);
        }
    }

    private void ShowEditor()
    {
        _popup.StaysOpen = false;
        _editorPage.Visibility = Visibility.Visible;
        _customizePage.Visibility = Visibility.Collapsed;
    }

    private void ShowCustomize()
    {
        _popup.StaysOpen = true;
        PaintCustomize();
        _editorPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Visible;
    }

    private void ClosePopup()
    {
        _popup.StaysOpen = false;
        _popup.IsOpen = false;
    }

    private ProgressRing ChargeRing(double size, PowerDeviceSnapshot device)
    {
        return new ProgressRing(size)
        {
            Clockwise = true,
            Progress = (device.Percent ?? 0) / 100.0,
            Fill = TokenBrushes.From(Tint(device.Percent)),
            Track = TokenBrushes.From(_isLight ? 0x2E000000u : 0x55FFFFFFu)
        };
    }

    private void PaintDockEmpty(bool noDevices, bool noneVisible)
    {
        var noBattery = noDevices && !_hasBattery;
        _emptyBattery.Visibility = noBattery ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = !noBattery && (noDevices || noneVisible) ? Visibility.Visible : Visibility.Collapsed;
        if (!noBattery)
        {
            _empty.Text = noDevices ? PowerFormat.NoDevices : "已全部隐藏";
        }

        _tile.ToolTip = noBattery ? PowerFormat.NoBattery : null;
    }

    private string EmptyCopy() => _hasBattery ? PowerFormat.NoDevices : PowerFormat.NoBattery;

    private static uint Tint(int? percent) =>
        percent is <= 15 ? DockTokens.BatteryCritical : DockTokens.BatteryOk;

    private static TextBlock PercentLabel(PowerDeviceSnapshot device, double size)
    {
        var label = WidgetCopy.Primary();
        label.Text = PowerFormat.Percent(device.Percent);
        label.FontSize = size;
        label.FontWeight = FontWeights.SemiBold;
        return label;
    }

    private static UIElement ChargingIcon(PowerDeviceSnapshot device, TextBlock icon)
    {
        if (!device.Charging)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            return icon;
        }

        var bolt = new TextBlock
        {
            Text = "\uEC1B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = Math.Max(7, icon.FontSize - 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -1, -1),
            IsHitTestVisible = false
        };
        return new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, bolt }
        };
    }

    private static TextBlock DeviceGlyph(PowerDeviceKind kind, double size) => new()
    {
        Text = Glyph(kind),
        FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock EmptyBatteryIcon()
    {
        var icon = new TextBlock
        {
            Text = "\uE850",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 28,
            LineHeight = 28,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Opacity = 0.5,
            Width = 32,
            Height = 32,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        AutomationProperties.SetName(icon, PowerFormat.NoBattery);
        return icon;
    }

    private static string Glyph(PowerDeviceKind kind) => kind switch
    {
        PowerDeviceKind.Laptop => "\uE7F8",
        PowerDeviceKind.Headphones => "\uE7F6",
        PowerDeviceKind.Case => "\uE83F",
        PowerDeviceKind.Keyboard => "\uE92E",
        PowerDeviceKind.Mouse => "\uE962",
        PowerDeviceKind.Phone => "\uE8EA",
        _ => "\uE83F"
    };

    private static string LayoutGlyph(string layout) => PowerLayouts.Normalize(layout) switch
    {
        PowerLayouts.PercentageRing => "%",
        PowerLayouts.IconOnly => "\uE7F8",
        _ => "\uE7F4"
    };

    private static TextBlock Centered(string text)
    {
        var label = WidgetCopy.Primary();
        label.Text = text;
        label.FontSize = 13;
        label.FontWeight = FontWeights.Normal;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(left);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var item in right)
        {
            actions.Children.Add(item);
        }

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    private static TextBlock HeaderLabel(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock HeaderGlyph(string glyph, Action click, double size = 13)
    {
        var block = new TextBlock
        {
            Text = glyph,
            FontFamily = glyph == "\u00D7" ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = size,
            Opacity = 0.45,
            Padding = new Thickness(8, 4, 0, 4),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        block.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return block;
    }
}
