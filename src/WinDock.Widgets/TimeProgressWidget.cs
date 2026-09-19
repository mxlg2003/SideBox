using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class TimeProgressWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IWidgetModeAware, ITimeProgressConfigurable, IWidgetThemeAware
{
    private const uint Accent = 0xFF0078D4;
    private const uint QuietFill = 0x14000000;
    private const double DockRing = 18;
    private const double FlyoutRing = 88;
    private const double ExpandedWidth = 124;

    private readonly IClockProvider _clock;
    private readonly Grid _dockBody = new();
    private readonly TextBlock _dockCaption = WidgetCopy.Secondary();
    private readonly TextBlock _dockPercent = WidgetCopy.Primary();
    private readonly TextBlock _ringPercent = WidgetCopy.Primary();
    private readonly TextBlock _panelCaption = WidgetCopy.Primary();
    private readonly TextBlock _panelPercent = WidgetCopy.Primary();
    private readonly TextBlock _panelRingPercent = WidgetCopy.Primary();
    private readonly TimeProgressBars _dockBars = new();
    private readonly TimeProgressBars _panelBars = new();
    private readonly ProgressRing _dockRing = new(DockRing) { Clockwise = true };
    private readonly ProgressRing _panelRing = new(FlyoutRing) { Clockwise = true };
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private readonly ComboBox _periodBox = new();
    private readonly List<(string Layout, Border Row, TextBlock Check)> _layoutMarks = [];
    private readonly DispatcherTimer _timer;
    private readonly StackPanel _panelVisual = new();
    private TimeProgressSettings _settings = TimeProgressSettings.Default;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private bool _applying;
    private bool _isLight = true;

    public TimeProgressWidget(IClockProvider clock)
    {
        _clock = clock;
        _dockBody.VerticalAlignment = VerticalAlignment.Center;
        _dockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _dockCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _dockPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _dockPercent.FontSize = 16;
        _ringPercent.FontSize = 11;
        _ringPercent.FontWeight = FontWeights.SemiBold;
        _ringPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _ringPercent.VerticalAlignment = VerticalAlignment.Center;
        _panelCaption.FontSize = 22;
        _panelPercent.FontSize = 22;
        _panelPercent.FontWeight = FontWeights.SemiBold;
        _panelRingPercent.FontSize = 20;
        _panelRingPercent.FontWeight = FontWeights.SemiBold;
        _panelRingPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _panelRingPercent.VerticalAlignment = VerticalAlignment.Center;
        _dockBars.Height = 10;
        _panelBars.Height = 28;
        _panelBars.Margin = new Thickness(0, 16, 0, 4);

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

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
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
        _panelBars.Fill = fill;
        _panelBars.Track = track;
        _dockRing.Fill = fill;
        _dockRing.Track = track;
        _panelRing.Fill = fill;
        _panelRing.Track = track;
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        RebuildTile();
        Tick();
    }

    public void ApplySettings(TimeProgressSettings settings)
    {
        _applying = true;
        _settings = TimeProgressSettings.Normalize(settings);
        PaintCustomize();
        RebuildTile();
        Tick();
        _applying = false;
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
        Release(_panelBars);
        Release(_panelRing);
        Release(_panelRingPercent);
        _dockBody.Children.Clear();
        var compact = _density != WidgetDensity.Expanded;
        var layout = TimeProgressLayouts.Normalize(_settings.Layout);
        _dockBody.Children.Add(layout switch
        {
            TimeProgressLayouts.Ring => RingTile(compact),
            TimeProgressLayouts.Percentage => PercentTile(compact),
            _ => BarsTile(compact)
        });

        _panelVisual.Children.Clear();
        if (layout == TimeProgressLayouts.Bars)
        {
            _panelVisual.Children.Add(_panelBars);
        }
        else if (layout == TimeProgressLayouts.Ring)
        {
            var face = new Grid
            {
                Width = FlyoutRing,
                Height = FlyoutRing,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 4),
                Children = { _panelRing, _panelRingPercent }
            };
            _panelVisual.Children.Add(face);
        }

        _dockBody.Width = compact ? double.NaN : ExpandedWidth;
        _dockBody.MinWidth = compact ? 0 : ExpandedWidth;
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    private UIElement BarsTile(bool compact)
    {
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
        _panelCaption.Text = caption;
        _panelPercent.Text = percent;
        _panelRingPercent.Text = percent;
        _dockBars.Progress = fraction;
        _panelBars.Progress = fraction;
        _dockRing.Progress = fraction;
        _panelRing.Progress = fraction;
        _panelBars.Width = 288;
        _panelBars.BarCount = 52;
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
        }
    }

    private Grid BuildEditor()
    {
        var title = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.TimeProgress));
        var customize = HeaderGlyph("\uE8FD", OpenCustomize);
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(title, customize, close);
        var names = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        names.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        names.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        names.Children.Add(_panelCaption);
        Grid.SetColumn(_panelPercent, 1);
        names.Children.Add(_panelPercent);
        var page = new StackPanel { Children = { header, names, _panelVisual } };
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

        var periodCaption = HeaderLabel("周期");
        periodCaption.Opacity = 0.62;
        periodCaption.Margin = new Thickness(0, 8, 0, 8);
        foreach (var period in TimeProgressPeriods.All)
        {
            _periodBox.Items.Add(new ComboBoxItem
            {
                Content = TimeProgressFormat.PeriodTitle(period),
                Tag = period
            });
        }

        _periodBox.FontFamily = WidgetFonts.Ui;
        _periodBox.FontSize = 14;
        _periodBox.BorderThickness = new Thickness(0);
        _periodBox.Padding = new Thickness(10, 6, 8, 6);
        _periodBox.Background = Brushes.Transparent;
        _periodBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        _periodBox.SelectedIndex = 0;
        _periodBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _periodBox.SelectedItem is not ComboBoxItem item || item.Tag is not string period)
            {
                return;
            }

            _settings = _settings with { Period = TimeProgressPeriods.Normalize(period) };
            Persist();
            RebuildTile();
            Tick();
        };
        var periodWrap = new Border
        {
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Background = TokenBrushes.From(QuietFill),
            Padding = new Thickness(4, 2, 4, 2),
            Child = _periodBox
        };

        var layoutCaption = HeaderLabel("布局");
        layoutCaption.Opacity = 0.62;
        layoutCaption.Margin = new Thickness(0, 16, 0, 8);
        var layouts = new StackPanel();
        foreach (var layout in TimeProgressLayouts.All)
        {
            layouts.Children.Add(LayoutRow(layout));
        }

        var page = new StackPanel
        {
            Children = { header, periodCaption, periodWrap, layoutCaption, layouts }
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
            FontFamily = layout == TimeProgressLayouts.Percentage ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Width = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var title = HeaderLabel(TimeProgressFormat.LayoutTitle(layout));
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

        foreach (var item in _periodBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string period && string.Equals(period, _settings.Period, StringComparison.Ordinal))
            {
                _periodBox.SelectedItem = item;
                break;
            }
        }
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
        TimeProgressLayouts.Percentage => "%",
        TimeProgressLayouts.Ring => "\uEA3A",
        _ => "\uE9D2"
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
