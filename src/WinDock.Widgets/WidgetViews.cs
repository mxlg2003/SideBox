using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class WidgetViewRegistry
{
    public UIElement Create(DockItem item, IProviderHub hub)
    {
        return item.WidgetId switch
        {
            BuiltInWidgets.Clock => new ClockWidget(hub.Clock),
            BuiltInWidgets.Notes => new NotesWidget(hub.Notes, item.Id),
            BuiltInWidgets.Power => new PowerWidget(hub.Power),
            BuiltInWidgets.SysActivity => new SysActivityWidget(hub.Perf),
            BuiltInWidgets.Network => new NetworkWidget(hub.Network),
            BuiltInWidgets.NowPlaying => new NowPlayingWidget(hub.Media),
            BuiltInWidgets.BingWallpaper => new BingWallpaperWidget(hub.Bing),
            BuiltInWidgets.Stopwatch => new StopwatchWidget(),
            BuiltInWidgets.Countdown => new CountdownWidget(),
            BuiltInWidgets.FocusTimer => new FocusTimerWidget(),
            BuiltInWidgets.TimeProgress => new TimeProgressWidget(hub.Clock),
            _ => new PlaceholderWidget(BuiltInWidgets.DisplayName(item.WidgetId ?? string.Empty))
        };
    }
}

public static class WidgetChrome
{
    internal const string Tag = "wd-chrome";
    internal const string NotesTag = "wd-chrome-notes";
    internal const string FlyoutTag = "wd-chrome-flyout";
    internal const string FlyoutHostTag = "wd-chrome-flyout-host";

    public static Border Wrap(UIElement content, double minWidth = 64, string tag = Tag)
    {
        var border = new Border
        {
            Tag = tag,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Padding = new Thickness(DockTokens.TilePaddingX, DockTokens.TilePaddingY, DockTokens.TilePaddingX, DockTokens.TilePaddingY),
            MinWidth = minWidth,
            Height = DockTokens.TileHeight,
            MinHeight = DockTokens.TileHeight,
            MaxHeight = DockTokens.TileHeight,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true,
            Cursor = Cursors.Hand,
            Child = content
        };
        ApplyTheme(border, false);
        return border;
    }

