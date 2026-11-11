using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class NetworkWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IWidgetModeAware, IDockEdgeAware, INetworkConfigurable, IWidgetThemeAware, IIslandLayoutAware, ILocalizable
{
    private const double DockChartHeight = 28;
    private const string DockValueSample = "000.00";
    private const string DockUnitSample = "MB/s";

    private readonly INetworkProvider _network;
    private readonly EventHandler<NetworkSnapshot> _updated;
    private readonly Grid _dockBody = new();
    private readonly TextBlock _downArrow = Arrow("↓");
    private readonly TextBlock _upArrow = Arrow("↑");
    private readonly TextBlock _downValue = WidgetCopy.Primary();
    private readonly TextBlock _upValue = WidgetCopy.Primary();
    private readonly TextBlock _downUnit = WidgetCopy.Secondary();
    private readonly TextBlock _upUnit = WidgetCopy.Secondary();
    private readonly TextBlock _status = WidgetCopy.Primary();
    private readonly Grid _rateGrid = new();
    private readonly StackPanel _copy = new();
    private readonly NetworkSparkline _dockChart = new();
    private readonly TextBlock _panelDownArrow = Arrow("↓");
    private readonly TextBlock _panelUpArrow = Arrow("↑");
    private readonly TextBlock _panelDownName = WidgetCopy.Primary();
    private readonly TextBlock _panelUpName = WidgetCopy.Primary();
    private readonly TextBlock _panelDownValue = WidgetCopy.Primary();
    private readonly TextBlock _panelUpValue = WidgetCopy.Primary();
    private readonly TextBlock _panelDownUnit = WidgetCopy.Secondary();
    private readonly TextBlock _panelUpUnit = WidgetCopy.Secondary();
    private readonly NetworkSparkline _panelChart = new();
    private readonly TextBlock _footer = WidgetCopy.Secondary();
    private readonly ComboBox _showBox = new();
    private readonly CheckBox _dockChartBox = new();
    private readonly CheckBox _popupChartBox = new();
    private readonly CheckBox _wifiBox = new();
    private readonly TextBlock _wifiLabel = WidgetCopy.Secondary();
    private readonly TextBlock _wifiValue = WidgetCopy.Primary();
    private readonly Grid _wifiRow = new();
    private readonly Grid _panelRates = new();
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _showCaption = null!;
    private NetworkWidgetSettings _settings = NetworkWidgetSettings.Default;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private double _copyWidth;
    private double _laidOutWidth = double.NaN;
    private bool _applying;
    private bool _isLight = true;
    private bool _side;

    public NetworkWidget(INetworkProvider network, WidgetDensity density = WidgetDensity.Expanded)
    {
        _network = network;
        _density = density;
        _updated = UiPump.Coalesce<NetworkSnapshot>(Dispatcher, Render);
        StyleRate(_downValue, _downUnit);
        StyleRate(_upValue, _upUnit);
        StyleRate(_panelDownValue, _panelDownUnit);
        StyleRate(_panelUpValue, _panelUpUnit);
        _status.FontSize = 13;
        _panelDownName.Text = UiText.Download;
        _panelUpName.Text = UiText.Upload;
        _footer.Text = NetworkFormat.Footer;
        _footer.Margin = new Thickness(0, 10, 0, 0);
        _wifiLabel.Text = UiText.WifiSignal;
        _dockChart.Width = DockTokens.SparklineDockWidth;
        _dockChart.Height = DockChartHeight;
        _dockChart.Margin = new Thickness(4, 0, 0, 0);
        _dockChart.VerticalAlignment = VerticalAlignment.Center;
        PlaceDockRates();
        PlacePanelRates();
        _panelChart.Height = 120;
        _panelChart.Margin = new Thickness(0, 16, 0, 0);
        _panelChart.StrokeThickness = 2.2;
        _downArrow.Foreground = TokenBrushes.From(DockTokens.Network);
        _upArrow.Foreground = TokenBrushes.From(DockTokens.NetworkUp);
        _panelDownArrow.Foreground = TokenBrushes.From(DockTokens.Network);
        _panelUpArrow.Foreground = TokenBrushes.From(DockTokens.NetworkUp);
        _dockChart.ReceiveStroke = TokenBrushes.From(DockTokens.Network);
        _dockChart.SendStroke = TokenBrushes.From(DockTokens.NetworkUp);
        _panelChart.ReceiveStroke = TokenBrushes.From(DockTokens.Network);
        _panelChart.SendStroke = TokenBrushes.From(DockTokens.NetworkUp);

        _tile = WidgetChrome.Wrap(_dockBody, 0);
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
            _network.Updated += _updated;
            _network.CollectWifi = _settings.ShowWifiSignal;
            RebuildTile();
            Render(_network.Current);
        };
        Unloaded += (_, _) =>
        {
            _network.Updated -= _updated;
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ApplyTheme(true);
        RebuildTile();
        Render(network.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<NetworkWidgetSettings>? SettingsChanged;

    public event EventHandler? IslandLayoutChanged;

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        RebuildTile();
        WidgetChrome.FitEdge(_tile, _side, 0);
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = TokenBrushes.From(isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        _downArrow.Foreground = TokenBrushes.From(DockTokens.Network);
        _upArrow.Foreground = TokenBrushes.From(DockTokens.NetworkUp);
        _panelDownArrow.Foreground = TokenBrushes.From(DockTokens.Network);
        _panelUpArrow.Foreground = TokenBrushes.From(DockTokens.NetworkUp);
        _dockChart.ReceiveStroke = TokenBrushes.From(DockTokens.Network);
        _dockChart.SendStroke = TokenBrushes.From(DockTokens.NetworkUp);
        _panelChart.ReceiveStroke = TokenBrushes.From(DockTokens.Network);
        _panelChart.SendStroke = TokenBrushes.From(DockTokens.NetworkUp);
    }

    public void ApplyLocale()
    {
        _applying = true;
        _panelDownName.Text = UiText.Download;
        _panelUpName.Text = UiText.Upload;
        _footer.Text = NetworkFormat.Footer;
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Network);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _showCaption.Text = UiText.Display;
        _dockChartBox.Content = UiText.DockChart;
        _popupChartBox.Content = UiText.PopupChart;
        _wifiBox.Content = UiText.WifiSignal;
        FillShowBox();
        _applying = false;
        Render(_network.Current);
    }

    private void FillShowBox()
    {
        var selected = _showBox.SelectedItem is ComboChoice choice ? choice.Id : _settings.Show;
        _showBox.Items.Clear();
        foreach (var show in NetworkShows.All)
        {
            _showBox.Items.Add(new ComboChoice(show, NetworkFormat.ShowTitle(show)));
        }

        WidgetChrome.SelectChoice(_showBox, selected);
    }

    public void ApplyDensity(WidgetDensity density)
    {
        var changed = _density != density;
        _density = density;
        RebuildTile();
        Render(_network.Current);
        if (changed)
        {
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplySettings(NetworkWidgetSettings settings)
    {
        _applying = true;
        _settings = NetworkWidgetSettings.Normalize(settings);
        _network.CollectWifi = _settings.ShowWifiSignal;
        PaintCustomize();
        RebuildTile();
        Render(_network.Current);
        _applying = false;
    }

    public void Open()
    {
        ShowEditor();
        Render(_network.Current);
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

    private void RebuildTile()
    {
        Release(_copy);
        Release(_dockChart);
        _dockBody.Children.Clear();
        var compact = _density != WidgetDensity.Expanded;
        var chart = !compact && _settings.ChartInDock;
        if (_side)
        {
            var inner = DockTokens.SideTileWidth - 12;
            _copy.HorizontalAlignment = HorizontalAlignment.Center;
            _copy.Width = double.NaN;
            _copy.MinWidth = 0;
            _copy.MaxWidth = inner;
            _dockChart.Width = inner;
            _dockChart.Height = 16;
            _dockChart.Margin = new Thickness(0, 4, 0, 0);
            _dockChart.HorizontalAlignment = HorizontalAlignment.Stretch;
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(_copy);
            if (chart)
            {
                stack.Children.Add(_dockChart);
            }

            _dockBody.Children.Add(stack);
            _dockBody.Width = double.NaN;
            _dockBody.MinWidth = 0;
            WidgetChrome.ApplyTheme(_tile, _isLight);
            _tile.InvalidateMeasure();
            InvalidateMeasure();
            return;
        }

        _dockChart.Width = DockTokens.SparklineDockWidth;
        _dockChart.Height = DockChartHeight;
        _dockChart.Margin = new Thickness(4, 0, 0, 0);
        var width = chart ? _copyWidth + 4 + DockTokens.SparklineDockWidth : _copyWidth;
        _dockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _copy.HorizontalAlignment = HorizontalAlignment.Left;
        _copy.Width = _copyWidth;
        _copy.MinWidth = _copyWidth;
        if (chart)
        {
            var row = new Grid
            {
                Width = width,
                MinWidth = width,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_copyWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(_copy);
            Grid.SetColumn(_dockChart, 1);
            row.Children.Add(_dockChart);
            _dockBody.Children.Add(row);
        }
        else
        {
            _dockBody.Children.Add(_copy);
        }

        _dockBody.Width = width;
        _dockBody.MinWidth = width;
        WidgetChrome.ApplyTheme(_tile, _isLight);
        _tile.InvalidateMeasure();
        InvalidateMeasure();
        if (double.IsNaN(_laidOutWidth) || Math.Abs(_laidOutWidth - width) > 0.5)
        {
            _laidOutWidth = width;
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PlaceDockRates()
    {
        var valueWidth = MeasureText(_downValue, DockValueSample);
        var unitWidth = MeasureText(_downUnit, DockUnitSample);
        _copyWidth = MeasureText(_downArrow, "↓") + 4 + valueWidth + 3 + unitWidth;
        _rateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _rateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(valueWidth) });
        _rateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitWidth + 3) });
        _rateGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _rateGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _downArrow.Margin = new Thickness(0, 0, 4, 0);
        _upArrow.Margin = new Thickness(0, 2, 4, 0);
        _upValue.Margin = new Thickness(0, 2, 0, 0);
        _upUnit.Margin = new Thickness(3, 2, 0, 0);
        _downValue.Width = valueWidth;
        _upValue.Width = valueWidth;
        _downUnit.Width = unitWidth;
        _upUnit.Width = unitWidth;
        _downValue.HorizontalAlignment = HorizontalAlignment.Right;
        _upValue.HorizontalAlignment = HorizontalAlignment.Right;
        _downValue.TextAlignment = TextAlignment.Right;
        _upValue.TextAlignment = TextAlignment.Right;
        _downUnit.HorizontalAlignment = HorizontalAlignment.Left;
        _upUnit.HorizontalAlignment = HorizontalAlignment.Left;
        _status.Width = _copyWidth;
        _rateGrid.Width = _copyWidth;
        _rateGrid.MinWidth = _copyWidth;
        _rateGrid.HorizontalAlignment = HorizontalAlignment.Left;
        Cell(_rateGrid, _downArrow, 0, 0);
        Cell(_rateGrid, _downValue, 1, 0);
        Cell(_rateGrid, _downUnit, 2, 0);
        Cell(_rateGrid, _upArrow, 0, 1);
        Cell(_rateGrid, _upValue, 1, 1);
        Cell(_rateGrid, _upUnit, 2, 1);
        _copy.VerticalAlignment = VerticalAlignment.Center;
        _copy.Children.Add(_status);
        _copy.Children.Add(_rateGrid);
    }

    private void PlacePanelRates()
    {
        _panelRates.Margin = new Thickness(0, 8, 0, 0);
        _panelRates.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _panelRates.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _panelRates.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _panelRates.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _panelRates.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _panelRates.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _panelRates.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _panelDownArrow.Margin = new Thickness(0, 0, 8, 0);
        _panelUpArrow.Margin = new Thickness(0, 8, 8, 0);
        _panelDownName.Margin = new Thickness(0, 0, 8, 0);
        _panelUpName.Margin = new Thickness(0, 8, 8, 0);
        _panelUpValue.Margin = new Thickness(0, 8, 0, 0);
        _panelUpUnit.Margin = new Thickness(3, 8, 0, 0);
        _panelDownValue.HorizontalAlignment = HorizontalAlignment.Right;
        _panelUpValue.HorizontalAlignment = HorizontalAlignment.Right;
        _panelDownValue.TextAlignment = TextAlignment.Right;
        _panelUpValue.TextAlignment = TextAlignment.Right;
        _panelDownUnit.HorizontalAlignment = HorizontalAlignment.Left;
        _panelUpUnit.HorizontalAlignment = HorizontalAlignment.Left;
        Cell(_panelRates, _panelDownArrow, 0, 0);
        Cell(_panelRates, _panelDownName, 1, 0);
        Cell(_panelRates, _panelDownValue, 3, 0);
        Cell(_panelRates, _panelDownUnit, 4, 0);
        Cell(_panelRates, _panelUpArrow, 0, 1);
        Cell(_panelRates, _panelUpName, 1, 1);
        Cell(_panelRates, _panelUpValue, 3, 1);
        Cell(_panelRates, _panelUpUnit, 4, 1);
    }

    private static void Cell(Grid grid, UIElement child, int column, int row)
    {
        Grid.SetColumn(child, column);
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private void Render(NetworkSnapshot snapshot)
    {
        var details = snapshot.AdapterName is not null
                      || snapshot.ReceiveBytesPerSecond > 0
                      || snapshot.SendBytesPerSecond > 0
                      || snapshot.ReceiveHistory.Count > 0;
        var down = ByteRate.Split(snapshot.ReceiveBytesPerSecond);
        var up = ByteRate.Split(snapshot.SendBytesPerSecond);
        var showDown = _settings.ShowsDownload;
        var showUp = _settings.ShowsUpload;
        if (!snapshot.IsConnected)
        {
            _status.Text = UiText.Disconnected;
            _status.Visibility = Visibility.Visible;
            HideRates();
        }
        else if (!details)
        {
            _status.Text = UiText.ConnectedDetailsOff;
            _status.Visibility = Visibility.Visible;
            HideRates();
        }
        else
        {
            _status.Text = string.Empty;
            _status.Visibility = Visibility.Collapsed;
            _rateGrid.Visibility = Visibility.Visible;
            _downValue.Text = down.Value;
            _downUnit.Text = down.Unit;
            _upValue.Text = up.Value;
            _upUnit.Text = up.Unit;
            _downArrow.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
            _upArrow.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
            _downValue.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
            _downUnit.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
            _upValue.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
            _upUnit.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
        }

        if (snapshot.IsConnected && details)
        {
            _panelDownValue.Text = down.Value;
            _panelDownUnit.Text = down.Unit;
            _panelUpValue.Text = up.Value;
            _panelUpUnit.Text = up.Unit;
        }
        else
        {
            _panelDownValue.Text = "—";
            _panelDownUnit.Text = string.Empty;
            _panelUpValue.Text = "—";
            _panelUpUnit.Text = string.Empty;
        }
        _panelDownArrow.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
        _panelDownName.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
        _panelDownValue.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
        _panelDownUnit.Visibility = showDown ? Visibility.Visible : Visibility.Collapsed;
        _panelUpArrow.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
        _panelUpName.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
        _panelUpValue.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
        _panelUpUnit.Visibility = showUp ? Visibility.Visible : Visibility.Collapsed;
        _dockChart.Visibility = _density == WidgetDensity.Expanded && _settings.ChartInDock
            ? Visibility.Visible
            : Visibility.Collapsed;
        _panelChart.Visibility = _settings.ChartInPopup ? Visibility.Visible : Visibility.Collapsed;
        _footer.Visibility = _settings.ChartInPopup ? Visibility.Visible : Visibility.Collapsed;
        _wifiValue.Text = snapshot.IsWifi
            ? SysActivityFormat.Percent(snapshot.WifiSignalPercent ?? 0, snapshot.WifiSignalPercent is not null)
            : "—";
        _wifiRow.Visibility = _settings.ShowWifiSignal ? Visibility.Visible : Visibility.Collapsed;
        _dockChart.Render(snapshot.ReceiveHistory, snapshot.SendHistory, _settings.ShowsDownload, _settings.ShowsUpload);
        _panelChart.Render(snapshot.ReceiveHistory, snapshot.SendHistory, _settings.ShowsDownload, _settings.ShowsUpload);
        ToolTip = string.IsNullOrWhiteSpace(snapshot.AdapterName) ? null : snapshot.AdapterName;
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Network));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        _wifiRow.Margin = new Thickness(0, 12, 0, 0);
        _wifiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _wifiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _wifiRow.Children.Add(_wifiLabel);
        Grid.SetColumn(_wifiValue, 1);
        _wifiRow.Children.Add(_wifiValue);
        _panelChart.HorizontalAlignment = HorizontalAlignment.Stretch;
        var page = new StackPanel
        {
            Children = { header, _panelRates, _wifiRow, _panelChart, _footer }
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

        _showCaption = HeaderLabel(UiText.Display);
        _showCaption.Opacity = 0.62;
        _showCaption.Margin = new Thickness(0, 8, 0, 8);
        WidgetChrome.PaintCombo(_showBox, true);
        FillShowBox();

        _showBox.SelectedIndex = 0;
        _showBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _showBox.SelectedItem is not ComboChoice choice)
            {
                return;
            }

            _settings = _settings with { Show = NetworkShows.Normalize(choice.Id) };
            Persist();
            RebuildTile();
            Render(_network.Current);
        };

        _dockChartBox.Content = UiText.DockChart;
        _dockChartBox.Margin = new Thickness(0, 14, 0, 4);
        _dockChartBox.Checked += (_, _) => SetChart(dock: true, on: true);
        _dockChartBox.Unchecked += (_, _) => SetChart(dock: true, on: false);
        _popupChartBox.Content = UiText.PopupChart;
        _popupChartBox.Margin = new Thickness(0, 4, 0, 4);
        _popupChartBox.Checked += (_, _) => SetChart(dock: false, on: true);
        _popupChartBox.Unchecked += (_, _) => SetChart(dock: false, on: false);
        _wifiBox.Content = UiText.WifiSignal;
        _wifiBox.Margin = new Thickness(0, 4, 0, 4);
        _wifiBox.Checked += (_, _) => SetWifi(true);
        _wifiBox.Unchecked += (_, _) => SetWifi(false);

        var page = new StackPanel
        {
            Children = { header, _showCaption, _showBox, _dockChartBox, _popupChartBox, _wifiBox }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private void SetChart(bool dock, bool on)
    {
        if (_applying)
        {
            return;
        }

        _settings = dock
            ? _settings with { ChartInDock = on }
            : _settings with { ChartInPopup = on };
        Persist();
        RebuildTile();
        Render(_network.Current);
    }

    private void SetWifi(bool on)
    {
        if (_applying)
        {
            return;
        }

        _settings = _settings with { ShowWifiSignal = on };
        _network.CollectWifi = on;
        Persist();
        Render(_network.Current);
    }

    private void PaintCustomize()
    {
        WidgetChrome.SelectChoice(_showBox, _settings.Show);

        _dockChartBox.IsChecked = _settings.ChartInDock;
        _popupChartBox.IsChecked = _settings.ChartInPopup;
        _wifiBox.IsChecked = _settings.ShowWifiSignal;
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

    private void HideRates()
    {
        _rateGrid.Visibility = Visibility.Collapsed;
        _downArrow.Visibility = Visibility.Collapsed;
        _upArrow.Visibility = Visibility.Collapsed;
        _downValue.Visibility = Visibility.Collapsed;
        _downUnit.Visibility = Visibility.Collapsed;
        _upValue.Visibility = Visibility.Collapsed;
        _upUnit.Visibility = Visibility.Collapsed;
    }

    private static void StyleRate(TextBlock value, TextBlock unit)
    {
        value.FontSize = 13;
        value.VerticalAlignment = VerticalAlignment.Center;
        value.TextWrapping = TextWrapping.NoWrap;
        Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular);
        unit.FontSize = 11;
        unit.Margin = new Thickness(3, 0, 0, 0);
        unit.VerticalAlignment = VerticalAlignment.Center;
        unit.TextWrapping = TextWrapping.NoWrap;
    }

    private static double MeasureText(TextBlock block, string sample)
    {
        var text = new FormattedText(
            sample,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
            block.FontSize,
            Brushes.Black,
            1.0);
        return Math.Ceiling(text.WidthIncludingTrailingWhitespace);
    }

    private static TextBlock Arrow(string glyph) => new()
    {
        Text = glyph,
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
        Tag = "tint"
    };

    private static void Release(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, element):
                content.Content = null;
                break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, element):
                presenter.Content = null;
                break;
        }
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
