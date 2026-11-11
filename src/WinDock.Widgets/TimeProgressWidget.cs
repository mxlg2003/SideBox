using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class TimeProgressWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IWidgetModeAware, IDockEdgeAware, ITimeProgressConfigurable, IWidgetThemeAware, ILocalizable, IIslandLayoutAware
{
    public event EventHandler? IslandLayoutChanged;
    private const uint Accent = 0xFF0078D4;
    private const uint QuietFill = 0x14000000;
    private const double DockRing = 18;
    private const double FlyoutRing = 52;
    private const double ExpandedWidth = 124;

    private readonly IClockProvider _clock;
    private readonly Grid _dockBody = new();
    private readonly TextBlock _dockCaption = WidgetCopy.Secondary();
    private readonly TextBlock _dockPercent = WidgetCopy.Primary();
    private readonly TextBlock _ringPercent = WidgetCopy.Primary();
    private readonly TimeProgressBars _dockBars = new();
    private readonly ProgressRing _dockRing = new(DockRing) { Clockwise = true };
    private readonly PeriodFace[] _faces =
    [
        new(TimeProgressPeriods.Day),
        new(TimeProgressPeriods.Month),
        new(TimeProgressPeriods.Year)
    ];
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private readonly ComboBox _periodBox = new();
    private readonly Grid _root;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _periodCaption = null!;
    private TextBlock _layoutCaption = null!;
    private readonly Dictionary<string, TextBlock> _layoutTitles = new(StringComparer.Ordinal);
    private readonly List<(string Layout, Border Row, PhosphorGlyph Check)> _layoutMarks = [];
    private readonly DispatcherTimer _timer;
    private TimeProgressSettings _settings = TimeProgressSettings.Default;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private bool _applying;
    private bool _isLight = true;
    private bool _side;

    public TimeProgressWidget(IClockProvider clock, WidgetDensity density = WidgetDensity.Expanded)
    {
        _clock = clock;
        _density = density;
        _dockBody.VerticalAlignment = VerticalAlignment.Center;
        _dockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _dockCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _dockPercent.FontSize = 16;
        _ringPercent.FontSize = 11;
        _ringPercent.FontWeight = FontWeights.SemiBold;
        _ringPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _ringPercent.VerticalAlignment = VerticalAlignment.Center;
        _dockBars.Height = 10;

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

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) =>
        {
            RebuildTile();
            Tick();
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _popup.IsOpen = false;
        };

        _root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        _root.Children.Add(_tile);
        _root.Children.Add(_popup);
        Content = _root;
        ApplyTheme(true);
        RebuildTile();
        Tick();
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<TimeProgressSettings>? SettingsChanged;

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = TokenBrushes.From(isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        var fill = TokenBrushes.From(isLight ? 0xFF2A2A32u : 0xFFFFFFFFu);
        var track = TokenBrushes.From(isLight ? 0x22000000u : 0x33FFFFFFu);
        _dockBars.Fill = fill;
        _dockBars.Track = track;
        _dockRing.Fill = fill;
        _dockRing.Track = track;
        foreach (var face in _faces)
        {
            face.Bars.Fill = fill;
            face.Bars.Track = track;
            face.Ring.Fill = fill;
            face.Ring.Track = track;
        }

        WidgetChrome.PaintCombo(_periodBox, isLight);
    }

    public void ApplyLocale()
    {
        _applying = true;
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.TimeProgress);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _periodCaption.Text = UiText.Period;
        _layoutCaption.Text = UiText.Layout;
        foreach (var (layout, title) in _layoutTitles)
        {
            title.Text = TimeProgressFormat.LayoutTitle(layout);
        }

        FillPeriodBox();
        _applying = false;
        Tick();
    }

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        RebuildTile();
        WidgetChrome.FitEdge(_tile, _side, 0);
        Tick();
    }

    public void ApplyDensity(WidgetDensity density)
    {
        var changed = _density != density;
        _density = density;
        RebuildTile();
        Tick();
        if (changed)
        {
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplySettings(TimeProgressSettings settings)
    {
        _applying = true;
        var layoutChanged = !string.Equals(_settings.Layout, settings.Layout, StringComparison.OrdinalIgnoreCase);
        _settings = TimeProgressSettings.Normalize(settings);
        PaintCustomize();
        RebuildTile();
        Tick();
        _applying = false;
        if (layoutChanged)
        {
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Open()
    {
        ShowEditor();
        Tick();
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
        Release(_dockCaption);
        Release(_dockPercent);
        Release(_dockBars);
        Release(_dockRing);
        Release(_ringPercent);
        _dockBody.Children.Clear();
        var compact = _density != WidgetDensity.Expanded;
        var layout = TimeProgressLayouts.Normalize(_settings.Layout);
        _dockBody.Children.Add(layout switch
        {
            TimeProgressLayouts.Ring => RingTile(compact),
            TimeProgressLayouts.Percentage => PercentTile(compact),
            _ => BarsTile(compact)
        });

        RebuildFlyout();
        if (_side)
        {
            _dockBody.Width = double.NaN;
            _dockBody.MinWidth = 0;
        }
        else
        {
            _dockBody.Width = compact ? double.NaN : ExpandedWidth;
            _dockBody.MinWidth = compact ? 0 : ExpandedWidth;
        }
        WidgetChrome.ApplyTheme(_tile, _isLight);
        _dockBody.InvalidateMeasure();
        _tile.InvalidateMeasure();
        _root?.InvalidateMeasure();
        InvalidateMeasure();
    }

    private UIElement BarsTile(bool compact)
    {
        if (_side)
        {
            var inner = DockTokens.SideTileWidth - 12;
            _dockBars.Width = inner;
            _dockBars.Height = 8;
            _dockBars.BarCount = 24;
            _dockBars.Margin = new Thickness(0, 4, 0, 0);
            _dockPercent.FontSize = 15;
            ResetCaption(HorizontalAlignment.Center);
            _dockCaption.TextAlignment = TextAlignment.Center;
            _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
            _dockPercent.TextAlignment = TextAlignment.Center;
            return new StackPanel
            {
                Width = inner,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _dockCaption, _dockPercent, _dockBars }
            };
        }

        _dockBars.Width = compact ? 88 : ExpandedWidth;
        _dockBars.Height = compact ? 9 : 11;
        _dockBars.BarCount = compact ? 22 : 36;
        _dockBars.Margin = new Thickness(0, compact ? 3 : 4, 0, 0);
        _dockPercent.FontSize = compact ? 16 : 15;
        ResetCaption(compact ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        if (compact)
        {
            _dockCaption.HorizontalAlignment = HorizontalAlignment.Center;
            _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
            return new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _dockPercent, _dockCaption, _dockBars }
            };
        }

        return HeaderAndVisual(_dockBars, ExpandedWidth);
    }

    private UIElement PercentTile(bool compact)
    {
        if (_side)
        {
            _dockPercent.FontSize = 18;
            ResetCaption(HorizontalAlignment.Center);
            _dockCaption.TextAlignment = TextAlignment.Center;
            _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
            _dockPercent.TextAlignment = TextAlignment.Center;
            return new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _dockPercent, _dockCaption }
            };
        }

        _dockPercent.FontSize = compact ? 18 : 16;
        ResetCaption(compact ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        if (compact)
        {
            _dockCaption.HorizontalAlignment = HorizontalAlignment.Center;
            _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
            return new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 48,
                Children = { _dockPercent, _dockCaption }
            };
        }

        return HeaderAndVisual(null, ExpandedWidth);
    }

    private UIElement RingTile(bool compact)
    {
        if (_side)
        {
            _dockRing.Width = DockRing;
            _dockRing.Height = DockRing;
            var ringFace = new Grid
            {
                Width = DockRing,
                Height = DockRing,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _dockRing }
            };
            ResetCaption(HorizontalAlignment.Center);
            _dockCaption.Margin = new Thickness(0, 1, 0, 0);
            _dockCaption.TextAlignment = TextAlignment.Center;
            _dockPercent.FontSize = 15;
            _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
            _dockPercent.TextAlignment = TextAlignment.Center;
            _dockPercent.Margin = new Thickness(0, 2, 0, 0);
            return new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { ringFace, _dockPercent, _dockCaption }
            };
        }

        var size = compact ? 16 : DockRing;
        _dockRing.Width = size;
        _dockRing.Height = size;
        var face = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _dockRing }
        };
        ResetCaption(HorizontalAlignment.Center);
        _dockCaption.Margin = new Thickness(0, 1, 0, 0);
        _dockPercent.FontSize = compact ? 15 : 16;
        _dockPercent.HorizontalAlignment = HorizontalAlignment.Left;
        _dockPercent.VerticalAlignment = VerticalAlignment.Center;
        _dockPercent.Margin = new Thickness(6, 0, 0, 0);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { face, _dockPercent }
        };
        return new StackPanel
        {
            Width = compact ? double.NaN : ExpandedWidth,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = compact ? new Thickness(4, 0, 6, 0) : new Thickness(0),
            Children = { row, _dockCaption }
        };
    }

    private void ResetCaption(HorizontalAlignment align)
    {
        _dockCaption.HorizontalAlignment = align;
        _dockCaption.VerticalAlignment = VerticalAlignment.Center;
        _dockCaption.Margin = new Thickness(0);
        _dockCaption.FontSize = DockTokens.SecondaryFontSize;
        _dockPercent.Margin = new Thickness(0);
        _dockPercent.VerticalAlignment = VerticalAlignment.Center;
    }

    private UIElement HeaderAndVisual(UIElement? visual, double width)
    {
        _dockCaption.HorizontalAlignment = HorizontalAlignment.Left;
        _dockPercent.HorizontalAlignment = HorizontalAlignment.Right;
        _dockPercent.Margin = new Thickness(0);
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_dockCaption);
        Grid.SetColumn(_dockPercent, 1);
        header.Children.Add(_dockPercent);
        var stack = new StackPanel
        {
            Width = width,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(header);
        if (visual is not null)
        {
            stack.Children.Add(visual);
        }

        return stack;
    }

    private void Tick()
    {
        var now = _clock.LocalNow;
        var period = _settings.Period;
        var fraction = TimeProgressFormat.Fraction(now, period);
        var percent = TimeProgressFormat.PercentLabel(now, period);
        var caption = TimeProgressFormat.Caption(now, period);
        _dockCaption.Text = caption;
        _dockPercent.Text = percent;
        _ringPercent.Text = percent;
        _dockBars.Progress = fraction;
        _dockRing.Progress = fraction;
        foreach (var face in _faces)
        {
            var part = TimeProgressFormat.Fraction(now, face.Period);
            var label = TimeProgressFormat.PercentLabel(now, face.Period);
            face.Caption.Text = TimeProgressFormat.Caption(now, face.Period);
            face.Percent.Text = label;
            face.RingPercent.Text = label;
            face.Bars.Progress = part;
            face.Ring.Progress = part;
            face.Bars.Width = 288;
            face.Bars.BarCount = 40;
        }
    }

    private void RebuildFlyout()
    {
        var layout = TimeProgressLayouts.Normalize(_settings.Layout);
        foreach (var face in _faces)
        {
            Release(face.Caption);
            Release(face.Percent);
            Release(face.Bars);
            Release(face.Ring);
            Release(face.RingPercent);
            face.Root.Children.Clear();
            face.Caption.Margin = new Thickness(0);
            face.Caption.FontSize = 15;
            face.Percent.FontSize = layout == TimeProgressLayouts.Percentage ? 22 : 15;
            face.Percent.FontWeight = FontWeights.SemiBold;
            if (layout == TimeProgressLayouts.Ring)
            {
                face.Ring.Width = FlyoutRing;
                face.Ring.Height = FlyoutRing;
                face.RingPercent.FontSize = 13;
                face.RingPercent.FontWeight = FontWeights.SemiBold;
                var ring = new Grid
                {
                    Width = FlyoutRing,
                    Height = FlyoutRing,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { face.Ring, face.RingPercent }
                };
                face.Caption.Margin = new Thickness(14, 0, 0, 0);
                face.Caption.VerticalAlignment = VerticalAlignment.Center;
                face.Root.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { ring, face.Caption }
                });
                continue;
            }

            face.Root.Children.Add(PeriodHeader(face));
            if (layout == TimeProgressLayouts.Bars)
            {
                face.Bars.Height = 14;
                face.Bars.Margin = new Thickness(0, 8, 0, 0);
                face.Root.Children.Add(face.Bars);
            }
        }
    }

    private static Grid PeriodHeader(PeriodFace face)
    {
        face.Caption.HorizontalAlignment = HorizontalAlignment.Left;
        face.Caption.VerticalAlignment = VerticalAlignment.Center;
        face.Percent.HorizontalAlignment = HorizontalAlignment.Right;
        face.Percent.VerticalAlignment = VerticalAlignment.Center;
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(face.Caption);
        Grid.SetColumn(face.Percent, 1);
        header.Children.Add(face.Percent);
        return header;
    }

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

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.TimeProgress));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        var periods = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var face in _faces)
        {
            periods.Children.Add(face.Root);
        }

        var page = new StackPanel { Children = { header, periods } };
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

        _periodCaption = HeaderLabel(UiText.Period);
        _periodCaption.Opacity = 0.62;
        _periodCaption.Margin = new Thickness(0, 8, 0, 8);
        WidgetChrome.PaintCombo(_periodBox, _isLight);
        FillPeriodBox();

        _periodBox.SelectedIndex = 0;
        _periodBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _periodBox.SelectedItem is not ComboChoice choice)
            {
                return;
            }

            _settings = _settings with { Period = TimeProgressPeriods.Normalize(choice.Id) };
            Persist();
            RebuildTile();
            Tick();
        };

        _layoutCaption = HeaderLabel(UiText.Layout);
        _layoutCaption.Opacity = 0.62;
        _layoutCaption.Margin = new Thickness(0, 16, 0, 8);
        var layouts = new StackPanel();
        foreach (var layout in TimeProgressLayouts.All)
        {
            layouts.Children.Add(LayoutRow(layout));
        }

        var page = new StackPanel
        {
            Children = { header, _periodCaption, _periodBox, _layoutCaption, layouts }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private void FillPeriodBox()
    {
        var selected = _periodBox.SelectedItem is ComboChoice choice ? choice.Id : _settings.Period;
        _periodBox.Items.Clear();
        foreach (var period in TimeProgressPeriods.All)
        {
            _periodBox.Items.Add(new ComboChoice(period, TimeProgressFormat.PeriodTitle(period)));
        }

        WidgetChrome.SelectChoice(_periodBox, selected);
    }

    private Border LayoutRow(string layout)
    {
        var check = WidgetChrome.AccentCheck(Accent);
        UIElement glyph = layout == TimeProgressLayouts.Percentage
            ? new TextBlock
            {
                Text = "%",
                FontFamily = WidgetFonts.Ui,
                FontSize = 14,
                Width = 22,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            }
            : new PhosphorGlyph(LayoutGlyph(layout), 14)
            {
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
        var title = HeaderLabel(TimeProgressFormat.LayoutTitle(layout));
        _layoutTitles[layout] = title;
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
        _settings = _settings with { Layout = TimeProgressLayouts.Normalize(layout) };
        Persist();
        PaintCustomize();
        RebuildTile();
        Tick();
        ApplyTheme(_isLight);
    }

    private void PaintCustomize()
    {
        foreach (var (layout, row, check) in _layoutMarks)
        {
            var selected = string.Equals(layout, _settings.Layout, StringComparison.Ordinal);
            row.Background = selected ? TokenBrushes.From(QuietFill) : Brushes.Transparent;
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        WidgetChrome.SelectChoice(_periodBox, _settings.Period);
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

    private static string LayoutGlyph(string layout) => layout switch
    {
        TimeProgressLayouts.Ring => PhosphorIcon.Circle,
        _ => PhosphorIcon.ChartBar
    };

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

    private sealed class PeriodFace
    {
        public PeriodFace(string period)
        {
            Period = period;
            Caption.FontSize = 15;
            Percent.FontSize = 15;
            Percent.FontWeight = FontWeights.SemiBold;
            RingPercent.FontSize = 13;
            RingPercent.FontWeight = FontWeights.SemiBold;
            RingPercent.HorizontalAlignment = HorizontalAlignment.Center;
            RingPercent.VerticalAlignment = VerticalAlignment.Center;
            Bars.Height = 14;
            Root.Margin = new Thickness(0, 14, 0, 0);
        }

        public string Period { get; }

        public TextBlock Caption { get; } = WidgetCopy.Primary();

        public TextBlock Percent { get; } = WidgetCopy.Primary();

        public TextBlock RingPercent { get; } = WidgetCopy.Primary();

        public TimeProgressBars Bars { get; } = new();

        public ProgressRing Ring { get; } = new(FlyoutRing) { Clockwise = true };

        public StackPanel Root { get; } = new();
    }

}