    public static Border Flyout(UIElement content, double minWidth = 280)
    {
        var card = new Border
        {
            Tag = FlyoutTag,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 12, 16, 20),
            MinWidth = minWidth,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Child = content
        };
        Round(card, DockTokens.IslandCornerRadius);
        ApplyTheme(card, false);
        return Shadow(card);
    }

    public static Border Shadow(UIElement child) => new()
    {
        Tag = FlyoutHostTag,
        Background = Brushes.Transparent,
        SnapsToDevicePixels = true,
        Effect = new DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.18,
            Color = Colors.Black
        },
        Child = child
    };

    public static void Round(Border border, double radius)
    {
        border.CornerRadius = new CornerRadius(radius);
        void ClipToRadius()
        {
            var width = border.ActualWidth;
            var height = border.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            border.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        }

        border.SizeChanged += (_, _) => ClipToRadius();
        ClipToRadius();
    }

    public static TextBlock Glyph(string glyph, uint? tint = null) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize = DockTokens.GlyphSize,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
        Foreground = tint is uint color ? TokenBrushes.From(color) : TokenBrushes.From(DockTokens.Dark.Text)
    };

    public static StackPanel Row(UIElement glyph, UIElement body) => new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { glyph, body }
    };

    public static void ApplyTree(DependencyObject root, bool isLight, DockBackdrop backdrop = DockBackdrop.Mica)
    {
        if (root is IWidgetThemeAware themed)
        {
            themed.ApplyTheme(isLight);
        }

        if (root is Border border && border.Tag is string tag && tag.StartsWith("wd-chrome", StringComparison.Ordinal))
        {
            ApplyTheme(border, isLight, backdrop);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            ApplyTree(VisualTreeHelper.GetChild(root, i), isLight, backdrop);
        }

        if (root is Decorator decorator && decorator.Child is not null)
        {
            ApplyTree(decorator.Child, isLight, backdrop);
        }
        else if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is DependencyObject d)
                {
                    ApplyTree(d, isLight, backdrop);
                }
            }
        }
        else if (root is ContentControl control && control.Content is DependencyObject inner)
        {
            ApplyTree(inner, isLight, backdrop);
        }
        else if (root is Popup popup && popup.Child is DependencyObject popupChild)
        {
            ApplyTree(popupChild, isLight, backdrop);
        }
    }

    public static void ApplyTheme(Border border, bool isLight, DockBackdrop backdrop = DockBackdrop.Mica)
    {
        if (Equals(border.Tag, NotesTag))
        {
            // Paper fill and ink are owned by NotesWidget; theme text would wash them out.
            return;
        }

        if (Equals(border.Tag, FlyoutHostTag))
        {
            // Flyout host stays clear for rounded clip.
        }
        else if (Equals(border.Tag, FlyoutTag))
        {
            border.Background = TokenBrushes.From(DockTokens.ResolveMenuFill(isLight, true, backdrop));
        }
        else
        {
            border.Background = TokenBrushes.From(DockTokens.ResolveTileFill(isLight, backdrop));
        }

        SetForeground(border, isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
    }

    private static void SetForeground(DependencyObject root, uint argb)
    {
        if (root is FrameworkElement element &&
            (Equals(element.Tag, NotesTag) || element.Tag as string is "tint" or "ink"))
        {
            return;
        }

        var brush = TokenBrushes.From(argb);
        if (root is Control control)
        {
            control.Foreground = brush;
        }

        if (root is TextBlock text)
        {
            text.Foreground = brush;
        }

        if (root is AnalogClockFace face)
        {
            face.Stroke = brush;
        }

        if (root is TextBox box)
        {
            box.CaretBrush = brush;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            SetForeground(VisualTreeHelper.GetChild(root, i), argb);
        }

        if (root is Decorator decorator && decorator.Child is not null)
        {
            SetForeground(decorator.Child, argb);
        }
        else if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is DependencyObject d)
                {
                    SetForeground(d, argb);
                }
            }
        }
        else if (root is ContentControl content && content.Content is DependencyObject inner)
        {
            SetForeground(inner, argb);
        }
        else if (root is Popup popup && popup.Child is DependencyObject popupChild)
        {
            SetForeground(popupChild, argb);
        }
    }
}

internal static class TokenBrushes
{
    public static SolidColorBrush From(uint argb)
    {
        var (a, r, g, b) = DockTokens.Split(argb);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}

internal static class WidgetFonts
{
    public static readonly FontFamily Ui = new("Segoe UI Variable, Segoe UI");
    public static readonly FontFamily Notes = new("Candara, Microsoft YaHei UI, Segoe UI");
}

internal static class WidgetCopy
{
    public static TextBlock Primary() => new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = DockTokens.PrimaryFontSize,
        FontWeight = FontWeights.SemiBold
    };

    public static TextBlock Secondary() => new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = DockTokens.SecondaryFontSize,
        Opacity = 0.62,
        Margin = new Thickness(0, 1, 0, 0)
    };
}

public sealed class PlaceholderWidget : UserControl
{
    public PlaceholderWidget(string title)
    {
        Content = WidgetChrome.Wrap(new TextBlock
        {
            Text = title,
            FontSize = DockTokens.PrimaryFontSize,
            Opacity = 0.7
        });
        Opacity = 0.55;
        IsHitTestVisible = false;
    }
}

