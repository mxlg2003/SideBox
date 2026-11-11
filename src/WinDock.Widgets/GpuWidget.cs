using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class GpuWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IGpuConfigurable, IWidgetThemeAware, ILocalizable, IDockEdgeAware
{
    private readonly IGpuProvider _gpu;
    private readonly EventHandler<GpuSnapshot> _updated;
    private readonly StackPanel _dockBody = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly PhosphorGlyph _glyph = WidgetChrome.Glyph(PhosphorIcon.GraphicsCard, DockTokens.Gpu);
    private readonly TextBlock _value = WidgetCopy.Primary();
    private readonly NetworkSparkline _dockChart = new();
    private readonly TextBlock _panelPercent = WidgetCopy.Primary();
    private readonly TextBlock _adapterName = WidgetCopy.Secondary();
    private readonly TextBlock _unsupported = WidgetCopy.Secondary();
    private readonly TextBlock _vramLabel = WidgetCopy.Secondary();
    private readonly TextBlock _vramValue = WidgetCopy.Primary();
    private readonly StackPanel _engines = new();
    private readonly Grid _vramRow = new();
    private readonly ComboBox _adapterBox = new();
    private readonly CheckBox _vramBox = new();
    private readonly CheckBox _engineBox = new();
    private readonly CheckBox _chartBox = new();
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _adapterCaption = null!;
    private GpuWidgetSettings _settings = GpuWidgetSettings.Default;
    private bool _applying;
    private bool _isLight = true;
    private bool _side;

    public GpuWidget(IGpuProvider gpu)
    {
        _gpu = gpu;
        _updated = UiPump.Coalesce<GpuSnapshot>(Dispatcher, Render);
        _value.FontSize = 17;
        _panelPercent.FontSize = 38;
        _panelPercent.FontWeight = FontWeights.SemiBold;
        _adapterName.TextWrapping = TextWrapping.Wrap;
        _unsupported.Text = UiText.GpuUnsupported;
        _unsupported.TextWrapping = TextWrapping.Wrap;
        _vramLabel.Text = UiText.GpuVram;
        _dockChart.Width = DockTokens.SparklineDockWidth;
        _dockChart.Height = 28;
        _dockChart.Margin = new Thickness(6, 0, 0, 0);
        _dockChart.ReceiveStroke = TokenBrushes.From(DockTokens.Gpu);
        _dockChart.SendStroke = TokenBrushes.From(DockTokens.Gpu);

        _tile = WidgetChrome.Wrap(_dockBody);
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
        _panel = WidgetChrome.Flyout(body, 360);
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
        _popup.Closed += (_, _) => ShowEditor();
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
            _gpu.Updated += _updated;
            ApplyOptions();
            Render(_gpu.Current);
        };
        Unloaded += (_, _) =>
        {
            _gpu.Updated -= _updated;
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ClipToBounds = false;
        MaxHeight = DockTokens.TileHeight;
        ApplyTheme(true);
        RebuildTile();
        Render(gpu.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<GpuWidgetSettings>? SettingsChanged;

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        RebuildTile();
        WidgetChrome.FitEdge(_tile, _side);
        WidgetChrome.FitHost(this, _side);
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = Ink();
        PaintChrome();
        _panelPercent.Foreground = Ink();
        _adapterName.Foreground = Ink();
        _unsupported.Foreground = Ink();
        _vramLabel.Foreground = Ink();
        _vramValue.Foreground = Ink();
        _dockChart.ReceiveStroke = TokenBrushes.From(DockTokens.Gpu);
        _dockChart.SendStroke = TokenBrushes.From(DockTokens.Gpu);
    }

    public void ApplyLocale()
    {
        _applying = true;
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Gpu);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _adapterCaption.Text = UiText.GpuAdapter;
        _unsupported.Text = UiText.GpuUnsupported;
        _vramLabel.Text = UiText.GpuVram;
        _vramBox.Content = UiText.GpuVram;
        _engineBox.Content = UiText.GpuEngines;
        _chartBox.Content = UiText.DockChart;
        _applying = false;
        Render(_gpu.Current);
    }

    public void ApplySettings(GpuWidgetSettings settings)
    {
        _applying = true;
        _settings = GpuWidgetSettings.Normalize(settings);
        ApplyOptions();
        PaintCustomize();
        RebuildTile();
        Render(_gpu.Current);
        _applying = false;
    }

    public void Open()
    {
        ShowEditor();
        Render(_gpu.Current);
        PaintChrome();
        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
    }

    public void OpenCustomize()
    {
        ShowCustomize();
        PaintChrome();
        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
    }

    private void ApplyOptions() =>
        _gpu.SetOptions(new GpuSampleOptions
        {
            Vram = _settings.ShowVram,
            Engines = _settings.ShowEngines,
            AdapterId = _settings.AdapterId
        });

    private Brush Ink() => TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

    private void PaintChrome()
    {
        var ink = Ink();
        Foreground = ink;
        _value.Foreground = ink;
        _panelPercent.Foreground = ink;
        _adapterName.Foreground = ink;
        _unsupported.Foreground = ink;
        _vramLabel.Foreground = ink;
        _vramValue.Foreground = ink;
        _flyoutTitle.Foreground = ink;
        _customizeTitle.Foreground = ink;
        _backLabel.Foreground = ink;
        _adapterCaption.Foreground = ink;
        _vramBox.Foreground = ink;
        _engineBox.Foreground = ink;
        _chartBox.Foreground = ink;
        WidgetChrome.PaintSurfaces(_tile, _panel, _isLight);
        WidgetChrome.PaintCombo(_adapterBox, _isLight);
        _glyph.Foreground = TokenBrushes.From(DockTokens.Gpu);
    }

    private void Persist()
    {
        if (_applying)
        {
            return;
        }

        SettingsChanged?.Invoke(this, _settings);
    }

    private void RebuildTile()
    {
        _dockBody.Children.Clear();
        _dockBody.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _dockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _value.VerticalAlignment = VerticalAlignment.Center;
        _value.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _value.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _value.FontSize = _side ? DockTokens.PrimaryFontSize : 17;
        _value.TextWrapping = TextWrapping.NoWrap;
        _dockChart.Width = _side ? DockTokens.SideTileWidth - 12 : DockTokens.SparklineDockWidth;
        _dockChart.Height = _side ? 16 : 28;
        _dockChart.Margin = _side ? new Thickness(0, 4, 0, 0) : new Thickness(6, 0, 0, 0);
        _dockChart.HorizontalAlignment = HorizontalAlignment.Center;
        _dockBody.Children.Add(_glyph);
        _dockBody.Children.Add(_value);
        if (_settings.ChartInDock)
        {
            _dockBody.Children.Add(_dockChart);
        }

        _dockChart.Visibility = _settings.ChartInDock ? Visibility.Visible : Visibility.Collapsed;
        PaintChrome();
    }

    private void Render(GpuSnapshot snapshot)
    {
        _value.Text = snapshot.Supported
            ? GpuFormat.Percent(snapshot.Percent, snapshot.PercentAvailable)
            : UiText.GpuUnavailable;
        _value.Foreground = Ink();
        _panelPercent.Text = _value.Text;
        _panelPercent.Foreground = Ink();
        _adapterName.Text = snapshot.AdapterName ?? "";
        _adapterName.Foreground = Ink();
        _unsupported.Foreground = Ink();
        _vramLabel.Foreground = Ink();
        _vramValue.Foreground = Ink();
        _unsupported.Visibility = snapshot.Supported ? Visibility.Collapsed : Visibility.Visible;
        _panelPercent.Visibility = snapshot.Supported ? Visibility.Visible : Visibility.Collapsed;
        _adapterName.Visibility = snapshot.Supported && !string.IsNullOrWhiteSpace(snapshot.AdapterName)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _vramRow.Visibility = _settings.ShowVram ? Visibility.Visible : Visibility.Collapsed;
        _vramValue.Text = GpuFormat.Vram(snapshot.VramUsedBytes, snapshot.VramTotalBytes, snapshot.VramAvailable);
        _engines.Visibility = _settings.ShowEngines ? Visibility.Visible : Visibility.Collapsed;
        PaintEngines(snapshot);
        FillAdapters(snapshot.Adapters);
        if (_settings.ChartInDock)
        {
            _dockChart.Render(snapshot.History, [], true, false);
        }
    }

    private void PaintEngines(GpuSnapshot snapshot)
    {
        _engines.Children.Clear();
        AddEngine(UiText.GpuEngine3D, snapshot.Engine3D);
        AddEngine(UiText.GpuEngineCopy, snapshot.EngineCopy);
        AddEngine(UiText.GpuEngineVideo, snapshot.EngineVideo);
        AddEngine(UiText.GpuEngineCompute, snapshot.EngineCompute);
    }

    private void AddEngine(string name, double? percent)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = WidgetCopy.Secondary();
        label.Text = name;
        label.Foreground = Ink();
        var value = WidgetCopy.Primary();
        value.Text = GpuFormat.Engine(percent);
        value.Foreground = Ink();
        row.Children.Add(label);
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        _engines.Children.Add(row);
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Gpu));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        _vramRow.Margin = new Thickness(0, 16, 0, 0);
        _vramRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _vramRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _vramRow.Children.Add(_vramLabel);
        Grid.SetColumn(_vramValue, 1);
        _vramRow.Children.Add(_vramValue);
        var page = new StackPanel
        {
            Children = { header, _unsupported, _panelPercent, _adapterName, _vramRow, _engines }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Grid BuildCustomize()
    {
        _backLabel = HeaderLabel(UiText.Back);
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, ShowEditor, 12),
                _backLabel
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        _customizeTitle = HeaderLabel(UiText.Customize);
        _customizeTitle.Margin = new Thickness(8, 0, 0, 0);
        _customizeTitle.FontWeight = FontWeights.SemiBold;
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, _customizeTitle }
        }, close);

        _adapterCaption = HeaderLabel(UiText.GpuAdapter);
        _adapterCaption.Opacity = 0.62;
        _adapterCaption.Margin = new Thickness(0, 8, 0, 8);
        WidgetChrome.PaintCombo(_adapterBox, true);
        _adapterBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _adapterBox.SelectedItem is not ComboChoice choice)
            {
                return;
            }

            _settings = _settings with { AdapterId = string.IsNullOrWhiteSpace(choice.Id) ? null : choice.Id };
            ApplyOptions();
            Persist();
            Render(_gpu.Current);
        };

        _vramBox.Content = UiText.GpuVram;
        _vramBox.Margin = new Thickness(0, 14, 0, 4);
        _vramBox.Checked += (_, _) => SetFlag(vram: true);
        _vramBox.Unchecked += (_, _) => SetFlag(vram: false);
        _engineBox.Content = UiText.GpuEngines;
        _engineBox.Margin = new Thickness(0, 4, 0, 4);
        _engineBox.Checked += (_, _) => SetFlag(engines: true);
        _engineBox.Unchecked += (_, _) => SetFlag(engines: false);
        _chartBox.Content = UiText.DockChart;
        _chartBox.Margin = new Thickness(0, 4, 0, 4);
        _chartBox.Checked += (_, _) => SetFlag(chart: true);
        _chartBox.Unchecked += (_, _) => SetFlag(chart: false);

        var page = new StackPanel
        {
            Children = { header, _adapterCaption, _adapterBox, _vramBox, _engineBox, _chartBox }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private string _adapterKey = "";

    private void FillAdapters(IReadOnlyList<GpuAdapterInfo> adapters)
    {
        var selected = _settings.AdapterId ?? adapters.FirstOrDefault()?.Id;
        var key = string.Join("|", adapters.Select(adapter => adapter.Id + "=" + adapter.Name)) + "|" + (selected ?? "");
        if (key == _adapterKey && _adapterBox.Items.Count > 0)
        {
            return;
        }

        _adapterKey = key;
        var applying = _applying;
        _applying = true;
        _adapterBox.Items.Clear();
        foreach (var adapter in adapters)
        {
            _adapterBox.Items.Add(new ComboChoice(adapter.Id, adapter.Name));
        }

        if (_adapterBox.Items.Count == 0)
        {
            _adapterBox.Items.Add(new ComboChoice("", "—"));
        }

        WidgetChrome.SelectChoice(_adapterBox, selected);
        _applying = applying;
    }

    private void SetFlag(bool? vram = null, bool? engines = null, bool? chart = null)
    {
        if (_applying)
        {
            return;
        }

        _settings = _settings with
        {
            ShowVram = vram ?? _settings.ShowVram,
            ShowEngines = engines ?? _settings.ShowEngines,
            ChartInDock = chart ?? _settings.ChartInDock
        };
        ApplyOptions();
        Persist();
        RebuildTile();
        Render(_gpu.Current);
    }

    private void PaintCustomize()
    {
        _vramBox.IsChecked = _settings.ShowVram;
        _engineBox.IsChecked = _settings.ShowEngines;
        _chartBox.IsChecked = _settings.ChartInDock;
        FillAdapters(_gpu.Current.Adapters);
    }

    private void ShowEditor()
    {
        _popup.StaysOpen = false;
        _customizePage.Visibility = Visibility.Collapsed;
        _editorPage.Visibility = Visibility.Visible;
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

}
