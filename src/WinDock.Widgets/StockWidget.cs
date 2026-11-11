using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class StockWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IStockConfigurable, IWidgetThemeAware, ILocalizable, IDockEdgeAware, IWidgetModeAware
{
    private WidgetDensity _density = WidgetDensity.Expanded;
    private readonly StackPanel _dockCompact = new()
    {
        Orientation = Orientation.Vertical,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };
    private readonly TextBlock _compactName = WidgetCopy.Primary();
    private readonly Grid _compactRow = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 2, 0, 0)
    };
    private readonly StockChart _compactChart = new()
    {
        Spark = true,
        Height = 20,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = new Thickness(0, 0, 4, 0),
        IsHitTestVisible = false
    };
    private readonly StackPanel _compactQuote = new()
    {
        Orientation = Orientation.Vertical,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right
    };
    private readonly TextBlock _compactPrice = WidgetCopy.Primary();
    private readonly TextBlock _compactChange = WidgetCopy.Primary();
    private readonly IStockProvider _stocks;
    private readonly EventHandler _changed;
    private readonly DispatcherTimer _searchTimer;
    private readonly PhosphorGlyph _glyph = WidgetChrome.Glyph(PhosphorIcon.ChartBar, DockTokens.StockUp);
    private readonly TextBlock _emptyLabel = WidgetCopy.Primary();
    private readonly TextBlock _dockName = WidgetCopy.Primary();
    private readonly TextBlock _dockBadgeText = new();
    private readonly TextBlock _dockCode = WidgetCopy.Secondary();
    private readonly TextBlock _dockBoardText = new();
    private readonly TextBlock _dockPrice = WidgetCopy.Primary();
    private readonly TextBlock _dockChange = WidgetCopy.Primary();
    private readonly Border _dockBadge;
    private readonly Border _dockBoard;
    private readonly StockChart _dockChart = new()
    {
        Spark = true,
        Width = DockTokens.StockSparkWidth,
        Height = DockTokens.StockSparkHeight,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 1, 0, 1),
        IsHitTestVisible = false
    };
    private readonly Grid _dockFace = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _dockMeta = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 2, 0, 0)
    };
    private readonly StackPanel _dock;
    private bool _side;
    private readonly StackPanel _dockLeft;
    private readonly StackPanel _dockQuote = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(12, 0, 0, 0)
    };
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Popup _popup;
    private readonly StackPanel _results = new();
    private readonly TextBox _searchBox = new()
    {
        BorderThickness = new Thickness(0),
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(8, 0, 8, 0)
    };
    private readonly ComboBox _rangeBox = new() { Width = 84 };
    private readonly StockChart _chart = new() { Height = DockTokens.StockChartHeight };
    private TextBlock _detailTitle = null!;
    private TextBlock _symbol = null!;
    private TextBlock _name = null!;
    private TextBlock _money = null!;
    private TextBlock _changeLine = null!;
    private TextBlock _hint = null!;
    private TextBlock _asOf = null!;
    private TextBlock _previous = null!;
    private TextBlock _sample = null!;
    private TextBlock _chartStatus = null!;
    private TextBlock _searchTitle = null!;
    private TextBlock _searchCaption = null!;
    private TextBlock _searchStatus = null!;
    private UIElement _searchBack = null!;
    private StackPanel _detailPage = null!;
    private StackPanel _searchPage = null!;
    private StackPanel _customizePage = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _islandCaption = null!;
    private TextBlock _quoteCaption = null!;
    private CheckBox _nameBox = null!;
    private CheckBox _badgeBox = null!;
    private CheckBox _codeBox = null!;
    private CheckBox _boardBox = null!;
    private CheckBox _priceBox = null!;
    private CheckBox _sparkBox = null!;
    private CheckBox _changeBox = null!;
    private CheckBox _currencyBox = null!;
    private CheckBox _hintBox = null!;
    private CheckBox _asOfBox = null!;
    private CheckBox _previousBox = null!;
    private CheckBox _sampleBox = null!;
    private StockWidgetSettings _settings = StockWidgetSettings.Default;
    private StockSymbol? _detailSymbol;
    private bool _applying;
    private bool _isLight = true;
    private int _searchGen;
    private int _seriesGen;
    private string? _sparkFor;
    private DateTime _sparkAttempt = DateTime.MinValue;

    public StockWidget(IStockProvider stocks)
    {
        _stocks = stocks;
        _changed = UiPump.Coalesce(Dispatcher, Refresh);
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            _ = RunSearchAsync();
        };

        _emptyLabel.FontSize = 13;
        _emptyLabel.VerticalAlignment = VerticalAlignment.Center;
        _dockName.FontSize = 13;
        _dockName.LineHeight = 16;
        _dockName.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _dockName.TextWrapping = TextWrapping.NoWrap;
        _dockBadge = MarketBadge(_dockBadgeText);
        _dockCode.FontSize = 11;
        _dockCode.Margin = new Thickness(4, 0, 0, 0);
        _dockCode.VerticalAlignment = VerticalAlignment.Center;
        _dockCode.TextWrapping = TextWrapping.NoWrap;
        _dockBoardText.FontFamily = WidgetFonts.Ui;
        _dockBoardText.FontSize = 10;
        _dockBoardText.VerticalAlignment = VerticalAlignment.Center;
        _dockBoard = new Border
        {
            Height = 14,
            CornerRadius = new CornerRadius(DockTokens.StockMarkRadius),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _dockBoardText
        };
        _dockPrice.FontSize = 13;
        _dockPrice.LineHeight = 15;
        _dockPrice.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _dockPrice.HorizontalAlignment = HorizontalAlignment.Right;
        _dockPrice.TextWrapping = TextWrapping.NoWrap;
        _dockChange.FontSize = 11;
        _dockChange.LineHeight = 13;
        _dockChange.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _dockChange.FontWeight = FontWeights.SemiBold;
        _dockChange.HorizontalAlignment = HorizontalAlignment.Right;
        _dockChange.Tag = "tint";
        _dockChange.TextWrapping = TextWrapping.NoWrap;

        _dockMeta.Children.Add(_dockBadge);
        _dockMeta.Children.Add(_dockCode);
        _dockMeta.Children.Add(_dockBoard);
        _dockLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _dockName, _dockMeta } };
        _dockQuote.Children.Add(_dockPrice);
        _dockQuote.Children.Add(_dockChart);
        _dockQuote.Children.Add(_dockChange);
        _dockFace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _dockFace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dockFace.Children.Add(_dockLeft);
        Grid.SetColumn(_dockQuote, 1);
        _dockFace.Children.Add(_dockQuote);

        _compactName.FontSize = 12;
        _compactName.FontWeight = FontWeights.SemiBold;
        _compactName.LineHeight = 14;
        _compactName.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _compactName.TextTrimming = TextTrimming.CharacterEllipsis;
        _compactName.TextWrapping = TextWrapping.NoWrap;
        _compactName.HorizontalAlignment = HorizontalAlignment.Left;
        _compactName.TextAlignment = TextAlignment.Left;

        _compactPrice.FontSize = 11;
        _compactPrice.LineHeight = 13;
        _compactPrice.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _compactPrice.TextWrapping = TextWrapping.NoWrap;
        _compactPrice.HorizontalAlignment = HorizontalAlignment.Right;
        _compactPrice.TextAlignment = TextAlignment.Right;

        _compactChange.FontSize = 9.5;
        _compactChange.LineHeight = 11;
        _compactChange.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _compactChange.FontWeight = FontWeights.SemiBold;
        _compactChange.TextWrapping = TextWrapping.NoWrap;
        _compactChange.HorizontalAlignment = HorizontalAlignment.Right;
        _compactChange.TextAlignment = TextAlignment.Right;
        _compactChange.Margin = new Thickness(0, 1, 0, 0);
        _compactChange.Tag = "tint";

        _compactQuote.Children.Add(_compactPrice);
        _compactQuote.Children.Add(_compactChange);

        _compactRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _compactRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _compactRow.Children.Add(_compactChart);
        Grid.SetColumn(_compactQuote, 1);
        _compactRow.Children.Add(_compactQuote);

        _dockCompact.Children.Add(_compactName);
        _dockCompact.Children.Add(_compactRow);

        _dock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _glyph, _emptyLabel, _dockFace, _dockCompact }
        };

        _tile = WidgetChrome.Wrap(_dock, 156);
        _tile.Padding = new Thickness(8, 4, 10, 4);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        foreach (var range in StockRanges.All)
        {
            _rangeBox.Items.Add(new ComboChoice(StockRanges.Key(range), StockRanges.Key(range)));
        }

        WidgetChrome.PaintCombo(_rangeBox, false);
        _rangeBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _rangeBox.SelectedItem is not ComboChoice choice || choice.Id == _settings.Range)
            {
                return;
            }

            _settings = _settings with { Range = choice.Id };
            Persist();
            if (_detailSymbol is not null)
            {
                _ = LoadDetailAsync(false);
            }
        };

        _detailPage = BuildDetail();
        _searchPage = BuildSearch();
        _customizePage = BuildCustomize();
        _searchPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Collapsed;
        var body = new Grid();
        body.Children.Add(_detailPage);
        body.Children.Add(_searchPage);
        body.Children.Add(_customizePage);
        _panel = WidgetChrome.Flyout(body, DockTokens.StockFlyoutWidth);
        _panel.MinWidth = DockTokens.StockFlyoutWidth;
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
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

        _searchBox.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };

        Loaded += (_, _) =>
        {
            _stocks.Changed += _changed;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _stocks.Changed -= _changed;
            _popup.IsOpen = false;
            _searchTimer.Stop();
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ClipToBounds = false;
        MaxHeight = DockTokens.TileHeight;
        SyncRange();
        ApplyLocale();
        Refresh();
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<StockWidgetSettings>? SettingsChanged;

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        ArrangeDock();
        ApplyTileLayout();
        WidgetChrome.FitHost(this, _side);
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        ArrangeDock();
        ApplyTileLayout();
        PaintDock();
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void ApplyTileLayout()
    {
        var compact = _density != WidgetDensity.Expanded;
        var minWidth = compact ? 98 : 156;
        var padding = _side ? (Thickness?)null : (compact ? new Thickness(8, 4, 8, 4) : new Thickness(8, 4, 10, 4));
        WidgetChrome.FitEdge(_tile, _side, minWidth, padding);
        WidgetChrome.FitHost(this, _side);
    }

    private void ArrangeDock()
    {
        var inner = DockTokens.SideTileWidth - 16;
        _dock.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _dock.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyLabel.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _compactName.MaxWidth = _side ? inner : 110;
        _compactName.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _compactName.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;

        if (_side)
        {
            _compactChart.Visibility = Visibility.Collapsed;
            _compactQuote.HorizontalAlignment = HorizontalAlignment.Center;
            _compactPrice.HorizontalAlignment = HorizontalAlignment.Center;
            _compactPrice.TextAlignment = TextAlignment.Center;
            _compactChange.HorizontalAlignment = HorizontalAlignment.Center;
            _compactChange.TextAlignment = TextAlignment.Center;
            if (_compactRow.ColumnDefinitions.Count >= 2)
            {
                _compactRow.ColumnDefinitions[0].Width = GridLength.Auto;
                _compactRow.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        else
        {
            _compactQuote.HorizontalAlignment = HorizontalAlignment.Right;
            _compactPrice.HorizontalAlignment = HorizontalAlignment.Right;
            _compactPrice.TextAlignment = TextAlignment.Right;
            _compactChange.HorizontalAlignment = HorizontalAlignment.Right;
            _compactChange.TextAlignment = TextAlignment.Right;
            if (_compactRow.ColumnDefinitions.Count >= 2)
            {
                _compactRow.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                _compactRow.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
        }
        _dockFace.ColumnDefinitions.Clear();
        _dockFace.RowDefinitions.Clear();
        _dockFace.HorizontalAlignment = HorizontalAlignment.Center;
        _dockName.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _dockName.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _dockName.TextTrimming = TextTrimming.CharacterEllipsis;
        _dockName.MaxWidth = _side ? inner : double.PositiveInfinity;
        _dockMeta.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _dockLeft.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _dockPrice.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _dockPrice.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Right;
        _dockChange.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _dockChange.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Right;
        _dockChart.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _dockChart.Width = _side ? inner : DockTokens.StockSparkWidth;
        _dockChart.Height = _side ? 18 : DockTokens.StockSparkHeight;
        _dockQuote.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        _dockQuote.Margin = _side ? new Thickness(0, 2, 0, 0) : new Thickness(12, 0, 0, 0);
        if (_side)
        {
            _dockFace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _dockFace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(_dockLeft, 0);
            Grid.SetColumn(_dockQuote, 0);
            Grid.SetRow(_dockLeft, 0);
            Grid.SetRow(_dockQuote, 1);
            return;
        }

        _dockFace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _dockFace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(_dockLeft, 0);
        Grid.SetRow(_dockQuote, 0);
        Grid.SetColumn(_dockLeft, 0);
        Grid.SetColumn(_dockQuote, 1);
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void OpenCustomize()
    {
        ShowCustomize();
        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
    }

    public void ApplySettings(StockWidgetSettings settings)
    {
        var next = StockWidgetSettings.Normalize(settings);
        if (StockWidgetSettings.Same(_settings, next))
        {
            return;
        }

        var previous = _settings.Symbol?.Id;
        _settings = next;
        SyncRange();
        if (next.Symbol?.Id != previous)
        {
            _sparkFor = null;
        }

        if (_popup.IsOpen)
        {
            if (next.Symbol is { } symbol && symbol.Id != previous)
            {
                ShowDetail(symbol);
            }
            else if (next.Symbol is null)
            {
                ShowSearch();
            }
        }

        Refresh();
    }

    public void Open()
    {
        if (_settings.Symbol is null)
        {
            ShowSearch();
        }
        else
        {
            ShowDetail(_settings.Symbol);
        }

        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
        _ = _stocks.RefreshQuotesAsync();
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        _chart.IsLight = isLight;
        _dockChart.IsLight = isLight;
        _compactChart.IsLight = isLight;
        PaintBoardChrome();
    }

    public void ApplyLocale()
    {
        _emptyLabel.Text = UiText.StockChoose;
        _searchTitle.Text = UiText.StockChoose;
        _searchCaption.Text = UiText.StockSearch;
        _hint.Text = UiText.StockHoverHint;
        _customizeTitle.Text = UiText.Customize;
        _islandCaption.Text = UiText.StockIslandSection;
        _quoteCaption.Text = UiText.StockQuoteSection;
        _nameBox.Content = UiText.StockShowName;
        _badgeBox.Content = UiText.StockShowBadge;
        _codeBox.Content = UiText.StockShowCode;
        _boardBox.Content = UiText.StockShowBoard;
        _priceBox.Content = UiText.StockShowPrice;
        _sparkBox.Content = UiText.StockShowSpark;
        _changeBox.Content = UiText.StockShowChange;
        _currencyBox.Content = UiText.StockShowCurrency;
        _hintBox.Content = UiText.StockShowHint;
        _asOfBox.Content = UiText.StockShowAsOf;
        _previousBox.Content = UiText.StockShowPrevious;
        _sampleBox.Content = UiText.StockShowSample;
        if (_detailPage.Visibility == Visibility.Visible && _detailSymbol is not null)
        {
            _detailTitle.Text = DetailTitle(_detailSymbol);
        }

        Refresh();
    }

    private void Refresh()
    {
        PaintDock();
        PaintDetail();
        EnsureSparkline();
    }

    private void PaintDock()
    {
        var symbol = _settings.Symbol;
        if (symbol is null)
        {
            _glyph.Visibility = Visibility.Visible;
            _emptyLabel.Visibility = Visibility.Visible;
            _emptyLabel.Text = UiText.StockChoose;
            _dockFace.Visibility = Visibility.Collapsed;
            _dockCompact.Visibility = Visibility.Collapsed;
            return;
        }


        var quote = _stocks.QuoteFor(symbol.Id);
        var currency = quote?.Currency ?? StockLinks.Currency(symbol.Market);
        var priceText = _settings.ShowCurrency
            ? StockFormat.Money(quote?.Price, currency)
            : StockFormat.Price(quote?.Price);
        var changeText = quote?.Stale == true
            ? StockFormat.Percent(quote.ChangePercent) + " · " + UiText.StockStaleMark
            : StockFormat.Percent(quote?.ChangePercent);
        var toneBrush = TokenBrushes.From(StockFormat.Tone(quote?.ChangePercent));
        var staleTip = quote?.Stale == true ? UiText.StockStaleQuote : null;
        var series = _stocks.SeriesFor(symbol.Id, StockRange.Day);

        if (_density != WidgetDensity.Expanded)
        {
            var nameOn = _settings.ShowName;
            var codeOn = _settings.ShowCode;
            var priceOn = _settings.ShowPrice;
            var changeOn = _settings.ShowChange;

            if (!nameOn && !codeOn && !priceOn && !changeOn)
            {
                _glyph.Visibility = Visibility.Visible;
                _emptyLabel.Visibility = Visibility.Visible;
                _emptyLabel.Text = symbol.Symbol;
                _dockFace.Visibility = Visibility.Collapsed;
                _dockCompact.Visibility = Visibility.Collapsed;
                return;
            }

            _glyph.Visibility = Visibility.Collapsed;
            _emptyLabel.Visibility = Visibility.Collapsed;
            _dockFace.Visibility = Visibility.Collapsed;
            _dockCompact.Visibility = Visibility.Visible;

            if (nameOn)
            {
                _compactName.Text = DisplayName(symbol);
                _compactName.Visibility = Visibility.Visible;
            }
            else if (codeOn)
            {
                _compactName.Text = symbol.Symbol;
                _compactName.Visibility = Visibility.Visible;
            }
            else
            {
                _compactName.Text = "";
                _compactName.Visibility = Visibility.Collapsed;
            }

            var sparkOn = _settings.ShowSpark && !_side;
            if (sparkOn && series is { Points.Count: > 1 })
            {
                _compactChart.Visibility = Visibility.Visible;
                _compactChart.Rising = series.Points[^1].Price >= series.Points[0].Price;
                _compactChart.Points = series.Points;
                if (_compactRow.ColumnDefinitions.Count >= 2)
                {
                    _compactRow.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                }
            }
            else
            {
                _compactChart.Visibility = Visibility.Collapsed;
                if (_compactRow.ColumnDefinitions.Count >= 2)
                {
                    _compactRow.ColumnDefinitions[0].Width = GridLength.Auto;
                }
            }

            _compactPrice.Text = priceText;
            _compactPrice.Visibility = Shown(priceOn);
            _compactPrice.ToolTip = staleTip;

            _compactChange.Text = changeText;
            _compactChange.Foreground = toneBrush;
            _compactChange.Visibility = Shown(changeOn);

            _compactQuote.Visibility = Shown(priceOn || changeOn);
            _compactRow.Visibility = Shown(priceOn || changeOn || _compactChart.Visibility == Visibility.Visible);
            return;
        }

        _dockCompact.Visibility = Visibility.Collapsed;
        _dockName.Text = DisplayName(symbol);
        _dockName.Visibility = Shown(_settings.ShowName);
        _dockBadgeText.Text = StockLinks.Badge(symbol.Market);
        _dockBadge.Visibility = Shown(_settings.ShowBadge);
        _dockCode.Text = symbol.Symbol;
        _dockCode.Visibility = Shown(_settings.ShowCode);
        var board = StockLinks.BoardText(symbol);
        _dockBoardText.Text = board ?? "";
        _dockBoard.Visibility = Shown(_settings.ShowBoard && board is not null);
        var metaOn = _settings.ShowBadge || _settings.ShowCode || (_settings.ShowBoard && board is not null);
        _dockMeta.Visibility = Shown(metaOn);
        PaintBoardChrome();


        _dockPrice.Text = priceText;
        _dockPrice.Visibility = Shown(_settings.ShowPrice);
        _dockPrice.ToolTip = staleTip;
        _dockChange.Text = changeText;
        _dockChange.Foreground = toneBrush;
        _dockChange.Visibility = Shown(_settings.ShowChange);
        if (_settings.ShowSpark && series is { Points.Count: > 1 })
        {
            _dockChart.Visibility = Visibility.Visible;
            _dockChart.Rising = series.Points[^1].Price >= series.Points[0].Price;
            _dockChart.Points = series.Points;
        }
        else
        {
            _dockChart.Visibility = Visibility.Collapsed;
        }

        var leftOn = _settings.ShowName || metaOn;
        var rightOn = _settings.ShowPrice || _settings.ShowSpark || _settings.ShowChange;
        if (!leftOn && !rightOn)
        {
            _glyph.Visibility = Visibility.Visible;
            _emptyLabel.Visibility = Visibility.Visible;
            _emptyLabel.Text = symbol.Symbol;
            _dockFace.Visibility = Visibility.Collapsed;
            _dockCompact.Visibility = Visibility.Collapsed;
            return;
        }

        _glyph.Visibility = Visibility.Collapsed;
        _emptyLabel.Visibility = Visibility.Collapsed;
        _dockFace.Visibility = Visibility.Visible;
        _dockQuote.Visibility = Shown(rightOn);
    }

    private void PaintBoardChrome()
    {
        var stroke = _isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke;
        _dockBoard.BorderBrush = TokenBrushes.From(stroke);
    }

    private void PaintDetail()
    {
        var symbol = _detailSymbol;
        if (symbol is null || _detailPage.Visibility != Visibility.Visible)
        {
            return;
        }

        var range = StockRanges.Parse(_settings.Range);
        var series = _stocks.SeriesFor(symbol.Id, range);
        _detailTitle.Text = DetailTitle(symbol);
        _symbol.Text = symbol.Symbol;
        _symbol.Visibility = Shown(_settings.ShowCode);
        var board = StockLinks.BoardText(symbol);
        var caption = new List<string>();
        if (_settings.ShowName)
        {
            caption.Add(DisplayName(symbol));
        }

        if (_settings.ShowBoard && board is not null && !caption.Contains(board, StringComparer.Ordinal))
        {
            caption.Add(board);
        }

        if (caption.Count == 0)
        {
            _name.Visibility = Visibility.Collapsed;
        }
        else
        {
            _name.Visibility = Visibility.Visible;
            WidgetCopy.WrapUserText(_name, string.Join("  ·  ", caption), 220);
        }

        var quote = _stocks.QuoteFor(symbol.Id);
        var currency = quote?.Currency ?? StockLinks.Currency(symbol.Market);
        _money.Text = StockFormat.Money(quote?.Price, currency);
        _money.Visibility = Shown(_settings.ShowPrice);
        _changeLine.Text = StockFormat.Percent(quote?.ChangePercent) + "  ·  " + StockRanges.Key(range);
        _changeLine.Foreground = TokenBrushes.From(StockFormat.Tone(quote?.ChangePercent));
        _changeLine.Visibility = Shown(_settings.ShowChange);
        var stamp = series is { Points.Count: > 0 } ? series.Points[^1].Time : (DateTime?)null;
        if (quote?.Stale == true)
        {
            _asOf.Text = UiText.StockStaleQuote;
            _asOf.Visibility = Visibility.Visible;
        }
        else
        {
            _asOf.Text = stamp is DateTime time ? UiText.StockAsOf(time) : "";
            _asOf.Visibility = Shown(_settings.ShowAsOf && stamp is not null);
        }
        _hint.Visibility = Shown(_settings.ShowHint);
        var previous = series?.PreviousClose ?? quote?.PreviousClose;
        var showPrevious = _settings.ShowPrevious && StockRanges.ShowPreviousClose(range) && previous is not null;
        _previous.Visibility = showPrevious ? Visibility.Visible : Visibility.Collapsed;
        _previous.Text = showPrevious
            ? UiText.StockPreviousClose(StockFormat.Money(previous, currency))
            : "";
        _sample.Text = UiText.StockSample(range);
        _sample.Visibility = Shown(_settings.ShowSample);
        _chart.IsLight = _isLight;
        _chart.Intraday = StockRanges.Intraday(range);
        _chart.ShowPreviousClose = showPrevious;
        _chart.PreviousClose = previous;
        _chart.Rising = (quote?.ChangePercent ?? 0) >= 0;
        if (series is not null)
        {
            _chart.Points = series.Points;
            _chartStatus.Visibility = series.Points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _chartStatus.Text = series.Points.Count == 0 ? UiText.ClickRetry : "";
        }
    }

    private StackPanel BuildDetail()
    {
        _detailTitle = HeaderLabel("");
        _detailTitle.FontSize = 15;
        _detailTitle.FontWeight = FontWeights.SemiBold;
        var search = WidgetChrome.HeaderIcon(PhosphorIcon.MagnifyingGlass, ShowSearch);
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var open = WidgetChrome.HeaderIcon(PhosphorIcon.ArrowSquareOut, OpenQuote);
        var header = HeaderRow(_detailTitle, search, customize, open, WidgetChrome.HeaderClose(ClosePopup, 18));

        var refresh = new Border
        {
            Width = DockTokens.FlyoutControlHeight,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Cursor = Cursors.Hand,
            Child = new PhosphorGlyph(PhosphorIcon.ArrowsClockwise, 14)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        WidgetChrome.MarkQuiet(refresh);
        refresh.MouseLeftButtonUp += (_, e) =>
        {
            _ = _stocks.RefreshQuotesAsync();
            _ = LoadDetailAsync(true);
            e.Handled = true;
        };
        var rangeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 8)
        };
        rangeRow.Children.Add(_rangeBox);
        refresh.Margin = new Thickness(8, 0, 0, 0);
        rangeRow.Children.Add(refresh);

        _symbol = new TextBlock
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        };
        _name = WidgetCopy.Secondary();
        _name.FontSize = 12;
        _money = new TextBlock
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _changeLine = new TextBlock
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0),
            Tag = "tint"
        };
        var identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Children = { _symbol, _name } };
        var right = new StackPanel { Children = { _money, _changeLine } };
        identity.Children.Add(left);
        Grid.SetColumn(right, 1);
        identity.Children.Add(right);

        _hint = WidgetCopy.Secondary();
        _hint.Margin = new Thickness(0, 10, 0, 0);
        _asOf = WidgetCopy.Secondary();
        _asOf.Opacity = 0.5;

        var chartHost = new Grid { Height = DockTokens.StockChartHeight, Margin = new Thickness(0, 6, 0, 0) };
        chartHost.Children.Add(_chart);
        _chartStatus = new TextBlock
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 12,
            Opacity = 0.62,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed
        };
        _chartStatus.MouseLeftButtonUp += (_, e) =>
        {
            _ = LoadDetailAsync(true);
            e.Handled = true;
        };
        chartHost.Children.Add(_chartStatus);

        _previous = WidgetCopy.Secondary();
        _previous.Margin = new Thickness(0, 8, 0, 0);
        _sample = WidgetCopy.Secondary();
        _sample.Opacity = 0.5;

        return new StackPanel
        {
            Children = { header, rangeRow, identity, _hint, _asOf, chartHost, _previous, _sample }
        };
    }

    private StackPanel BuildSearch()
    {
        _searchTitle = HeaderLabel(UiText.StockChoose);
        _searchTitle.FontSize = 15;
        _searchTitle.FontWeight = FontWeights.SemiBold;
        _searchBack = WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, BackToDetail, 12, padding: new Thickness(0, 4, 4, 4));
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _searchBack, _searchTitle }
        };
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var header = HeaderRow(title, customize, WidgetChrome.HeaderClose(ClosePopup, 18));
        _searchCaption = HeaderLabel("");
        _searchCaption.Opacity = 0.62;
        _searchCaption.Margin = new Thickness(0, 8, 0, 6);
        var field = WidgetChrome.Field(_searchBox);
        _searchStatus = WidgetCopy.Secondary();
        _searchStatus.Margin = new Thickness(0, 6, 0, 0);
        var results = new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _results
        };
        return new StackPanel { Children = { header, _searchCaption, field, _searchStatus, results } };
    }

    private void ShowSearch()
    {
        _searchGen++;
        _searchTimer.Stop();
        _searchBox.Text = "";
        _results.Children.Clear();
        _searchStatus.Text = "";
        _searchBack.Visibility = _settings.Symbol is null ? Visibility.Collapsed : Visibility.Visible;
        _popup.StaysOpen = false;
        _detailPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Collapsed;
        _searchPage.Visibility = Visibility.Visible;
        WidgetChrome.ApplyTree(_searchPage, _isLight, WidgetChrome.ThemeBackdrop);
        Dispatcher.BeginInvoke(() => _searchBox.Focus());
    }

    private void BackToDetail()
    {
        if (_settings.Symbol is { } symbol)
        {
            ShowDetail(symbol);
        }
    }

    private void ShowDetail(StockSymbol symbol)
    {
        var changed = _detailSymbol?.Id != symbol.Id;
        _detailSymbol = symbol;
        _popup.StaysOpen = false;
        _searchPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Collapsed;
        _detailPage.Visibility = Visibility.Visible;
        _detailTitle.Text = DetailTitle(symbol);
        if (changed)
        {
            _chart.Points = [];
            _chartStatus.Visibility = Visibility.Visible;
            _chartStatus.Text = UiText.Loading;
        }

        PaintDetail();
        _ = LoadDetailAsync(false);
    }

    private async Task LoadDetailAsync(bool force)
    {
        var symbol = _detailSymbol;
        if (symbol is null)
        {
            return;
        }

        var range = StockRanges.Parse(_settings.Range);
        var gen = ++_seriesGen;
        if (force || _stocks.SeriesFor(symbol.Id, range) is null)
        {
            _chartStatus.Visibility = Visibility.Visible;
            _chartStatus.Text = UiText.Loading;
        }

        var series = await _stocks.LoadSeriesAsync(symbol, range, force);
        if (gen != _seriesGen || _detailSymbol?.Id != symbol.Id)
        {
            return;
        }

        if (series is null || series.Points.Count == 0)
        {
            _chart.Points = [];
            _chartStatus.Visibility = Visibility.Visible;
            _chartStatus.Text = UiText.ClickRetry;
        }

        PaintDetail();
    }

    private void EnsureSparkline()
    {
        var symbol = _settings.Symbol;
        if (!_settings.ShowSpark || symbol is null || _stocks.SeriesFor(symbol.Id, StockRange.Day) is not null)
        {
            return;
        }

        if (_sparkFor == symbol.Id && DateTime.UtcNow - _sparkAttempt < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _sparkFor = symbol.Id;
        _sparkAttempt = DateTime.UtcNow;
        _ = LoadSparkAsync(symbol);
    }

    private async Task LoadSparkAsync(StockSymbol symbol)
    {
        await _stocks.LoadSeriesAsync(symbol, StockRange.Day, false);
        Refresh();
    }

    private async Task RunSearchAsync()
    {
        var query = _searchBox.Text.Trim();
        var gen = ++_searchGen;
        _results.Children.Clear();
        if (query.Length == 0)
        {
            _searchStatus.Text = "";
            return;
        }

        _searchStatus.Text = UiText.Loading;
        var hits = await _stocks.SearchAsync(query);
        if (gen != _searchGen)
        {
            return;
        }

        _searchStatus.Text = hits.Count == 0 ? UiText.StockNoMatch : "";
        foreach (var hit in hits)
        {
            _results.Children.Add(SearchRow(hit));
        }

        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
    }

    private Border SearchRow(StockSymbol symbol)
    {
        var title = new TextBlock
        {
            Text = DisplayName(symbol),
            FontFamily = WidgetFonts.Ui,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        };
        var badge = MarketBadge(new TextBlock { Text = StockLinks.Badge(symbol.Market) });
        var code = WidgetCopy.Secondary();
        code.Text = symbol.Symbol;
        code.Margin = new Thickness(6, 0, 0, 0);
        code.VerticalAlignment = VerticalAlignment.Center;
        var meta = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 0),
            Children = { badge, code }
        };
        var board = StockLinks.BoardText(symbol);
        if (board is not null)
        {
            var chip = WidgetCopy.Secondary();
            chip.Text = board;
            chip.Margin = new Thickness(6, 0, 0, 0);
            chip.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(chip);
        }

        var row = new StackPanel { Margin = new Thickness(4, 6, 4, 6), Children = { title, meta } };
        var border = new Border { Cursor = Cursors.Hand, Child = row };
        border.MouseLeftButtonUp += (_, e) =>
        {
            Choose(symbol);
            e.Handled = true;
        };
        return border;
    }

    private void Choose(StockSymbol symbol)
    {
        _settings = _settings with { Symbol = symbol };
        _sparkFor = null;
        Persist();
        ShowDetail(symbol);
    }

    private void Persist()
    {
        _settings = StockWidgetSettings.Normalize(_settings);
        SettingsChanged?.Invoke(this, _settings);
        Refresh();
    }

    private void SyncRange()
    {
        _applying = true;
        WidgetChrome.SelectChoice(_rangeBox, _settings.Range);
        _applying = false;
    }

    private void OpenQuote()
    {
        if (_detailSymbol is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(StockLinks.Url(_detailSymbol)) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // The quote page is optional.
        }
    }

    private void ClosePopup()
    {
        _popup.StaysOpen = false;
        _popup.IsOpen = false;
    }

    private StackPanel BuildCustomize()
    {
        var backLabel = HeaderLabel(UiText.Back);
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, BackFromCustomize, 12),
                backLabel
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            BackFromCustomize();
            e.Handled = true;
        };
        _customizeTitle = HeaderLabel(UiText.Customize);
        _customizeTitle.Margin = new Thickness(8, 0, 0, 0);
        _customizeTitle.FontWeight = FontWeights.SemiBold;
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, _customizeTitle }
        }, WidgetChrome.HeaderClose(ClosePopup, 18));

        _islandCaption = SectionCaption();
        _quoteCaption = SectionCaption();
        _quoteCaption.Margin = new Thickness(0, 14, 0, 0);
        _nameBox = Flag(on => SetShow(on, settings => settings.ShowName, settings => settings with { ShowName = on }));
        _badgeBox = Flag(on => SetShow(on, settings => settings.ShowBadge, settings => settings with { ShowBadge = on }));
        _codeBox = Flag(on => SetShow(on, settings => settings.ShowCode, settings => settings with { ShowCode = on }));
        _boardBox = Flag(on => SetShow(on, settings => settings.ShowBoard, settings => settings with { ShowBoard = on }));
        _priceBox = Flag(on => SetShow(on, settings => settings.ShowPrice, settings => settings with { ShowPrice = on }));
        _sparkBox = Flag(on => SetShow(on, settings => settings.ShowSpark, settings => settings with { ShowSpark = on }));
        _changeBox = Flag(on => SetShow(on, settings => settings.ShowChange, settings => settings with { ShowChange = on }));
        _currencyBox = Flag(on => SetShow(on, settings => settings.ShowCurrency, settings => settings with { ShowCurrency = on }));
        _hintBox = Flag(on => SetShow(on, settings => settings.ShowHint, settings => settings with { ShowHint = on }));
        _asOfBox = Flag(on => SetShow(on, settings => settings.ShowAsOf, settings => settings with { ShowAsOf = on }));
        _previousBox = Flag(on => SetShow(on, settings => settings.ShowPrevious, settings => settings with { ShowPrevious = on }));
        _sampleBox = Flag(on => SetShow(on, settings => settings.ShowSample, settings => settings with { ShowSample = on }));

        var checks = new StackPanel
        {
            Children =
            {
                _islandCaption,
                _nameBox, _badgeBox, _codeBox, _boardBox, _priceBox, _sparkBox, _changeBox, _currencyBox,
                _quoteCaption,
                _hintBox, _asOfBox, _previousBox, _sampleBox
            }
        };
        var scroller = new ScrollViewer
        {
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = checks
        };
        return new StackPanel { Children = { header, scroller } };
    }

    private void ShowCustomize()
    {
        PaintCustomize();
        _popup.StaysOpen = true;
        _detailPage.Visibility = Visibility.Collapsed;
        _searchPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Visible;
    }

    private void BackFromCustomize()
    {
        if (_settings.Symbol is { } symbol)
        {
            ShowDetail(symbol);
        }
        else
        {
            ShowSearch();
        }
    }

    private void PaintCustomize()
    {
        _applying = true;
        _nameBox.IsChecked = _settings.ShowName;
        _badgeBox.IsChecked = _settings.ShowBadge;
        _codeBox.IsChecked = _settings.ShowCode;
        _boardBox.IsChecked = _settings.ShowBoard;
        _priceBox.IsChecked = _settings.ShowPrice;
        _sparkBox.IsChecked = _settings.ShowSpark;
        _changeBox.IsChecked = _settings.ShowChange;
        _currencyBox.IsChecked = _settings.ShowCurrency;
        _hintBox.IsChecked = _settings.ShowHint;
        _asOfBox.IsChecked = _settings.ShowAsOf;
        _previousBox.IsChecked = _settings.ShowPrevious;
        _sampleBox.IsChecked = _settings.ShowSample;
        _applying = false;
    }

    private void SetShow(bool on, Func<StockWidgetSettings, bool> read, Func<StockWidgetSettings, StockWidgetSettings> write)
    {
        if (_applying || read(_settings) == on)
        {
            return;
        }

        _settings = write(_settings);
        Persist();
    }

    private static string DisplayName(StockSymbol symbol) =>
        string.IsNullOrWhiteSpace(symbol.Name) ? symbol.Symbol : symbol.Name;

    private string DetailTitle(StockSymbol symbol)
    {
        if (_settings.ShowName && !string.IsNullOrWhiteSpace(symbol.Name))
        {
            return symbol.Name;
        }

        if (_settings.ShowCode)
        {
            return symbol.Symbol;
        }

        return BuiltInWidgets.DisplayName(BuiltInWidgets.Stocks);
    }

    private static Visibility Shown(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private CheckBox Flag(Action<bool> set)
    {
        var box = new CheckBox
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0)
        };
        box.Checked += (_, _) => set(true);
        box.Unchecked += (_, _) => set(false);
        return box;
    }

    private static TextBlock SectionCaption()
    {
        var caption = HeaderLabel("");
        caption.Opacity = 0.62;
        caption.Margin = new Thickness(0, 4, 0, 2);
        return caption;
    }

    private static Border MarketBadge(TextBlock label)
    {
        label.FontFamily = WidgetFonts.Ui;
        label.FontSize = 9;
        label.FontWeight = FontWeights.SemiBold;
        label.Foreground = TokenBrushes.From(DockTokens.Dark.Text);
        label.Tag = "tint";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextWrapping = TextWrapping.NoWrap;
        return new Border
        {
            MinWidth = 14,
            Height = 14,
            CornerRadius = new CornerRadius(DockTokens.StockMarkRadius),
            Background = TokenBrushes.From(DockTokens.Network),
            Padding = new Thickness(3, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };
    }

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
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