internal static class WidgetMode
{
    public static Visibility Extra(WidgetDensity density) =>
        density == WidgetDensity.Expanded ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class ClockWidget : UserControl, IWidgetModeAware, IWidgetOpenable, IWidgetCustomizable, IClockConfigurable
{
    private readonly IClockProvider _clock;
    private readonly AnalogClockFace _tileFace = new(DockTokens.ClockFaceSize)
    {
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly AnalogClockFace _panelFace = new(DockTokens.ClockPanelFaceSize)
    {
        Margin = new Thickness(0, 8, 0, 14),
        HorizontalAlignment = HorizontalAlignment.Center
    };
    private readonly TextBlock _time = WidgetCopy.Primary();
    private readonly TextBlock _date = WidgetCopy.Secondary();
    private readonly TextBlock _second = WidgetCopy.Secondary();
    private readonly TextBlock _panelTime = WidgetCopy.Primary();
    private readonly TextBlock _panelDate = WidgetCopy.Primary();
    private readonly TextBlock _panelZone = WidgetCopy.Secondary();
    private readonly TextBlock _panelSecond = WidgetCopy.Secondary();
    private readonly StackPanel _copy;
    private readonly Border _tile;
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private string? _timeZoneId;
    private readonly Popup _customize;

    public event EventHandler<string>? PaperChanged;

    public event EventHandler<string?>? TimeZoneChanged;

    public ClockWidget(IClockProvider clock)
    {
        _clock = clock;
        _time.FontSize = 17;
        _date.Margin = new Thickness(0);
        _copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _time, _date, _second }
        };
        _panelTime.FontSize = 38;
        _panelTime.FontWeight = FontWeights.SemiBold;
        _panelTime.HorizontalAlignment = HorizontalAlignment.Center;
        _panelDate.FontSize = 14;
        _panelDate.FontWeight = FontWeights.SemiBold;
        _panelDate.HorizontalAlignment = HorizontalAlignment.Center;
        _panelDate.Margin = new Thickness(0, 2, 0, 0);
        _panelZone.FontSize = 12;
        _panelZone.HorizontalAlignment = HorizontalAlignment.Center;
        _panelZone.Margin = new Thickness(0, 6, 0, 0);
        _panelSecond.FontSize = 12;
        _panelSecond.HorizontalAlignment = HorizontalAlignment.Center;
        _panelSecond.Margin = new Thickness(0, 4, 0, 0);
        _second.FontSize = 11;
        _second.Margin = new Thickness(0, 1, 0, 0);

        _tile = WidgetChrome.Wrap(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _tileFace, _copy }
        }, 0);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        var title = WidgetCopy.Secondary();
        title.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Clock);
        title.FontSize = 12;
        title.VerticalAlignment = VerticalAlignment.Center;
        var close = new TextBlock
        {
            Text = "\u00D7",
            FontSize = 18,
            Opacity = 0.45,
            Padding = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(title);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        var panel = WidgetChrome.Flyout(new StackPanel
        {
            Children =
            {
                header,
                _panelFace,
                _panelTime,
                _panelDate,
                _panelZone,
                _panelSecond
            }
        });
        panel.RenderTransformOrigin = new Point(0.5, 0);
        panel.RenderTransform = new ScaleTransform(1, 1);

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = panel
        };
        WidgetFlyout.Attach(_popup);
        close.MouseLeftButtonUp += (_, e) =>
        {
            _popup.IsOpen = false;
            e.Handled = true;
        };
        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { Tick(); _timer.Start(); };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        _customize = BuildCustomizePopup();
        root.Children.Add(_customize);
        Content = root;
        ApplyDensity(_density);
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void OpenCustomize()
    {
        _customize.IsOpen = true;
    }

    public void ApplyTimeZone(string? timeZoneId)
    {
        _timeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId;
        Tick();
        InvalidateMeasure();
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        var expanded = density == WidgetDensity.Expanded;
        _tileFace.Visibility = WidgetMode.Extra(density);
        _tileFace.Width = expanded ? DockTokens.ClockFaceSize : 0;
        _tileFace.Margin = expanded ? new Thickness(0, 0, 8, 0) : new Thickness(0);
        var align = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        var textAlign = expanded ? TextAlignment.Left : TextAlignment.Center;
        _copy.HorizontalAlignment = align;
        _time.HorizontalAlignment = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _date.HorizontalAlignment = expanded ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _time.TextAlignment = textAlign;
        _date.TextAlignment = textAlign;
        Tick();
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    public void Open()
    {
        Tick();
        _popup.IsOpen = true;
    }

    private void Tick()
    {
        var now = _clock.LocalNow;
        _time.Text = ClockFormat.Time(now);
        _date.Text = _density == WidgetDensity.Expanded
            ? ClockFormat.ExpandedDate(now)
            : ClockFormat.CompactDate(now);
        _tileFace.Time = now;
        _panelFace.Time = now;
        _panelTime.Text = ClockFormat.TimeWithSeconds(now);
        _panelDate.Text = ClockFormat.FullDate(now);
        _panelZone.Text = ClockFormat.ZoneLine(TimeZoneInfo.Local, now);
        var zone = ClockFormat.Resolve(_timeZoneId);
        if (zone is null)
        {
            _second.Visibility = Visibility.Collapsed;
            _panelSecond.Visibility = Visibility.Collapsed;
            return;
        }

        var there = ClockFormat.ConvertLocal(now, zone);
        _second.Text = $"{ClockFormat.Time(there)} · {ClockFormat.PlaceName(zone)}";
        _second.Visibility = _density == WidgetDensity.Expanded ? Visibility.Visible : Visibility.Collapsed;
        _panelSecond.Text = ClockFormat.ZoneLine(zone, there);
        _panelSecond.Visibility = Visibility.Visible;
    }

    private Popup BuildCustomizePopup()
    {
        var box = new ComboBox { Width = 220, Margin = new Thickness(0, 8, 0, 0) };
        box.Items.Add(new ComboBoxItem { Content = "仅本地时间", Tag = "" });
        foreach (var choice in ClockFormat.ZoneChoices)
        {
            box.Items.Add(new ComboBoxItem { Content = choice.Place, Tag = choice.Id });
        }

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var id = item.Tag as string;
            TimeZoneChanged?.Invoke(this, string.IsNullOrWhiteSpace(id) ? null : id);
        };

        var panel = WidgetChrome.Flyout(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "第二时区", FontSize = 13 },
                box
            }
        }, 260);
        var popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = panel
        };
        WidgetFlyout.Attach(popup);
        popup.Opened += (_, _) =>
        {
            foreach (var item in box.Items.OfType<ComboBoxItem>())
            {
                var tag = item.Tag as string;
                item.IsSelected = string.Equals(tag, _timeZoneId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            if (box.SelectedItem is null && box.Items.Count > 0)
            {
                box.SelectedIndex = 0;
            }
        };
        return popup;
    }
}

