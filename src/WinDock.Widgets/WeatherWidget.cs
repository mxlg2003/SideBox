using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class WeatherWidget : UserControl, IWidgetOpenable, IWidgetModeAware, IDockEdgeAware, IWeatherConfigurable, IWidgetThemeAware, ILocalizable, IWidgetDetachable, IIslandLayoutAware
{
    public event EventHandler? IslandLayoutChanged;
    private static readonly FontFamily WeatherGlyphFont = new("Segoe UI Emoji, Segoe UI Symbol, Segoe MDL2 Assets");

    private readonly IWeatherProvider _provider;
    private WeatherSettings _settings = WeatherSettings.Default;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private bool _side;
    private bool _isLight;

    // Tile controls
    private readonly Border _tile;
    private readonly Grid _tileHost = new();

    // Compact tile controls (single line minimal badge)
    private readonly StackPanel _compactPanel = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _compactGlyph = new()
    {
        FontFamily = WeatherGlyphFont,
        FontSize = 15,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0)
    };
    private readonly TextBlock _compactTemp = WidgetCopy.Primary();
    private readonly TextBlock _compactStatus = WidgetCopy.Secondary();

    // Expanded tile controls (rich 2-line tile)
    private readonly StackPanel _expandedPanel = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _expandedGlyph = new()
    {
        FontFamily = WeatherGlyphFont,
        FontSize = 20,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0)
    };
    private readonly TextBlock _expandedTemp = WidgetCopy.Primary();
    private readonly TextBlock _expandedRange = WidgetCopy.Secondary();
    private readonly TextBlock _expandedCityStatus = WidgetCopy.Secondary();
    private readonly Border _expandedAqiBadge = new()
    {
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(5, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _expandedAqiText = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 9,
        FontWeight = FontWeights.SemiBold
    };

    // Flyout controls
    private readonly Popup _popup;
    private readonly Border _panel;
    private readonly TextBlock _cityLabel = WidgetCopy.Primary();
    private readonly TextBlock _badgeText = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 10,
        Opacity = 0.8
    };
    private readonly Border _badgeBorder = new()
    {
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(5, 1, 5, 1),
        Margin = new Thickness(6, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };

    // Hero section
    private readonly TextBlock _bigTemp = WidgetCopy.Primary();
    private readonly TextBlock _conditionText = WidgetCopy.Primary();
    private readonly TextBlock _heroRangeText = WidgetCopy.Secondary();
    private readonly TextBlock _heroGlyph = new()
    {
        FontFamily = WeatherGlyphFont,
        FontSize = 38,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right
    };

    // Metric 4-cards
    private readonly TextBlock _cardFeelsLikeVal = WidgetCopy.Primary();
    private readonly TextBlock _cardHumidityVal = WidgetCopy.Primary();
    private readonly TextBlock _cardWindVal = WidgetCopy.Primary();
    private readonly TextBlock _cardAqiVal = WidgetCopy.Primary();
    private readonly List<Border> _metricCardBorders = [];
    private readonly List<TextBlock> _metricCardLabels = [];

    // Hourly forecast
    private readonly ScrollViewer _hourlyScroll;
    private readonly StackPanel _hourlyPanel = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _hourlyCaption = Caption(UiText.WeatherHourly);
    private readonly Border _hourlyPrevBtn;
    private readonly Border _hourlyNextBtn;
    private readonly PhosphorGlyph _hourlyPrevGlyph = new(PhosphorIcon.CaretLeft, 11)
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly PhosphorGlyph _hourlyNextGlyph = new(PhosphorIcon.CaretRight, 11)
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private DispatcherTimer? _scrollAnimTimer;
    private double _targetScrollOffset;
    private Point _dragStartPoint;
    private double _dragStartOffset;
    private bool _isDraggingHourly;
    private bool _isMouseDownHourly;
    private readonly TextBlock _hourlySubCaption = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 10,
        Opacity = 0.5,
        VerticalAlignment = VerticalAlignment.Center
    };

    // Daily forecast
    private readonly StackPanel _dailyPanel = new();
    private readonly TextBlock _dailyCaption = Caption(UiText.Weather7Days);

    // City edit section & autocomplete suggestions
    private readonly TextBox _cityBox = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        Padding = new Thickness(8, 0, 8, 0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly StackPanel _citySuggestionsPanel = new();
    private readonly ScrollViewer _citySuggestionsScroll;
    private readonly Border _citySuggestionsContainer;
    private readonly Border _cityEditSection;
    private CancellationTokenSource? _searchCts;
    private List<WeatherCityItem> _suggestions = [];
    private int _selectedSuggestionIndex = -1;

    public event EventHandler<WeatherSettings>? SettingsChanged;

    public WeatherWidget(IWeatherProvider provider, WidgetDensity density = WidgetDensity.Expanded)
    {
        _provider = provider;
        _density = density;

        // Build Compact Tile
        _compactTemp.FontSize = 14;
        _compactTemp.FontWeight = FontWeights.SemiBold;
        _compactStatus.FontSize = 11;
        _compactStatus.Margin = new Thickness(4, 0, 0, 0);
        _compactPanel.Children.Add(_compactGlyph);
        _compactPanel.Children.Add(_compactTemp);
        _compactPanel.Children.Add(_compactStatus);

        // Build Expanded Tile
        _expandedTemp.FontSize = 15;
        _expandedTemp.FontWeight = FontWeights.SemiBold;
        _expandedRange.FontSize = 10;
        _expandedRange.Margin = new Thickness(5, 1, 0, 0);
        _expandedRange.VerticalAlignment = VerticalAlignment.Center;
        _expandedCityStatus.FontSize = 11;
        _expandedAqiBadge.Child = _expandedAqiText;

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _expandedTemp, _expandedRange }
        };
        var bottomRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0),
            Children = { _expandedCityStatus, _expandedAqiBadge }
        };
        var rightStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { topRow, bottomRow }
        };

        _expandedPanel.Children.Add(_expandedGlyph);
        _expandedPanel.Children.Add(rightStack);

        _tileHost.Children.Add(_compactPanel);
        _tileHost.Children.Add(_expandedPanel);

        _tile = WidgetChrome.Wrap(_tileHost, minWidth: 84);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        // City suggestions dropdown scroll
        _citySuggestionsScroll = new ScrollViewer
        {
            MaxHeight = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _citySuggestionsPanel
        };
        WidgetChrome.PaintScrollViewer(_citySuggestionsScroll, _isLight);
        _citySuggestionsContainer = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6),
            Visibility = Visibility.Collapsed,
            Child = _citySuggestionsScroll
        };

        // City box input events
        _cityBox.TextChanged += (_, _) => OnCityInputChanged();
        _cityBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (_selectedSuggestionIndex >= 0 && _selectedSuggestionIndex < _suggestions.Count)
                {
                    SelectCity(_suggestions[_selectedSuggestionIndex].Name);
                }
                else if (_suggestions.Count > 0)
                {
                    SelectCity(_suggestions[0].Name);
                }
                else if (!string.IsNullOrWhiteSpace(_cityBox.Text))
                {
                    SelectCity(_cityBox.Text.Trim());
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_suggestions.Count > 0)
                {
                    _selectedSuggestionIndex = Math.Min(_suggestions.Count - 1, _selectedSuggestionIndex + 1);
                    HighlightSuggestion();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (_suggestions.Count > 0)
                {
                    _selectedSuggestionIndex = Math.Max(0, _selectedSuggestionIndex - 1);
                    HighlightSuggestion();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                _citySuggestionsContainer.Visibility = Visibility.Collapsed;
                if (_cityEditSection != null)
                {
                    _cityEditSection.Visibility = Visibility.Collapsed;
                }
                e.Handled = true;
            }
        };

        // City edit popup row actions
        var btnSaveCity = ActionButton(UiText.Save, () =>
        {
            SelectCity(_cityBox.Text.Trim());
        });

        var btnAutoCity = ActionButton(UiText.WeatherCityAuto, () =>
        {
            _settings = _settings with { City = "", AutoLocate = true };
            _cityBox.Text = "";
            _citySuggestionsContainer.Visibility = Visibility.Collapsed;
            if (_cityEditSection != null)
            {
                _cityEditSection.Visibility = Visibility.Collapsed;
            }
            SettingsChanged?.Invoke(this, _settings);
            _ = _provider.RefreshAsync(_settings, force: true);
        });

        var cityField = WidgetChrome.Field(_cityBox);
        cityField.Margin = new Thickness(0, 6, 0, 6);
        var cityActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { btnAutoCity, btnSaveCity }
        };

        _cityEditSection = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 4, 0, 8),
            Visibility = Visibility.Collapsed,
            Child = new StackPanel
            {
                Children =
                {
                    Caption(UiText.WeatherEnterCity),
                    cityField,
                    _citySuggestionsContainer,
                    cityActions
                }
            }
        };

        _hourlyScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 4, 0, 10),
            Content = _hourlyPanel,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            PanningMode = PanningMode.HorizontalOnly,
            PanningDeceleration = 0.001,
            PanningRatio = 1.0,
            IsManipulationEnabled = true
        };

        _hourlyPrevBtn = CreateHourlyNavButton(_hourlyPrevGlyph, () => AnimateScrollTo(_hourlyScroll.HorizontalOffset - 180));
        _hourlyPrevBtn.ToolTip = UiText.WeatherPrevHours;
        _hourlyPrevBtn.Opacity = 0.25;
        _hourlyPrevBtn.IsEnabled = false;

        _hourlyNextBtn = CreateHourlyNavButton(_hourlyNextGlyph, () => AnimateScrollTo(_hourlyScroll.HorizontalOffset + 180));
        _hourlyNextBtn.ToolTip = UiText.WeatherNextHours;
        _hourlyNextBtn.Opacity = 0.85;
        // Enable smooth horizontal scrolling via vertical mouse wheel
        _hourlyScroll.PreviewMouseWheel += (_, e) =>
        {
            _scrollAnimTimer?.Stop();
            _hourlyScroll.ScrollToHorizontalOffset(_hourlyScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        };
        _hourlyScroll.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _scrollAnimTimer?.Stop();
            _isMouseDownHourly = true;
            _isDraggingHourly = false;
            _dragStartPoint = e.GetPosition(_hourlyScroll);
            _dragStartOffset = _hourlyScroll.HorizontalOffset;
            _hourlyScroll.CaptureMouse();
        };
        _hourlyScroll.PreviewMouseMove += (_, e) =>
        {
            if (!_isMouseDownHourly) return;
            var currentPoint = e.GetPosition(_hourlyScroll);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            if (!_isDraggingHourly && Math.Abs(deltaX) > 3)
            {
                _isDraggingHourly = true;
            }
            if (_isDraggingHourly)
            {
                _hourlyScroll.ScrollToHorizontalOffset(_dragStartOffset - deltaX);
            }
        };
        _hourlyScroll.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_hourlyScroll.IsMouseCaptured)
            {
                _hourlyScroll.ReleaseMouseCapture();
            }
            _isMouseDownHourly = false;
            if (_isDraggingHourly)
            {
                _isDraggingHourly = false;
                e.Handled = true;
            }
        };
        _hourlyScroll.LostMouseCapture += (_, _) =>
        {
            _isMouseDownHourly = false;
            _isDraggingHourly = false;
        };
        _hourlyScroll.ScrollChanged += (_, _) => UpdateHourlyNavButtons();

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8
        };
        WidgetFlyout.Attach(_popup);

        _panel = BuildPanel();
        _popup.Child = WidgetChrome.Shadow(_panel);

        _provider.Changed += OnProviderChanged;
        Content = new Grid { Children = { _tile, _popup } };

        ApplyDensity(_density);
        ApplyTheme(false);
        _ = _provider.RefreshAsync(_settings, force: false);
    }

    private void OnProviderChanged(object? sender, EventArgs e) => Dispatcher.Invoke(RenderSnapshot);

    public void Detach()
    {
        _provider.Changed -= OnProviderChanged;
        _scrollAnimTimer?.Stop();
        _scrollAnimTimer = null;
        _popup.IsOpen = false;
        _searchCts?.Cancel();
    }

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        ApplyDensity(_density);
    }

    public void ApplyDensity(WidgetDensity density)
    {
        var changed = _density != density;
        _density = density;
        var compact = density == WidgetDensity.Compact;
        _compactPanel.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        _expandedPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _compactPanel.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _compactPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _expandedPanel.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _expandedPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _compactGlyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 4, 0);
        _compactGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _compactTemp.Margin = new Thickness(0);
        _compactTemp.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _compactStatus.Margin = _side ? new Thickness(0, 1, 0, 0) : new Thickness(4, 0, 0, 0);
        _compactStatus.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _compactStatus.TextWrapping = _side ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _compactStatus.MaxWidth = _side ? DockTokens.SideTileWidth - 16 : double.PositiveInfinity;
        _expandedGlyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _expandedGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _expandedTemp.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _expandedCityStatus.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _expandedCityStatus.TextWrapping = _side ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _expandedCityStatus.MaxWidth = _side ? DockTokens.SideTileWidth - 16 : double.PositiveInfinity;
        if (_expandedPanel.Children.Count > 1
            && _expandedPanel.Children[1] is StackPanel detail
            && detail.Children.Count > 0
            && detail.Children[0] is StackPanel tempRow)
        {
            detail.HorizontalAlignment = HorizontalAlignment.Center;
            tempRow.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
            tempRow.HorizontalAlignment = HorizontalAlignment.Center;
            _expandedRange.Margin = _side ? new Thickness(0, 1, 0, 0) : new Thickness(5, 1, 0, 0);
            _expandedRange.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            if (detail.Children.Count > 1 && detail.Children[1] is StackPanel cityRow)
            {
                cityRow.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
                cityRow.HorizontalAlignment = HorizontalAlignment.Center;
                _expandedAqiBadge.Margin = _side ? new Thickness(0, 2, 0, 0) : new Thickness(5, 0, 0, 0);
                _expandedAqiBadge.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            }
        }
        foreach (UIElement child in _expandedPanel.Children)
        {
            if (child is FrameworkElement element)
            {
                element.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                element.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        _expandedTemp.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _expandedCityStatus.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _compactTemp.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _compactStatus.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        if (!_side)
        {
            _tile.MinWidth = compact ? 52 : 84;
        }

        WidgetChrome.FitEdge(_tile, _side, compact ? 52 : 84);
        RenderSnapshot();
        _tile.InvalidateMeasure();
        InvalidateMeasure();
        if (changed)
        {
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplySettings(WeatherSettings settings)
    {
        _settings = WeatherSettings.Normalize(settings);
        _cityBox.Text = _settings.City;
        _ = _provider.RefreshAsync(_settings, force: false);
        RenderSnapshot();
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        var textBrush = TokenBrushes.From(isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        var textMutedBrush = TokenBrushes.From(isLight ? 0x99000000u : 0xA6FFFFFFu);

        Foreground = textBrush;
        TextElement.SetForeground(_panel, textBrush);

        _dailyCaption.Foreground = textMutedBrush;
        _hourlyCaption.Foreground = textMutedBrush;
        _hourlySubCaption.Foreground = textMutedBrush;
        _cityLabel.Foreground = textBrush;
        _bigTemp.Foreground = textBrush;
        _conditionText.Foreground = textBrush;
        _heroRangeText.Foreground = textMutedBrush;
        _cardFeelsLikeVal.Foreground = textBrush;
        _cardHumidityVal.Foreground = textBrush;
        _cardWindVal.Foreground = textBrush;

        foreach (var label in _metricCardLabels)
        {
            label.Foreground = textMutedBrush;
        }

        var cardFill = TokenBrushes.From(isLight ? 0x0A000000u : 0x12FFFFFFu);
        var cardStroke = TokenBrushes.From(isLight ? 0x12000000u : 0x18FFFFFFu);

        foreach (var card in _metricCardBorders)
        {
            card.Background = cardFill;
            card.BorderBrush = cardStroke;
        }

        _cityEditSection.Background = TokenBrushes.From(isLight ? 0x0E000000u : 0x22000000u);
        _badgeBorder.Background = TokenBrushes.From(isLight ? 0x14000000u : 0x22FFFFFFu);
        _badgeText.Foreground = textMutedBrush;

        _citySuggestionsContainer.Background = TokenBrushes.From(isLight ? 0xF8FFFFFFu : 0xF21C1C26u);
        _citySuggestionsContainer.BorderBrush = TokenBrushes.From(isLight ? 0x24000000u : 0x30FFFFFFu);
        WidgetChrome.PaintScrollViewer(_citySuggestionsScroll, isLight);

        WidgetChrome.ApplyTheme(_panel, isLight);

        _hourlyPrevGlyph.Foreground = textBrush;
        _hourlyNextGlyph.Foreground = textBrush;
        UpdateHourlyNavButtons();

        RenderSnapshot();
    }

    public void ApplyLocale()
    {
        _hourlyCaption.Text = UiText.WeatherHourly;
        _dailyCaption.Text = UiText.Weather7Days;
        _hourlyPrevBtn.ToolTip = UiText.WeatherPrevHours;
        _hourlyNextBtn.ToolTip = UiText.WeatherNextHours;
        RenderSnapshot();
    }

    public void Open()
    {
        _popup.IsOpen = true;
        _hourlyScroll.ScrollToHorizontalOffset(0);
        UpdateHourlyNavButtons();
        _ = _provider.RefreshAsync(_settings, force: false);
    }

    private void ClosePopup()
    {
        _scrollAnimTimer?.Stop();
        _scrollAnimTimer = null;
        _popup.IsOpen = false;
    }

    private Border CreateHourlyNavButton(PhosphorGlyph glyph, Action click)
    {
        var btn = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Thickness(2, 0, 0, 0),
            Child = glyph
        };
        btn.MouseEnter += (_, _) =>
        {
            if (btn.IsEnabled)
            {
                btn.Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x24FFFFFFu);
            }
        };
        btn.MouseLeave += (_, _) =>
        {
            btn.Background = Brushes.Transparent;
        };
        btn.MouseLeftButtonUp += (_, e) =>
        {
            if (btn.IsEnabled)
            {
                click();
                e.Handled = true;
            }
        };
        return btn;
    }

    private void AnimateScrollTo(double targetOffset)
    {
        _targetScrollOffset = Math.Clamp(targetOffset, 0, Math.Max(0, _hourlyScroll.ScrollableWidth));
        _scrollAnimTimer?.Stop();
        _scrollAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollAnimTimer.Tick += (_, _) =>
        {
            var current = _hourlyScroll.HorizontalOffset;
            var diff = _targetScrollOffset - current;
            if (Math.Abs(diff) < 1.5)
            {
                _hourlyScroll.ScrollToHorizontalOffset(_targetScrollOffset);
                _scrollAnimTimer?.Stop();
                _scrollAnimTimer = null;
            }
            else
            {
                _hourlyScroll.ScrollToHorizontalOffset(current + diff * 0.35);
            }
        };
        _scrollAnimTimer.Start();
    }

    private void UpdateHourlyNavButtons()
    {
        var offset = _hourlyScroll.HorizontalOffset;
        var max = _hourlyScroll.ScrollableWidth;

        var canPrev = offset > 1;
        var canNext = max > 1 && offset < max - 1;

        _hourlyPrevBtn.IsEnabled = canPrev;
        _hourlyPrevBtn.Opacity = canPrev ? 0.85 : 0.25;
        _hourlyPrevBtn.Cursor = canPrev ? Cursors.Hand : Cursors.Arrow;

        _hourlyNextBtn.IsEnabled = canNext;
        _hourlyNextBtn.Opacity = canNext ? 0.85 : 0.25;
        _hourlyNextBtn.Cursor = canNext ? Cursors.Hand : Cursors.Arrow;
    }

    private void OnCityInputChanged()
    {
        var query = _cityBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            _suggestions.Clear();
            _citySuggestionsPanel.Children.Clear();
            _citySuggestionsContainer.Visibility = Visibility.Collapsed;
            _selectedSuggestionIndex = -1;
            return;
        }

        // 1. Instant local search from embedded cities database
        var localResults = WeatherCities.Search(query, 8);
        UpdateSuggestions(localResults);

        // 2. Debounced online search via Open-Meteo
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Delay(300, token).ContinueWith(async t =>
        {
            if (t.IsCanceled || token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var remoteResults = await _provider.SearchCitiesAsync(query, token);
                if (!token.IsCancellationRequested && remoteResults.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!token.IsCancellationRequested && _cityBox.Text.Trim().Equals(query, StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateSuggestions(remoteResults);
                        }
                    });
                }
            }
            catch
            {
                // Ignore cancel or network errors
            }
        }, TaskScheduler.Default);
    }

    private void UpdateSuggestions(IReadOnlyList<WeatherCityItem> items)
    {
        _suggestions = items.ToList();
        _selectedSuggestionIndex = -1;
        _citySuggestionsPanel.Children.Clear();

        if (_suggestions.Count == 0)
        {
            _citySuggestionsContainer.Visibility = Visibility.Collapsed;
            return;
        }

        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        var textMutedBrush = TokenBrushes.From(_isLight ? 0x99000000u : 0xA6FFFFFFu);

        for (var i = 0; i < _suggestions.Count; i++)
        {
            var item = _suggestions[i];
            var index = i;

            var locIcon = new PhosphorGlyph(PhosphorIcon.MapPin, 10)
            {
                Foreground = textMutedBrush,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "tint"
            };

            var nameText = new TextBlock
            {
                Text = item.Name,
                FontFamily = WidgetFonts.Ui,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            var adminText = new TextBlock
            {
                Text = item.DisplayLocation,
                FontFamily = WidgetFonts.Ui,
                FontSize = 11,
                Foreground = textMutedBrush,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center
            };

            var rowGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(locIcon, 0);
            rowGrid.Children.Add(locIcon);

            Grid.SetColumn(nameText, 1);
            rowGrid.Children.Add(nameText);

            Grid.SetColumn(adminText, 2);
            rowGrid.Children.Add(adminText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = rowGrid
            };

            itemBorder.MouseEnter += (_, _) =>
            {
                _selectedSuggestionIndex = index;
                HighlightSuggestion();
            };
            itemBorder.MouseLeave += (_, _) =>
            {
                itemBorder.Background = Brushes.Transparent;
            };
            itemBorder.MouseLeftButtonUp += (_, e) =>
            {
                SelectCity(item.Name);
                e.Handled = true;
            };

            _citySuggestionsPanel.Children.Add(itemBorder);
        }

        _citySuggestionsContainer.Visibility = Visibility.Visible;
    }

    private void HighlightSuggestion()
    {
        var hoverBg = TokenBrushes.From(_isLight ? 0x18000000u : 0x2AFFFFFFu);
        for (var i = 0; i < _citySuggestionsPanel.Children.Count; i++)
        {
            if (_citySuggestionsPanel.Children[i] is Border border)
            {
                border.Background = i == _selectedSuggestionIndex ? hoverBg : Brushes.Transparent;
            }
        }
    }

    private void SelectCity(string cityName)
    {
        _settings = _settings with { City = cityName.Trim(), AutoLocate = false };
        _cityBox.Text = _settings.City;
        _citySuggestionsContainer.Visibility = Visibility.Collapsed;
        if (_cityEditSection != null)
        {
            _cityEditSection.Visibility = Visibility.Collapsed;
        }
        SettingsChanged?.Invoke(this, _settings);
        _ = _provider.RefreshAsync(_settings, force: true);
    }

    private Border BuildPanel()
    {
        _cityLabel.FontSize = 16;
        _cityLabel.FontWeight = FontWeights.SemiBold;
        _bigTemp.FontSize = 38;
        _bigTemp.FontWeight = FontWeights.Bold;
        _conditionText.FontSize = 14;
        _conditionText.FontWeight = FontWeights.SemiBold;
        _heroRangeText.FontSize = 12;
        _heroRangeText.Opacity = 0.7;
        _heroRangeText.Margin = new Thickness(8, 2, 0, 0);

        _badgeBorder.Child = _badgeText;

        var locIcon = new PhosphorGlyph(PhosphorIcon.MapPin, 13)
        {
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var cityTitleGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { locIcon, _cityLabel, _badgeBorder }
        };

        var refreshBtn = WidgetChrome.HeaderIcon(PhosphorIcon.ArrowsClockwise, () => _ = _provider.RefreshAsync(_settings, force: true), opacity: 0.7);
        refreshBtn.ToolTip = UiText.Refresh;
        var editCityBtn = WidgetChrome.HeaderIcon(PhosphorIcon.PencilSimple, () =>
        {
            _cityEditSection.Visibility = _cityEditSection.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (_cityEditSection.Visibility == Visibility.Visible)
            {
                _cityBox.Focus();
                _cityBox.SelectAll();
                OnCityInputChanged();
            }
            else
            {
                _citySuggestionsContainer.Visibility = Visibility.Collapsed;
            }
        }, opacity: 0.7);
        editCityBtn.ToolTip = UiText.WeatherChangeCity;
        var closeBtn = WidgetChrome.HeaderClose(ClosePopup, opacity: 0.7);

        var header = HeaderRow(cityTitleGroup, editCityBtn, refreshBtn, closeBtn);

        // Hero Weather Card
        var heroGrid = new Grid { Margin = new Thickness(0, 6, 0, 8) };
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heroLeft = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _bigTemp,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Children = { _conditionText, _heroRangeText }
                }
            }
        };
        Grid.SetColumn(heroLeft, 0);
        Grid.SetColumn(_heroGlyph, 1);
        heroGrid.Children.Add(heroLeft);
        heroGrid.Children.Add(_heroGlyph);

        // 4 Metric cards in a 2x2 grid
        var metricsGrid = new UniformGrid
        {
            Columns = 2,
            Margin = new Thickness(0, 4, 0, 8)
        };
        metricsGrid.Children.Add(CreateMetricCard(PhosphorIcon.Thermometer, UiText.WeatherFeelsLike, _cardFeelsLikeVal));
        metricsGrid.Children.Add(CreateMetricCard(PhosphorIcon.Drop, UiText.WeatherHumidity, _cardHumidityVal));
        metricsGrid.Children.Add(CreateMetricCard(PhosphorIcon.Wind, UiText.WeatherWind, _cardWindVal));
        metricsGrid.Children.Add(CreateMetricCard(PhosphorIcon.Leaf, UiText.WeatherAqi, _cardAqiVal));

        // Hourly section header
        var hourlyHeader = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        _hourlySubCaption.Text = "24h";
        _hourlySubCaption.Margin = new Thickness(6, 0, 0, 0);
        var hourlyRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _hourlyPrevBtn, _hourlyNextBtn, _hourlySubCaption }
        };
        DockPanel.SetDock(hourlyRight, Dock.Right);
        hourlyHeader.Children.Add(hourlyRight);
        hourlyHeader.Children.Add(_hourlyCaption);

        var body = new StackPanel
        {
            Width = 330,
            Children =
            {
                header,
                _cityEditSection,
                heroGrid,
                metricsGrid,
                hourlyHeader,
                _hourlyScroll,
                _dailyCaption,
                _dailyPanel
            }
        };

        var border = new Border
        {
            Tag = WidgetChrome.FlyoutTag,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 14, 16, 16),
            SnapsToDevicePixels = true,
            Child = body
        };
        WidgetChrome.Round(border, DockTokens.IslandCornerRadius);
        WidgetChrome.ApplyTheme(border, _isLight);
        return border;
    }

    private Border CreateMetricCard(string glyph, string label, TextBlock valBlock)
    {
        var icon = new PhosphorGlyph(glyph, 11)
        {
            Opacity = 0.65,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var title = new TextBlock
        {
            Text = label,
            FontFamily = WidgetFonts.Ui,
            FontSize = 11,
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Center
        };
        _metricCardLabels.Add(title);

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, title }
        };

        valBlock.FontSize = 13;
        valBlock.FontWeight = FontWeights.SemiBold;
        valBlock.Margin = new Thickness(0, 3, 0, 0);

        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(2),
            Child = new StackPanel { Children = { titleRow, valBlock } }
        };
        _metricCardBorders.Add(card);
        return card;
    }

    private void RenderSnapshot()
    {
        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        var textMutedBrush = TokenBrushes.From(_isLight ? 0x99000000u : 0xA6FFFFFFu);

        var snapshot = _provider.SnapshotFor(_settings);
        if (snapshot == null || snapshot.Error != WeatherError.None)
        {
            var status = snapshot is null
                ? UiText.WeatherRefreshing
                : string.IsNullOrWhiteSpace(snapshot.ConditionText)
                    ? WeatherFormat.ErrorText(snapshot.Error)
                    : snapshot.ConditionText;
            var city = string.IsNullOrEmpty(_settings.City)
                ? (_settings.AutoLocate ? UiText.WeatherCityAuto : UiText.WeatherNeedCity)
                : _settings.City;
            var compactStatus = snapshot?.Error switch
            {
                WeatherError.NeedCity => UiText.WeatherNoCity,
                WeatherError.Network => UiText.WeatherUnavailableShort,
                _ => status
            };
            _compactGlyph.Text = "\u2600";
            _compactTemp.Text = "--°";
            _compactTemp.Foreground = textBrush;
            _compactStatus.Text = compactStatus;
            _compactStatus.Foreground = textMutedBrush;

            _expandedGlyph.Text = "\u2600";
            _expandedTemp.Text = "--°";
            _expandedTemp.Foreground = textBrush;
            _expandedRange.Text = "";
            _expandedCityStatus.Text = compactStatus;
            _expandedCityStatus.Foreground = textMutedBrush;
            _expandedAqiBadge.Visibility = Visibility.Collapsed;

            _cityLabel.Text = city;
            _cityLabel.Foreground = textBrush;
            _badgeText.Text = _settings.AutoLocate ? UiText.WeatherCityAuto : "";
            _badgeText.Foreground = textMutedBrush;
            _badgeBorder.Visibility = _settings.AutoLocate || snapshot?.Error == WeatherError.NeedCity ? Visibility.Visible : Visibility.Collapsed;

            _bigTemp.Text = "--°";
            _bigTemp.Foreground = textBrush;
            _conditionText.Text = status;
            _conditionText.Foreground = textBrush;
            _heroRangeText.Text = "";
            _heroGlyph.Text = "\u2600";

            _cardFeelsLikeVal.Text = "--";
            _cardFeelsLikeVal.Foreground = textBrush;
            _cardHumidityVal.Text = "--";
            _cardHumidityVal.Foreground = textBrush;
            _cardWindVal.Text = "--";
            _cardWindVal.Foreground = textBrush;
            _cardAqiVal.Text = "--";
            _cardAqiVal.Foreground = textBrush;
            return;
        }

        var fahr = _settings.UseFahrenheit;
        var tempStr = WeatherFormat.Temperature(snapshot.TemperatureC, fahr);
        var glyph = WeatherGlyphMapper.ConditionGlyph(snapshot.Condition);
        var condColor = WeatherGlyphMapper.ConditionColor(snapshot.Condition);
        var condBrush = TokenBrushes.From(condColor);

        // Render Compact Tile
        _compactGlyph.Text = glyph;
        _compactGlyph.Foreground = condBrush;
        _compactTemp.Text = tempStr;
        _compactTemp.Foreground = textBrush;
        _compactStatus.Text = snapshot.ConditionText;
        _compactStatus.Foreground = textMutedBrush;

        // Render Expanded Tile
        _expandedGlyph.Text = glyph;
        _expandedGlyph.Foreground = condBrush;
        _expandedTemp.Text = tempStr;
        _expandedTemp.Foreground = textBrush;

        var todayForecast = snapshot.Daily.FirstOrDefault(d => d.Date == DateOnly.FromDateTime(DateTime.Today));
        if (todayForecast != null)
        {
            _expandedRange.Text = WeatherFormat.Range(todayForecast.TempMinC, todayForecast.TempMaxC, fahr);
            _heroRangeText.Text = $"{WeatherFormat.Temperature(todayForecast.TempMaxC, fahr)} / {WeatherFormat.Temperature(todayForecast.TempMinC, fahr)}";
        }
        else
        {
            _expandedRange.Text = "";
            _heroRangeText.Text = "";
        }
        _expandedRange.Foreground = textMutedBrush;
        _heroRangeText.Foreground = textMutedBrush;

        _expandedCityStatus.Text = snapshot.IsOffline ? $"{snapshot.ConditionText} ({UiText.WeatherOffline})" : $"{snapshot.City} {snapshot.ConditionText}";
        _expandedCityStatus.Foreground = textMutedBrush;

        // AQI Badge in Expanded Dock
        var aqiColor = WeatherFormat.AqiColor(snapshot.AirQualityIndex);
        if (snapshot.AirQualityIndex < 0)
        {
            _expandedAqiBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            _expandedAqiText.Text = $"{WeatherFormat.AqiLevel(snapshot.AirQualityIndex)} {snapshot.AirQualityIndex}";
            _expandedAqiBadge.Background = TokenBrushes.From((aqiColor & 0x00FFFFFFu) | 0x2E000000u);
            _expandedAqiText.Foreground = TokenBrushes.From(aqiColor);
            _expandedAqiBadge.Visibility = Visibility.Visible;
        }

        // Dock Tooltip
        _tile.ToolTip = $"{snapshot.City} · {snapshot.ConditionText} · {tempStr} ({UiText.WeatherFeelsLike} {WeatherFormat.Temperature(snapshot.FeelsLikeC, fahr)})";

        // Render Flyout Header & Hero
        _cityLabel.Text = snapshot.City + (snapshot.IsOffline ? $" ({UiText.WeatherOffline})" : "");
        _cityLabel.Foreground = textBrush;
        _badgeText.Text = string.IsNullOrEmpty(_settings.City) ? UiText.WeatherCityAuto : snapshot.UpdatedTime.ToString("HH:mm");
        _badgeText.Foreground = textMutedBrush;
        _badgeBorder.Visibility = Visibility.Visible;

        _bigTemp.Text = WeatherFormat.TemperatureFull(snapshot.TemperatureC, fahr);
        _bigTemp.Foreground = textBrush;
        _conditionText.Text = snapshot.ConditionText;
        _conditionText.Foreground = textBrush;
        _heroGlyph.Text = glyph;
        _heroGlyph.Foreground = condBrush;

        // Render Metric Cards
        _cardFeelsLikeVal.Text = WeatherFormat.Temperature(snapshot.FeelsLikeC, fahr);
        _cardFeelsLikeVal.Foreground = textBrush;
        _cardHumidityVal.Text = $"{snapshot.Humidity}%";
        _cardHumidityVal.Foreground = textBrush;
        _cardWindVal.Text = $"{snapshot.WindSpeedKmh:F0} km/h";
        _cardWindVal.Foreground = textBrush;
        _cardAqiVal.Text = snapshot.AirQualityIndex < 0
            ? "—"
            : $"{snapshot.AirQualityIndex} · {WeatherFormat.AqiLevel(snapshot.AirQualityIndex)}";
        _cardAqiVal.Foreground = snapshot.AirQualityIndex < 0 ? textBrush : TokenBrushes.From(aqiColor);

        // Render Hourly Cards
        _hourlyPanel.Children.Clear();
        var now = DateTime.Now;
        var isFirst = true;

        foreach (var h in snapshot.Hourly)
        {
            var isCurrentHour = isFirst || (h.Time.Hour == now.Hour && h.Time.Date == now.Date);
            var timeLabel = isFirst ? UiText.WeatherNow : h.Time.ToString("HH:mm");
            isFirst = false;

            var hGlyph = new TextBlock
            {
                Text = WeatherGlyphMapper.ConditionGlyph(h.Condition),
                FontFamily = WeatherGlyphFont,
                FontSize = 15,
                Foreground = TokenBrushes.From(WeatherGlyphMapper.ConditionColor(h.Condition)),
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var hTime = new TextBlock
            {
                Text = timeLabel,
                FontFamily = WidgetFonts.Ui,
                FontSize = 10,
                Foreground = textBrush,
                Opacity = isCurrentHour ? 1.0 : 0.7,
                FontWeight = isCurrentHour ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var hTemp = new TextBlock
            {
                Text = WeatherFormat.Temperature(h.TemperatureC, fahr),
                FontFamily = WidgetFonts.Ui,
                FontSize = 11,
                Foreground = textBrush,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var hPrecip = new TextBlock
            {
                Text = h.PrecipitationProbability > 0 ? $"{h.PrecipitationProbability}%" : " ",
                FontFamily = WidgetFonts.Ui,
                FontSize = 9,
                Opacity = 0.8,
                Foreground = TokenBrushes.From(0xFF38BDF8u),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var cardStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { hTime, hGlyph, hTemp, hPrecip }
            };

            var cardBorder = new Border
            {
                Width = DockTokens.WeatherHourCardWidth,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 5, 4, 5),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.SizeWE,
                Background = isCurrentHour
                    ? TokenBrushes.From(_isLight ? 0x18000000u : 0x22FFFFFFu)
                    : TokenBrushes.From(_isLight ? 0x06000000u : 0x08FFFFFFu),
                Child = cardStack
            };

            _hourlyPanel.Children.Add(cardBorder);
        }

        // Render 7-Day Forecast with Temperature Range Bars
        _dailyPanel.Children.Clear();
        var dailyList = snapshot.Daily.Take(7).ToList();
        if (dailyList.Count > 0)
        {
            var weekMin = dailyList.Min(d => d.TempMinC);
            var weekMax = dailyList.Max(d => d.TempMaxC);
            var weekSpan = Math.Max(1.0, weekMax - weekMin);
            var today = DateOnly.FromDateTime(DateTime.Today);

            foreach (var d in dailyList)
            {
                string dayName;
                if (d.Date == today)
                {
                    dayName = UiText.Today;
                }
                else if (d.Date == today.AddDays(1))
                {
                    dayName = UiText.Tomorrow;
                }
                else
                {
                    dayName = d.Date.ToString("ddd");
                }

                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // Weekday
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // Glyph
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) }); // Condition Text
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Min temp
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Range bar
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Max temp

                var dayBlock = new TextBlock
                {
                    Text = dayName,
                    FontFamily = WidgetFonts.Ui,
                    FontSize = 12,
                    Foreground = textBrush,
                    Opacity = d.Date == today ? 1.0 : 0.85,
                    FontWeight = d.Date == today ? FontWeights.SemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dayBlock, 0);
                row.Children.Add(dayBlock);

                var glyphBlock = new TextBlock
                {
                    Text = WeatherGlyphMapper.ConditionGlyph(d.Condition),
                    FontFamily = WeatherGlyphFont,
                    FontSize = 14,
                    Foreground = TokenBrushes.From(WeatherGlyphMapper.ConditionColor(d.Condition)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(glyphBlock, 1);
                row.Children.Add(glyphBlock);

                var condBlock = new TextBlock
                {
                    Text = d.ConditionText,
                    FontFamily = WidgetFonts.Ui,
                    FontSize = 12,
                    Foreground = textBrush,
                    Opacity = 0.85,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                Grid.SetColumn(condBlock, 2);
                row.Children.Add(condBlock);

                var minBlock = new TextBlock
                {
                    Text = WeatherFormat.Temperature(d.TempMinC, fahr),
                    FontFamily = WidgetFonts.Ui,
                    FontSize = 11,
                    Foreground = textMutedBrush,
                    Opacity = 0.75,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(minBlock, 3);
                row.Children.Add(minBlock);

                // Range Bar Container
                var barContainer = new Grid
                {
                    Height = 4,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0)
                };

                var barTrack = new Border
                {
                    CornerRadius = new CornerRadius(2),
                    Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x1FFFFFFFu)
                };
                barContainer.Children.Add(barTrack);

                // Calculate bar start and length percentage relative to week
                var startRatio = Math.Clamp((d.TempMinC - weekMin) / weekSpan, 0.0, 1.0);
                var endRatio = Math.Clamp((d.TempMaxC - weekMin) / weekSpan, 0.0, 1.0);
                var lengthRatio = Math.Max(0.08, endRatio - startRatio);

                var activeBar = new Border
                {
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = new LinearGradientBrush(
                        Color.FromRgb(0x38, 0xBD, 0xF8), // Sky blue
                        Color.FromRgb(0xFB, 0x92, 0x3C), // Amber orange
                        new Point(0, 0),
                        new Point(1, 0))
                };

                var barColumnGrid = new Grid();
                barColumnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(startRatio, GridUnitType.Star) });
                barColumnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(lengthRatio, GridUnitType.Star) });
                barColumnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1.0 - startRatio - lengthRatio), GridUnitType.Star) });

                Grid.SetColumn(activeBar, 1);
                barColumnGrid.Children.Add(activeBar);
                barContainer.Children.Add(barColumnGrid);

                Grid.SetColumn(barContainer, 4);
                row.Children.Add(barContainer);

                var maxBlock = new TextBlock
                {
                    Text = WeatherFormat.Temperature(d.TempMaxC, fahr),
                    FontFamily = WidgetFonts.Ui,
                    FontSize = 11,
                    Foreground = textBrush,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(maxBlock, 5);
                row.Children.Add(maxBlock);

                _dailyPanel.Children.Add(row);
            }
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateHourlyNavButtons());
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Opacity = 0.65,
        Margin = new Thickness(0, 6, 0, 4)
    };

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid();
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


    private static Border ActionButton(string label, Action click)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var border = new Border
        {
            Height = DockTokens.FlyoutControlHeight,
            Background = TokenBrushes.From(0x22000000u),
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            Child = text
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return border;
    }
}
