using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class SysActivityWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, ISysActivityConfigurable, IWidgetThemeAware, ILocalizable
{
    private const uint QuietFill = 0x14000000;
    private const uint Accent = 0xFF0078D4;
    private const double DockRing = 34;
    private const double DockRingStroke = 2.6;
    private const double FlyoutRing = 88;
    private const double CaptionRingGap = 6;
    private const double MeterGap = 8;
    private static readonly double DockWidth = MeasureDockWidth();

    private readonly IPerfSampler _perf;
    private readonly EventHandler<PerfSnapshot> _updated;
    private readonly Grid _dockBody = new();
    private readonly TextBlock _cpuPercent = WidgetCopy.Primary();
    private readonly TextBlock _memPercent = WidgetCopy.Primary();
    private readonly TextBlock _cpuRingPercent = WidgetCopy.Primary();
    private readonly TextBlock _memRingPercent = WidgetCopy.Primary();
    private readonly TextBlock _cpuCaption = WidgetCopy.Secondary();
    private readonly TextBlock _memCaption = WidgetCopy.Secondary();
    private readonly Ellipse _cpuDot = Dot();
    private readonly Ellipse _memDot = Dot();
    private readonly ProgressRing _cpuRing = new(DockRing) { Clockwise = true, StrokeThickness = DockRingStroke };
    private readonly ProgressRing _memRing = new(DockRing) { Clockwise = true, StrokeThickness = DockRingStroke };
    private readonly SysActivityBar _cpuBar = new();
    private readonly SysActivityBar _memBar = new();
    private readonly ProgressRing _panelCpuRing = new(FlyoutRing) { Clockwise = true };
    private readonly ProgressRing _panelMemRing = new(FlyoutRing) { Clockwise = true };
    private readonly TextBlock _panelCpuPercent = WidgetCopy.Primary();
    private readonly TextBlock _panelMemPercent = WidgetCopy.Primary();
    private readonly TextBlock _panelCpuCaption = WidgetCopy.Secondary();
    private readonly TextBlock _panelMemCaption = WidgetCopy.Secondary();
    private readonly TextBlock _usedLabel = WidgetCopy.Secondary();
    private readonly TextBlock _usedValue = WidgetCopy.Primary();
    private readonly List<(string Layout, Border Row, TextBlock Check)> _layoutMarks = [];
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _layoutCaption = null!;
    private readonly Dictionary<string, TextBlock> _layoutTitles = new(StringComparer.Ordinal);
    private SysActivitySettings _settings = SysActivitySettings.Default;
    private bool _applying;
    private bool _isLight = true;

    public SysActivityWidget(IPerfSampler perf)
    {
        _perf = perf;
        _updated = UiPump.Coalesce<PerfSnapshot>(Dispatcher, Render);
        _cpuCaption.Text = "CPU";
        _memCaption.Text = UiText.MemoryShort;
        _panelCpuCaption.Text = "CPU";
        _panelMemCaption.Text = UiText.Memory;
        _usedLabel.Text = UiText.MemoryUsed;
        _cpuPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _memPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _cpuRingPercent.FontSize = 11;
        _memRingPercent.FontSize = 11;
        _cpuRingPercent.Padding = new Thickness(0);
        _memRingPercent.Padding = new Thickness(0);
        _cpuRingPercent.LineHeight = 12;
        _memRingPercent.LineHeight = 12;
        _cpuRingPercent.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _memRingPercent.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _cpuRingPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _cpuRingPercent.VerticalAlignment = VerticalAlignment.Center;
        _memRingPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _memRingPercent.VerticalAlignment = VerticalAlignment.Center;
        _panelCpuPercent.FontSize = 20;
        _panelMemPercent.FontSize = 20;
        _panelCpuPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _panelCpuPercent.VerticalAlignment = VerticalAlignment.Center;
        _panelMemPercent.HorizontalAlignment = HorizontalAlignment.Center;
        _panelMemPercent.VerticalAlignment = VerticalAlignment.Center;
        _panelCpuCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _panelMemCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _cpuCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _memCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _cpuCaption.Margin = new Thickness(0);
        _memCaption.Margin = new Thickness(0);
        _usedValue.FontSize = 13;
        _cpuDot.Fill = TokenBrushes.From(DockTokens.Cpu);
        _memDot.Fill = TokenBrushes.From(DockTokens.Memory);

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
            _perf.Updated += _updated;
            RebuildTile();
            Render(_perf.Current);
        };
        Unloaded += (_, _) =>
        {
            _perf.Updated -= _updated;
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ApplyTheme(true);
        RebuildTile();
        Render(perf.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<SysActivitySettings>? SettingsChanged;

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = TokenBrushes.From(isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        _cpuRing.Fill = TokenBrushes.From(DockTokens.Cpu);
        _memRing.Fill = TokenBrushes.From(DockTokens.Memory);
        _panelCpuRing.Fill = TokenBrushes.From(DockTokens.Cpu);
        _panelMemRing.Fill = TokenBrushes.From(DockTokens.Memory);
        _cpuRing.Track = TokenBrushes.From(Fade(DockTokens.Cpu, 0x33));
        _memRing.Track = TokenBrushes.From(Fade(DockTokens.Memory, 0x33));
        _panelCpuRing.Track = TokenBrushes.From(Fade(DockTokens.Cpu, 0x33));
        _panelMemRing.Track = TokenBrushes.From(Fade(DockTokens.Memory, 0x33));
        _cpuBar.Fill = TokenBrushes.From(DockTokens.Cpu);
        _memBar.Fill = TokenBrushes.From(DockTokens.Memory);
        _cpuBar.Track = TokenBrushes.From(Fade(DockTokens.Cpu, 0x33));
        _memBar.Track = TokenBrushes.From(Fade(DockTokens.Memory, 0x33));
        _cpuDot.Fill = TokenBrushes.From(DockTokens.Cpu);
        _memDot.Fill = TokenBrushes.From(DockTokens.Memory);
    }

    public void ApplyLocale()
    {
        _memCaption.Text = UiText.MemoryShort;
        _panelMemCaption.Text = UiText.Memory;
        _usedLabel.Text = UiText.MemoryUsed;
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.SysActivity);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _layoutCaption.Text = UiText.Layout;
        foreach (var (layout, title) in _layoutTitles)
        {
            title.Text = SysActivityFormat.LayoutTitle(layout);
        }

        Render(_perf.Current);
    }

    public void ApplySettings(SysActivitySettings settings)
    {
        _applying = true;
        _settings = SysActivitySettings.Normalize(settings);
        PaintCustomize();
        RebuildTile();
        Render(_perf.Current);
        _applying = false;
    }

    public void Open()
    {
        ShowEditor();
        Render(_perf.Current);
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
        Release(_cpuPercent);
        Release(_memPercent);
        Release(_cpuRingPercent);
        Release(_memRingPercent);
        Release(_cpuCaption);
        Release(_memCaption);
        Release(_cpuDot);
        Release(_memDot);
        Release(_cpuRing);
        Release(_memRing);
        Release(_cpuBar);
        Release(_memBar);
        _cpuCaption.ClearValue(FrameworkElement.WidthProperty);
        _memCaption.ClearValue(FrameworkElement.WidthProperty);
        _cpuCaption.Margin = new Thickness(0);
        _memCaption.Margin = new Thickness(0);
        _dockBody.Children.Clear();
        var layout = SysActivityLayouts.Normalize(_settings.Layout);
        _dockBody.Children.Add(layout switch
        {
            SysActivityLayouts.Rings => RingsTile(),
            SysActivityLayouts.Bars => BarsTile(),
            _ => NumbersTile()
        });
        _dockBody.Width = DockWidth;
        _dockBody.MinWidth = DockWidth;
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    private UIElement NumbersTile()
    {
        _cpuPercent.FontSize = 18;
        _memPercent.FontSize = 18;
        return Pair(MeterColumn(_cpuPercent, _cpuDot, _cpuCaption), MeterColumn(_memPercent, _memDot, _memCaption));
    }

    private UIElement RingsTile()
    {
        _cpuRingPercent.FontSize = 11;
        _memRingPercent.FontSize = 11;
        return Pair(RingColumn(_cpuRing, _cpuRingPercent, _cpuCaption), RingColumn(_memRing, _memRingPercent, _memCaption));
    }

    private UIElement BarsTile()
    {
        _cpuPercent.FontSize = 13;
        _memPercent.FontSize = 13;
        _cpuPercent.HorizontalAlignment = HorizontalAlignment.Right;
        _memPercent.HorizontalAlignment = HorizontalAlignment.Right;
        _cpuCaption.HorizontalAlignment = HorizontalAlignment.Left;
        _memCaption.HorizontalAlignment = HorizontalAlignment.Left;
        _cpuBar.Height = 6;
        _memBar.Height = 6;
        _cpuBar.Margin = new Thickness(8, 0, 8, 0);
        var stack = new StackPanel
        {
            Width = DockWidth,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(BarRow(_cpuCaption, _cpuBar, _cpuPercent));
        _memBar.Margin = new Thickness(8, 6, 8, 0);
        stack.Children.Add(BarRow(_memCaption, _memBar, _memPercent));
        return stack;
    }

    private static UIElement MeterColumn(TextBlock percent, Ellipse dot, TextBlock caption)
    {
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        caption.Margin = new Thickness(4, 0, 0, 0);
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                percent,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Children = { dot, caption }
                }
            }
        };
    }

    private static UIElement RingColumn(ProgressRing ring, TextBlock percent, TextBlock caption)
    {
        var face = new Grid
        {
            Width = DockRing,
            Height = DockRing,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { ring, percent }
        };
        caption.Margin = new Thickness(0, 0, CaptionRingGap, 0);
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        caption.VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { caption, face }
        };
    }

    private static Grid BarRow(TextBlock caption, SysActivityBar bar, TextBlock percent)
    {
        var row = new Grid();
        caption.Width = 36;
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(caption);
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        Grid.SetColumn(percent, 2);
        row.Children.Add(percent);
        return row;
    }

    private static Grid Pair(UIElement left, UIElement right)
    {
        var row = new Grid
        {
            Width = DockWidth,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(left);
        Grid.SetColumn(right, 1);
        row.Children.Add(right);
        return row;
    }

    private static double MeasureDockWidth()
    {
        var caption = Math.Max(
            MeasureSample(FontWeights.Normal, DockTokens.SecondaryFontSize, "CPU"),
            Math.Max(
                MeasureSample(FontWeights.Normal, DockTokens.SecondaryFontSize, UiText.Memory),
                MeasureSample(FontWeights.Normal, DockTokens.SecondaryFontSize, UiText.MemoryShort)));
        var rings = (caption + CaptionRingGap + DockRing) * 2 + MeterGap;
        var numbers = MeasureSample(FontWeights.SemiBold, 18, "100%") * 2 + MeterGap;
        return Math.Max(rings, numbers);
    }

    private static double MeasureSample(FontWeight weight, double size, string sample)
    {
        var text = new FormattedText(
            sample,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(WidgetFonts.Ui, FontStyles.Normal, weight, FontStretches.Normal),
            size,
            Brushes.Black,
            1.0);
        return Math.Ceiling(text.WidthIncludingTrailingWhitespace);
    }

    private void Render(PerfSnapshot snapshot)
    {
        var cpu = SysActivityFormat.Percent(snapshot.CpuPercent, snapshot.CpuAvailable);
        var mem = SysActivityFormat.Percent(snapshot.MemoryPercent, snapshot.MemoryAvailable);
        _cpuPercent.Text = cpu;
        _memPercent.Text = mem;
        _cpuRingPercent.Text = cpu;
        _memRingPercent.Text = mem;
        _panelCpuPercent.Text = cpu;
        _panelMemPercent.Text = mem;
        _cpuRing.Progress = SysActivityFormat.Fraction(snapshot.CpuPercent, snapshot.CpuAvailable);
        _memRing.Progress = SysActivityFormat.Fraction(snapshot.MemoryPercent, snapshot.MemoryAvailable);
        _panelCpuRing.Progress = _cpuRing.Progress;
        _panelMemRing.Progress = _memRing.Progress;
        _cpuBar.Progress = _cpuRing.Progress;
        _memBar.Progress = _memRing.Progress;
        _usedValue.Text = SysActivityFormat.MemoryUsed(snapshot.MemoryUsedBytes, snapshot.MemoryTotalBytes);
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.SysActivity));
        var customize = HeaderGlyph("\uE8FD", OpenCustomize);
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        var meters = new Grid { Margin = new Thickness(0, 12, 0, 8) };
        meters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        meters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cpu = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new Grid
                {
                    Width = FlyoutRing,
                    Height = FlyoutRing,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { _panelCpuRing, _panelCpuPercent }
                },
                _panelCpuCaption
            }
        };
        var mem = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new Grid
                {
                    Width = FlyoutRing,
                    Height = FlyoutRing,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { _panelMemRing, _panelMemPercent }
                },
                _panelMemCaption
            }
        };
        Grid.SetColumn(mem, 1);
        meters.Children.Add(cpu);
        meters.Children.Add(mem);
        var divider = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 12),
            Background = TokenBrushes.From(0x22000000),
            Opacity = 0.6
        };
        _usedLabel.Margin = new Thickness(0);
        var used = new Grid();
        used.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        used.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        used.Children.Add(_usedLabel);
        Grid.SetColumn(_usedValue, 1);
        used.Children.Add(_usedValue);
        var page = new StackPanel { Children = { header, meters, divider, used } };
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
                HeaderGlyph("\uE76B", ShowEditor, 12),
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
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, _customizeTitle }
        }, close);

        _layoutCaption = HeaderLabel(UiText.Layout);
        _layoutCaption.Opacity = 0.62;
        _layoutCaption.Margin = new Thickness(0, 8, 0, 8);
        var layouts = new StackPanel();
        foreach (var layout in SysActivityLayouts.All)
        {
            layouts.Children.Add(LayoutRow(layout));
        }

        var page = new StackPanel { Children = { header, _layoutCaption, layouts } };
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
            FontFamily = layout == SysActivityLayouts.Numbers ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Width = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var title = HeaderLabel(SysActivityFormat.LayoutTitle(layout));
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
        _settings = _settings with { Layout = SysActivityLayouts.Normalize(layout) };
        Persist();
        PaintCustomize();
        RebuildTile();
        Render(_perf.Current);
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
        SysActivityLayouts.Numbers => "#",
        SysActivityLayouts.Rings => "\uEA3A",
        _ => "\uE9D2"
    };

    private static Ellipse Dot() => new()
    {
        Width = 6,
        Height = 6,
        Margin = new Thickness(0, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static uint Fade(uint argb, byte alpha) => (argb & 0x00FFFFFFu) | ((uint)alpha << 24);

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