public interface IClockConfigurable
{
    void ApplyTimeZone(string? timeZoneId);

    event EventHandler<string?>? TimeZoneChanged;
}

public interface IKeepAliveWhileRunning
{
    bool IsAlive { get; }
}

public interface IPrivacyAware
{
    void OnPrivacyChanged();
}

public interface IIslandLayoutAware
{
    event EventHandler? IslandLayoutChanged;
}

public interface IWidgetOpenable
{
    void Open();
}

public interface IWidgetCustomizable
{
    void OpenCustomize();

    void ApplyPaper(string? paper);

    event EventHandler<string>? PaperChanged;
}

public sealed record NotesHistoryItem(string Id, string Preview, string Paper);

public interface INotesHistory
{
    void ApplyHistory(IReadOnlyList<NotesHistoryItem> items);

    event EventHandler<string>? RestoreRequested;

    event EventHandler<string>? DiscardRequested;
}

public interface IWidgetModeAware
{
    void ApplyDensity(WidgetDensity density);
}

public interface IFocusTimerConfigurable
{
    void ApplySettings(FocusTimerSettings settings);

    event EventHandler<FocusTimerSettings>? SettingsChanged;
}

public interface ICountdownConfigurable
{
    void ApplySettings(CountdownSettings settings);

    event EventHandler<CountdownSettings>? SettingsChanged;
}

public interface IPowerWidgetConfigurable
{
    void ApplySettings(PowerWidgetSettings settings);

    event EventHandler<PowerWidgetSettings>? SettingsChanged;
}

public interface ITimeProgressConfigurable
{
    void ApplySettings(TimeProgressSettings settings);

    event EventHandler<TimeProgressSettings>? SettingsChanged;
}

public interface ISysActivityConfigurable
{
    void ApplySettings(SysActivitySettings settings);

    event EventHandler<SysActivitySettings>? SettingsChanged;
}

public interface INetworkConfigurable
{
    void ApplySettings(NetworkWidgetSettings settings);

    event EventHandler<NetworkWidgetSettings>? SettingsChanged;
}

public interface IWidgetThemeAware
{
    void ApplyTheme(bool isLight);
}

public sealed class NowPlayingWidget : UserControl, IWidgetModeAware, IPrivacyAware
{
    private const string GlyphPrevious = "\uE892";
    private const string GlyphPlay = "\uE768";
    private const string GlyphPause = "\uE769";
    private const string GlyphNext = "\uE893";
    private const string GlyphMusic = "\uE8D6";

    private readonly IMediaProvider _media;
    private readonly TextBlock _title = WidgetCopy.Primary();
    private readonly TextBlock _artist = WidgetCopy.Secondary();
    private readonly TextBlock _artGlyph = WidgetChrome.Glyph(GlyphMusic);
    private readonly Image _artImage = new() { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
    private readonly TextBlock _elapsed = WidgetCopy.Secondary();
    private readonly TextBlock _remaining = WidgetCopy.Secondary();
    private readonly Border _progressFill = new()
    {
        Height = 3,
        CornerRadius = new CornerRadius(2),
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = TokenBrushes.From(0x99000000)
    };
    private readonly Grid _progressTrack;
    private readonly StackPanel _progressRow;
    private readonly TextBlock _inlinePlayGlyph;
    private readonly TextBlock _pillPlayGlyph;
    private readonly StackPanel _controls;
    private readonly Border _playPill;
    private readonly StackPanel _detail;
    private readonly Border _tile;
    private readonly EventHandler _changed;
    private WidgetDensity _density = WidgetDensity.Expanded;

    public NowPlayingWidget(IMediaProvider media)
    {
        _media = media;
        _title.FontSize = 12;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.MaxWidth = 110;
        _title.VerticalAlignment = VerticalAlignment.Center;
        _artist.FontSize = 10;
        _artist.Margin = new Thickness(0);
        _artist.TextTrimming = TextTrimming.CharacterEllipsis;
        _artist.MaxWidth = 128;
        _elapsed.FontSize = 10;
        _elapsed.Margin = new Thickness(0);
        _remaining.FontSize = 10;
        _remaining.Margin = new Thickness(0);
        _artGlyph.Tag = "tint";
        _artGlyph.FontSize = 14;
        _artGlyph.Margin = new Thickness(0);
        _inlinePlayGlyph = MediaGlyph(GlyphPlay, 11);
        _pillPlayGlyph = MediaGlyph(GlyphPlay, 14);
        var prev = TransportButton(GlyphPrevious, "上一首", () => _ = _media.TrySkipPreviousAsync());
        var inlinePlay = TransportButton(GlyphPlay, "播放/暂停", () => _ = TogglePlayPauseAsync(), _inlinePlayGlyph);
        var next = TransportButton(GlyphNext, "下一首", () => _ = _media.TrySkipNextAsync());
        var art = new Grid
        {
            Width = DockTokens.ArtSize,
            Height = DockTokens.ArtSize,
            Margin = new Thickness(0, 0, 8, 0),
            Clip = new RectangleGeometry(
                new Rect(0, 0, DockTokens.ArtSize, DockTokens.ArtSize),
                DockTokens.ArtCornerRadius,
                DockTokens.ArtCornerRadius)
        };
        art.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = TokenBrushes.From(0x33000000),
            Child = _artGlyph
        });
        art.Children.Add(_artImage);
        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { prev, inlinePlay, next }
        };
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(_title);
        Grid.SetColumn(_controls, 1);
        titleRow.Children.Add(_controls);

        _progressTrack = new Grid
        {
            Width = 72,
            Height = 4,
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Border
                {
                    Height = 3,
                    CornerRadius = new CornerRadius(2),
                    Background = TokenBrushes.From(0x33000000)
                },
                _progressFill
            }
        };
        _progressTrack.SizeChanged += (_, _) => RenderProgress(_media.Current);
        _progressRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0),
            Children = { _elapsed, _progressTrack, _remaining }
        };

        _playPill = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = TokenBrushes.From(0x14000000),
            Cursor = Cursors.Hand,
            Child = _pillPlayGlyph,
            ToolTip = "播放/暂停"
        };
        _playPill.MouseLeftButtonUp += (_, e) =>
        {
            _ = TogglePlayPauseAsync();
            e.Handled = true;
        };

        var info = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleRow, _artist, _progressRow }
        };
        _detail = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { art, info }
        };
        _changed = UiPump.Coalesce(Dispatcher, Render);
        _tile = WidgetChrome.Wrap(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _detail, _playPill }
        }, 0);
        Content = _tile;
        Loaded += (_, _) =>
        {
            _media.Changed += _changed;
            Render();
        };
        Unloaded += (_, _) => _media.Changed -= _changed;
        ApplyDensity(_density);
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        var expanded = density == WidgetDensity.Expanded;
        _detail.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _playPill.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        Render();
        _tile.InvalidateMeasure();
    }

    public void OnPrivacyChanged() => Render();

    private async Task TogglePlayPauseAsync()
    {
        var wasPlaying = _media.Current?.IsPlaying == true;
        var next = wasPlaying ? GlyphPlay : GlyphPause;
        _inlinePlayGlyph.Text = next;
        _pillPlayGlyph.Text = next;
        _playPill.ToolTip = wasPlaying ? "播放" : "暂停";
        _inlinePlayGlyph.ToolTip = _playPill.ToolTip;
        await _media.TryPlayPauseAsync();
        if (Dispatcher.CheckAccess())
        {
            Render();
            return;
        }

        _ = Dispatcher.BeginInvoke(Render, DispatcherPriority.Background);
    }

    private void Render()
    {
        var session = _media.Current;
        if (session is null)
        {
            _title.Text = "未在播放";
            _artist.Text = "网易云 / PotPlayer 等桌面版常常不提供系统媒体控制";
            _artist.TextWrapping = TextWrapping.Wrap;
            _artist.MaxWidth = 160;
            _title.ToolTip = "网易云 / PotPlayer 等桌面版常常不提供系统媒体控制，按钮会改用播放器自己的命令。";
            _inlinePlayGlyph.Text = GlyphPlay;
            _pillPlayGlyph.Text = GlyphPlay;
            _playPill.ToolTip = "没有可控制的播放会话";
            _playPill.IsEnabled = false;
            _controls.IsEnabled = false;
            _artImage.Source = null;
            _artImage.Visibility = Visibility.Collapsed;
            RenderProgress(null);
            return;
        }

        _playPill.IsEnabled = true;
        _controls.IsEnabled = true;

        _title.Text = string.IsNullOrWhiteSpace(session.Title)
            ? (session.IsPlaying ? "播放中" : "已暂停")
            : session.Title;
        _artist.Text = session.Artist ?? string.Empty;
        var glyph = session.IsPlaying ? GlyphPause : GlyphPlay;
        _inlinePlayGlyph.Text = glyph;
        _pillPlayGlyph.Text = glyph;
        _playPill.ToolTip = session.IsPlaying ? "暂停" : "播放";
        _artImage.Source = ImageThumbCache.Load(session.ThumbnailPath, 64);
        _artImage.Visibility = _artImage.Source is null ? Visibility.Collapsed : Visibility.Visible;
        RenderProgress(session);
    }

    private void RenderProgress(MediaSessionSnapshot? session)
    {
        var duration = session?.Duration ?? TimeSpan.Zero;
        var position = session?.Position ?? TimeSpan.Zero;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (duration > TimeSpan.Zero && position > duration)
        {
            position = duration;
        }

        _elapsed.Text = FormatClock(position);
        _remaining.Text = duration > TimeSpan.Zero ? FormatClock(duration) : "--:--";
        var ratio = duration > TimeSpan.Zero ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
        _progressFill.Width = Math.Max(0, _progressTrack.ActualWidth * ratio);
    }

    private static string FormatClock(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");

    private static Border TransportButton(string glyph, string tooltip, Action click, TextBlock? content = null)
    {
        var icon = content ?? MediaGlyph(glyph, 11);
        var button = new Border
        {
            Width = 22,
            Height = 22,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = icon
        };
        button.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return button;
    }

    private static TextBlock MediaGlyph(string glyph, double size) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
}

public sealed class BingWallpaperWidget : UserControl, IWidgetModeAware
{
    private const string GlyphPhoto = "\uE91B";
    private const string GlyphPrevious = "\uE892";
    private const string GlyphApply = "\uE8FB";
    private const string GlyphNext = "\uE893";
    private const double CopyWidth = 72;
    private const double ThumbSize = 38;
    private const double ThumbRadius = 8;

    private readonly IBingWallpaperProvider _bing;
    private readonly TextBlock _artGlyph = WidgetChrome.Glyph(GlyphPhoto, DockTokens.Bing);
    private readonly Border _thumb;
    private readonly TextBlock _title = WidgetCopy.Primary();
    private readonly TextBlock _detail = WidgetCopy.Secondary();
    private readonly StackPanel _copy;
    private readonly Button _prev;
    private readonly Button _apply;
    private readonly Button _next;
    private readonly StackPanel _controls;
    private readonly Border _tile;
    private readonly EventHandler _changed;
    private WidgetDensity _density = WidgetDensity.Expanded;

    public BingWallpaperWidget(IBingWallpaperProvider bing)
    {
        _bing = bing;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.Width = CopyWidth;
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _detail.TextWrapping = TextWrapping.NoWrap;
        _detail.Width = CopyWidth;
        _artGlyph.Tag = "tint";
        _artGlyph.FontSize = 13;
        _artGlyph.Margin = new Thickness(0);
        _artGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _artGlyph.VerticalAlignment = VerticalAlignment.Center;
        _prev = IconButton(GlyphPrevious, "前一天", () => _ = _bing.SelectRelativeAsync(-1));
        _apply = IconButton(GlyphApply, "设为桌面", () => _ = _bing.TryApplyCurrentAsync());
        _next = IconButton(GlyphNext, "后一天", () => _ = _bing.SelectRelativeAsync(1));
        _thumb = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(ThumbRadius),
            Margin = new Thickness(0, 0, 8, 0),
            SnapsToDevicePixels = true,
            Background = TokenBrushes.From(0x33FFFFFF),
            Child = _artGlyph
        };
        _copy = new StackPanel
        {
            Width = CopyWidth,
            MinWidth = CopyWidth,
            MaxWidth = CopyWidth,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _title, _detail }
        };
        _changed = UiPump.Coalesce(Dispatcher, Render);
        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _prev, _apply, _next }
        };
        _tile = WidgetChrome.Wrap(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _thumb, _copy, _controls }
        }, 0);
        Content = _tile;
        MouseLeftButtonUp += (_, e) =>
        {
            if (_bing.Current.Selected is null)
            {
                _ = _bing.RefreshAsync();
                e.Handled = true;
            }
        };
        Loaded += (_, _) =>
        {
            _bing.Changed += _changed;
            Render();
            _ = _bing.RefreshAsync();
        };
        Unloaded += (_, _) => _bing.Changed -= _changed;
        ApplyDensity(_density);
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        var compact = density != WidgetDensity.Expanded;
        _copy.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _controls.Margin = compact ? new Thickness(0) : new Thickness(8, 0, 0, 0);
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void Render()
    {
        var snapshot = _bing.Current;
        var selected = snapshot.Selected;
        _prev.IsEnabled = !snapshot.IsBusy;
        _next.IsEnabled = !snapshot.IsBusy;
        _apply.IsEnabled = !snapshot.IsBusy && selected is not null;
        if (selected is null)
        {
            _title.Text = "Bing 壁纸";
            _detail.Text = snapshot.Error ?? (snapshot.IsBusy ? "加载中…" : "点击重试");
            ShowThumb(null);
            return;
        }

        _title.Text = selected.Title;
        _detail.Text = snapshot.Error
                       ?? (snapshot.AppliedKey == selected.Key
                           ? "已设为桌面"
                           : snapshot.IsBusy ? "加载中…" : BingWallpaperArchive.FormatDate(selected.StartDate));
        ToolTip = selected.Copyright;
        ShowThumb(ImageThumbCache.Load(snapshot.LocalPath, 96));
    }

    private void ShowThumb(ImageSource? image)
    {
        if (image is null)
        {
            _thumb.Background = TokenBrushes.From(0x33FFFFFF);
            _thumb.Child = _artGlyph;
            return;
        }

        var brush = new ImageBrush(image)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
        brush.Freeze();
        _thumb.Background = brush;
        _thumb.Child = null;
    }

    private static Button IconButton(string glyph, string tooltip, Action click)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 32,
            Height = 32,
            MinWidth = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 2, 0),
            BorderThickness = new Thickness(0),
            Background = TokenBrushes.From(0x33FFFFFF),
            Cursor = Cursors.Hand,
            ToolTip = tooltip
        };
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => click();
        return button;
    }
}

internal static class ImageThumbCache
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Load(string? path, int decodeWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|" + decodeWidth;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            lock (Cache)
            {
                Cache[key] = bitmap;
            }

            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
