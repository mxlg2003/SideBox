using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class BingWallpaperWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IBingWallpaperConfigurable, IWidgetThemeAware, IWidgetModeAware, IDockEdgeAware, ILocalizable, IWidgetDetachable
{
    private const string GlyphPhoto = PhosphorIcon.Image;
    private const string GlyphPrevious = PhosphorIcon.CaretLeft;
    private const string GlyphApply = PhosphorIcon.Desktop;
    private const string GlyphNext = PhosphorIcon.CaretRight;
    private const double CopyWidth = 72;
    private const double ThumbSize = 38;
    private const double ThumbRadius = 8;
    private const double ContentWidth = 348;

    private readonly IBingWallpaperProvider _bing;
    private readonly Func<Task>? _applyDesktop;

    // --- Dock Tile Elements ---
    private readonly PhosphorGlyph _artGlyph = WidgetChrome.Glyph(GlyphPhoto, DockTokens.Bing);
    private readonly Border _thumb;
    private readonly TextBlock _title = WidgetCopy.Primary();
    private readonly TextBlock _detail = WidgetCopy.Secondary();
    private readonly StackPanel _copy;
    private readonly Border _prev;
    private readonly Border _apply;
    private readonly Border _restore;
    private readonly Border _next;
    private readonly StackPanel _controls;
    private readonly Border _tile;

    // --- Flyout Elements ---
    private readonly Popup _popup;
    private readonly Border _panel;
    private readonly TextBlock _flyoutTitle = WidgetCopy.Primary();
    private readonly TextBlock _flyoutDateBadge = WidgetCopy.Secondary();
    private readonly Border _flyoutDateBadgeBorder;
    private readonly PhosphorGlyph _refreshBtn;
    private readonly Border _closeBtn;

    // Hero Big Image Preview
    private readonly Border _heroCard;
    private readonly Border _heroImage;
    private readonly StackPanel _heroPlaceholder;
    private readonly TextBlock _heroPlaceholderText = WidgetCopy.Secondary();
    private readonly Border _heroPrevBtn;
    private readonly Border _heroNextBtn;
    private readonly Border _heroOpenOriginalBtn;
    private readonly TextBlock _heroOpenOriginalLabel = new();

    // 8-Day Carousel Strip
    private readonly StackPanel _thumbStrip = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 10, 0, 8)
    };

    // Information Section
    private readonly TextBlock _infoTitle = WidgetCopy.Primary();
    private readonly TextBlock _infoCopyright = WidgetCopy.Secondary();

    // Action Buttons
    private readonly Border _applyActionBtn;
    private readonly PhosphorGlyph _applyActionIcon;
    private readonly TextBlock _applyActionLabel;
    private readonly Border _restoreActionBtn;
    private readonly TextBlock _restoreActionLabel;

    // Auto Daily Wallpaper Settings
    private BingWallpaperSettings _settings = BingWallpaperSettings.Default;
    private readonly Border _autoDailyCard;
    private readonly TextBlock _autoDailyTitle = WidgetCopy.Primary();
    private readonly TextBlock _autoDailyHint = WidgetCopy.Secondary();
    private readonly Border _autoDailyToggleTrack;
    private readonly Border _autoDailyToggleThumb;

    private readonly EventHandler _changed;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private bool _side;
    private bool _isLight;

    public BingWallpaperWidget(IBingWallpaperProvider bing, Func<Task>? applyDesktop = null)
    {
        _bing = bing;
        _applyDesktop = applyDesktop;

        // Dock Copy
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.Width = CopyWidth;
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _detail.TextWrapping = TextWrapping.NoWrap;
        _detail.Width = CopyWidth;

        _artGlyph.Tag = "tint";
        _artGlyph.IconSize = 13;
        _artGlyph.Margin = new Thickness(0);
        _artGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _artGlyph.VerticalAlignment = VerticalAlignment.Center;

        _prev = IconButton(GlyphPrevious, UiText.PreviousDay, () => _ = _bing.SelectRelativeAsync(-1));
        _apply = IconButton(GlyphApply, UiText.SetAsDesktop, () => _ = ApplyDesktopAsync());
        _restore = IconButton(PhosphorIcon.ArrowCounterClockwise, UiText.RestorePreviousWallpaper, () => _ = RestoreDesktopAsync());
        _next = IconButton(GlyphNext, UiText.NextDay, () => _ = _bing.SelectRelativeAsync(1));

        _thumb = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(ThumbRadius),
            Margin = new Thickness(0, 0, 8, 0),
            SnapsToDevicePixels = true,
            Child = _artGlyph
        };
        WidgetChrome.MarkQuietOverlay(_thumb);

        _copy = new StackPanel
        {
            Width = CopyWidth,
            MinWidth = CopyWidth,
            MaxWidth = CopyWidth,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _title, _detail }
        };

        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _prev, _apply, _restore, _next }
        };

        _tile = WidgetChrome.Wrap(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _thumb, _copy, _controls }
        }, 0);

        // --- Build Flyout ---
        _flyoutTitle.Text = UiText.WidgetBing;
        _flyoutTitle.FontSize = 15;
        _flyoutTitle.FontWeight = FontWeights.SemiBold;
        _flyoutTitle.VerticalAlignment = VerticalAlignment.Center;

        _flyoutDateBadge.FontSize = 11;
        _flyoutDateBadge.VerticalAlignment = VerticalAlignment.Center;
        _flyoutDateBadgeBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _flyoutDateBadge
        };
        WidgetChrome.MarkQuietOverlay(_flyoutDateBadgeBorder);

        var headerLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new PhosphorGlyph(PhosphorIcon.Image, DockTokens.FlyoutGlyphSize)
                {
                    Foreground = TokenBrushes.From(DockTokens.Bing),
                    Tag = "tint",
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                },
                _flyoutTitle,
                _flyoutDateBadgeBorder
            }
        };

        _refreshBtn = WidgetChrome.HeaderIcon(
            PhosphorIcon.ArrowsClockwise,
            () => _ = _bing.RefreshAsync(),
            opacity: 0.75);
        _refreshBtn.ToolTip = UiText.Refresh;

        _closeBtn = WidgetChrome.HeaderClose(ClosePopup, 18);

        var headerRow = HeaderRow(headerLeft, _refreshBtn, _closeBtn);

        // Hero Card
        _heroImage = new Border
        {
            Width = ContentWidth,
            Height = DockTokens.BingHeroHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };

        _heroPlaceholder = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new PhosphorGlyph(PhosphorIcon.Image, 32)
                {
                    Foreground = TokenBrushes.From(DockTokens.Bing),
                    Tag = "tint",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                },
                _heroPlaceholderText
            }
        };
        _heroPlaceholderText.FontSize = 13;
        _heroPlaceholderText.HorizontalAlignment = HorizontalAlignment.Center;
        _heroPlaceholderText.Text = UiText.Loading;

        _heroPrevBtn = FloatingCircleButton(PhosphorIcon.CaretLeft, UiText.PreviousDay, () => _ = _bing.SelectRelativeAsync(-1));
        _heroPrevBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _heroPrevBtn.Margin = new Thickness(8, 0, 0, 0);

        _heroNextBtn = FloatingCircleButton(PhosphorIcon.CaretRight, UiText.NextDay, () => _ = _bing.SelectRelativeAsync(1));
        _heroNextBtn.HorizontalAlignment = HorizontalAlignment.Right;
        _heroNextBtn.Margin = new Thickness(0, 0, 8, 0);

        _heroOpenOriginalBtn = FloatingPillButton(PhosphorIcon.ArrowSquareOut, UiText.ViewOriginalShort, UiText.ViewOriginalImage, OpenOriginalUrl, _heroOpenOriginalLabel);
        _heroOpenOriginalBtn.HorizontalAlignment = HorizontalAlignment.Right;
        _heroOpenOriginalBtn.VerticalAlignment = VerticalAlignment.Bottom;
        _heroOpenOriginalBtn.Margin = new Thickness(0, 0, 8, 8);

        var heroOverlayGrid = new Grid
        {
            Width = ContentWidth,
            Height = DockTokens.BingHeroHeight
        };
        heroOverlayGrid.Children.Add(_heroImage);
        heroOverlayGrid.Children.Add(_heroPlaceholder);
        heroOverlayGrid.Children.Add(_heroPrevBtn);
        heroOverlayGrid.Children.Add(_heroNextBtn);
        heroOverlayGrid.Children.Add(_heroOpenOriginalBtn);

        _heroCard = new Border
        {
            Width = ContentWidth,
            Height = DockTokens.BingHeroHeight,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            SnapsToDevicePixels = true,
            ClipToBounds = true,
            Child = heroOverlayGrid
        };
        WidgetChrome.MarkQuietOverlay(_heroCard);

        // Information Section
        _infoTitle.FontSize = 14.5;
        _infoTitle.FontWeight = FontWeights.SemiBold;
        _infoTitle.TextWrapping = TextWrapping.Wrap;
        _infoTitle.Margin = new Thickness(0, 0, 0, 3);

        _infoCopyright.FontSize = 12;
        _infoCopyright.Opacity = 0.75;
        _infoCopyright.TextWrapping = TextWrapping.Wrap;
        _infoCopyright.Margin = new Thickness(0, 0, 0, 10);

        // Action Buttons
        _applyActionIcon = new PhosphorGlyph(PhosphorIcon.Desktop, 14)
        {
            Foreground = Brushes.White,
            Tag = "tint",
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _applyActionLabel = new TextBlock
        {
            Text = UiText.ApplyToDesktop,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        var applyContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _applyActionIcon, _applyActionLabel }
        };

        _applyActionBtn = new Border
        {
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius + 2),
            Background = TokenBrushes.From(DockTokens.Settings.Accent),
            Padding = new Thickness(16, 0, 16, 0),
            Cursor = Cursors.Hand,
            Child = applyContent,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _applyActionBtn.MouseLeftButtonUp += (_, e) =>
        {
            _ = ApplyDesktopAsync();
            e.Handled = true;
        };

        _restoreActionLabel = WidgetCopy.Primary();
        _restoreActionLabel.Text = UiText.RestorePreviousWallpaper;
        _restoreActionLabel.FontSize = 13;
        _restoreActionLabel.VerticalAlignment = VerticalAlignment.Center;

        var restoreContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new PhosphorGlyph(PhosphorIcon.ArrowCounterClockwise, 13)
                {
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                },
                _restoreActionLabel
            }
        };

        _restoreActionBtn = new Border
        {
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius + 2),
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            Child = restoreContent,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        WidgetChrome.MarkQuiet(_restoreActionBtn);
        _restoreActionBtn.MouseLeftButtonUp += (_, e) =>
        {
            _ = RestoreDesktopAsync();
            e.Handled = true;
        };

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _applyActionBtn, _restoreActionBtn }
        };

        // Auto Daily Switch Row
        var autoDailyIcon = new PhosphorGlyph(PhosphorIcon.Clock, 15)
        {
            Foreground = TokenBrushes.From(DockTokens.Bing),
            Tag = "tint",
            Margin = new Thickness(0, 2, 8, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        _autoDailyTitle.Text = UiText.BingAutoDaily;
        _autoDailyTitle.FontSize = 13;
        _autoDailyTitle.FontWeight = FontWeights.SemiBold;

        _autoDailyHint.Text = UiText.BingAutoDailyHint;
        _autoDailyHint.FontSize = 11;
        _autoDailyHint.Opacity = 0.72;
        _autoDailyHint.TextWrapping = TextWrapping.Wrap;
        _autoDailyHint.Margin = new Thickness(0, 2, 0, 0);

        var autoDailyTextStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Width = 248,
            Children = { _autoDailyTitle, _autoDailyHint }
        };

        _autoDailyToggleThumb = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0)
        };

        _autoDailyToggleTrack = new Border
        {
            Width = 36,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _autoDailyToggleThumb
        };

        var autoDailyRow = new DockPanel
        {
            LastChildFill = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_autoDailyToggleTrack, Dock.Right);
        autoDailyRow.Children.Add(_autoDailyToggleTrack);
        autoDailyRow.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { autoDailyIcon, autoDailyTextStack }
        });

        _autoDailyCard = new Border
        {
            Width = ContentWidth,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius + 2),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursors.Hand,
            Child = autoDailyRow
        };
        WidgetChrome.MarkQuietOverlay(_autoDailyCard);

        _autoDailyCard.MouseLeftButtonUp += (_, e) =>
        {
            ToggleAutoDaily();
            e.Handled = true;
        };

        _settings = new BingWallpaperSettings
        {
            AutoApplyDaily = _bing.AutoApplyDaily,
            LastAppliedKey = _bing.LastAppliedKey
        };
        UpdateAutoDailyVisual();

        var flyoutBody = new StackPanel
        {
            Width = ContentWidth,
            Children =
            {
                headerRow,
                _heroCard,
                _thumbStrip,
                _infoTitle,
                _infoCopyright,
                actionRow,
                _autoDailyCard
            }
        };

        _panel = WidgetChrome.Flyout(flyoutBody, DockTokens.BingFlyoutWidth);
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);
        _panel.Focusable = true;

        // Mouse wheel over flyout flips wallpaper
        _panel.PreviewMouseWheel += (_, e) =>
        {
            if (e.Delta > 0)
            {
                _ = _bing.SelectRelativeAsync(-1);
            }
            else if (e.Delta < 0)
            {
                _ = _bing.SelectRelativeAsync(1);
            }
            e.Handled = true;
        };

        // Arrow keys over flyout flip wallpaper
        _panel.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Left)
            {
                _ = _bing.SelectRelativeAsync(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                _ = _bing.SelectRelativeAsync(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ClosePopup();
                e.Handled = true;
            }
        };

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = DockTokens.FlyoutOffset,
            Child = _panel
        };
        WidgetFlyout.Attach(_popup);

        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)_panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        _tile.MouseLeftButtonUp += (_, e) =>
        {
            if (_popup.IsOpen)
            {
                ClosePopup();
            }
            else
            {
                Open();
            }
            e.Handled = true;
        };

        _changed = UiPump.Coalesce(Dispatcher, Render);

        Loaded += (_, _) =>
        {
            _bing.Changed += _changed;
            Render();
            _ = _bing.RefreshAsync();
        };
        Unloaded += (_, _) =>
        {
            _bing.Changed -= _changed;
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ClipToBounds = false;

        ApplyDensity(_density);
    }

    public void Open()
    {
        _popup.IsOpen = true;
        _panel.Focus();
    }

    public void OpenCustomize() => Open();

    public void ApplyPaper(string? paper) { }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<BingWallpaperSettings>? SettingsChanged;

    public void ApplySettings(BingWallpaperSettings settings)
    {
        _settings = BingWallpaperSettings.Normalize(settings);
        _bing.AutoApplyDaily = _settings.AutoApplyDaily;
        UpdateAutoDailyVisual();
    }

    private void ToggleAutoDaily()
    {
        var next = !_settings.AutoApplyDaily;
        _settings = _settings with { AutoApplyDaily = next };
        _bing.AutoApplyDaily = next;
        UpdateAutoDailyVisual();
        SettingsChanged?.Invoke(this, _settings);
        if (next)
        {
            _ = _bing.CheckAndAutoApplyAsync(CancellationToken.None);
        }
    }

    private void UpdateAutoDailyVisual()
    {
        var on = _settings.AutoApplyDaily;
        if (on)
        {
            _autoDailyToggleTrack.Background = TokenBrushes.From(DockTokens.Settings.Accent);
            _autoDailyToggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
            _autoDailyToggleThumb.Background = Brushes.White;
        }
        else
        {
            _autoDailyToggleTrack.Background = TokenBrushes.From(_isLight ? 0x24000000u : 0x33FFFFFFu);
            _autoDailyToggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
            _autoDailyToggleThumb.Background = TokenBrushes.From(_isLight ? 0x88000000u : 0xCCFFFFFFu);
        }

        AutomationProperties.SetName(_autoDailyCard, UiText.BingAutoDaily + (on ? " (开)" : " (关)"));
    }

    public void ClosePopup()
    {
        _popup.IsOpen = false;
    }

    public void Detach()
    {
        _bing.Changed -= _changed;
        _popup.IsOpen = false;
    }

    public void ApplyDockEdge(DockEdge edge) => ArrangeDock(WidgetChrome.IsSide(edge));

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        ArrangeDock(_side);
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        WidgetChrome.ApplyTree(_panel, isLight);
        UpdateAutoDailyVisual();
        Render();
    }

    private void ArrangeDock(bool side)
    {
        _side = side;
        var compact = _density != WidgetDensity.Expanded;
        if (_tile.Child is StackPanel row)
        {
            row.Orientation = side ? Orientation.Vertical : Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;
        }

        _copy.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _copy.HorizontalAlignment = side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _title.TextAlignment = side ? TextAlignment.Center : TextAlignment.Left;
        _detail.TextAlignment = side ? TextAlignment.Center : TextAlignment.Left;

        if (side)
        {
            var inner = DockTokens.SideTileWidth - 12;
            _thumb.Width = inner;
            _thumb.Height = 64;
            _thumb.Margin = new Thickness(0, 0, 0, 4);
            _copy.Width = inner;
            _copy.MinWidth = 0;
            _copy.MaxWidth = inner;
            _title.Width = inner;
            _detail.Width = inner;
            _controls.Margin = new Thickness(0, 4, 0, 0);
            _controls.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            _thumb.Width = ThumbSize;
            _thumb.Height = ThumbSize;
            _thumb.Margin = new Thickness(0, 0, 8, 0);
            _copy.Width = CopyWidth;
            _copy.MinWidth = CopyWidth;
            _copy.MaxWidth = CopyWidth;
            _title.Width = CopyWidth;
            _detail.Width = CopyWidth;
            _controls.Margin = compact ? new Thickness(0) : new Thickness(8, 0, 0, 0);
            _controls.HorizontalAlignment = HorizontalAlignment.Left;
        }

        WidgetChrome.FitEdge(_tile, side, 0);
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.WidgetBing;
        _refreshBtn.ToolTip = UiText.Refresh;
        _prev.ToolTip = UiText.PreviousDay;
        _apply.ToolTip = UiText.SetAsDesktop;
        _restore.ToolTip = UiText.RestorePreviousWallpaper;
        _next.ToolTip = UiText.NextDay;
        _heroPrevBtn.ToolTip = UiText.PreviousDay;
        _heroNextBtn.ToolTip = UiText.NextDay;
        _heroOpenOriginalBtn.ToolTip = UiText.ViewOriginalImage;
        _heroOpenOriginalLabel.Text = UiText.ViewOriginalShort;
        _restoreActionLabel.Text = UiText.RestorePreviousWallpaper;
        _autoDailyTitle.Text = UiText.BingAutoDaily;
        _autoDailyHint.Text = UiText.BingAutoDailyHint;
        UpdateAutoDailyVisual();
        Render();
    }

    private void Render()
    {
        var snapshot = _bing.Current;
        var selected = snapshot.Selected;

        _prev.IsEnabled = !snapshot.IsBusy;
        _next.IsEnabled = !snapshot.IsBusy;
        _heroPrevBtn.IsEnabled = !snapshot.IsBusy;
        _heroNextBtn.IsEnabled = !snapshot.IsBusy;
        _apply.IsEnabled = !snapshot.IsBusy && selected is not null;
        _applyActionBtn.IsEnabled = !snapshot.IsBusy && selected is not null;

        var canRestore = _bing.CanRestorePreviousWallpaper;
        _restore.Visibility = canRestore ? Visibility.Visible : Visibility.Collapsed;
        _restore.IsEnabled = !snapshot.IsBusy && canRestore;
        _restoreActionBtn.Visibility = canRestore ? Visibility.Visible : Visibility.Collapsed;
        _restoreActionBtn.IsEnabled = !snapshot.IsBusy && canRestore;

        if (selected is null)
        {
            _title.Text = UiText.WidgetBing;
            _detail.Text = snapshot.Error ?? (snapshot.IsBusy ? UiText.Loading : UiText.ClickRetry);
            _flyoutDateBadge.Text = "";
            _flyoutDateBadgeBorder.Visibility = Visibility.Collapsed;
            _infoTitle.Text = UiText.WidgetBing;
            _infoCopyright.Text = snapshot.Error ?? (snapshot.IsBusy ? UiText.Loading : UiText.ClickRetry);
            _heroPlaceholderText.Text = snapshot.Error ?? (snapshot.IsBusy ? UiText.Loading : UiText.ClickRetry);
            _heroPlaceholder.Visibility = Visibility.Visible;
            _heroImage.Background = Brushes.Transparent;
            _heroOpenOriginalBtn.Visibility = Visibility.Collapsed;
            ShowThumb(null);
            RenderThumbStrip(snapshot);
            return;
        }

        var isApplied = string.Equals(snapshot.AppliedKey, selected.Key, StringComparison.Ordinal);
        var dateFormatted = BingWallpaperArchive.FormatDate(selected.StartDate);

        // Dock View
        _title.Text = selected.Title;
        _detail.Text = snapshot.Error
                       ?? (isApplied
                           ? UiText.SetAsDesktopDone
                           : snapshot.IsBusy ? UiText.Loading : dateFormatted);
        ToolTip = selected.Copyright;
        ShowThumb(ImageThumbCache.Load(snapshot.LocalPath, 96));

        // Flyout Header
        _flyoutDateBadge.Text = dateFormatted;
        _flyoutDateBadgeBorder.Visibility = Visibility.Visible;

        // Flyout Hero Image
        var heroBitmap = ImageThumbCache.Load(snapshot.LocalPath, 800);
        if (heroBitmap is not null)
        {
            var heroBrush = new ImageBrush(heroBitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            heroBrush.Freeze();
            _heroImage.Background = heroBrush;
            _heroPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            _heroImage.Background = Brushes.Transparent;
            _heroPlaceholderText.Text = snapshot.IsBusy ? UiText.Loading : UiText.NoBingImage;
            _heroPlaceholder.Visibility = Visibility.Visible;
        }
        _heroOpenOriginalBtn.Visibility = Visibility.Visible;

        // Flyout Info
        _infoTitle.Text = selected.Title;
        _infoCopyright.Text = selected.Copyright;

        // Action Button State
        if (isApplied)
        {
            _applyActionIcon.Icon = PhosphorIcon.Check;
            _applyActionLabel.Text = UiText.CurrentDesktop;
            _applyActionBtn.Opacity = 0.88;
        }
        else if (snapshot.IsBusy)
        {
            _applyActionIcon.Icon = PhosphorIcon.Desktop;
            _applyActionLabel.Text = UiText.ApplyingWallpaper;
            _applyActionBtn.Opacity = 0.6;
        }
        else
        {
            _applyActionIcon.Icon = PhosphorIcon.Desktop;
            _applyActionLabel.Text = UiText.ApplyToDesktop;
            _applyActionBtn.Opacity = 1.0;
        }

        // 8-Day Strip
        RenderThumbStrip(snapshot);
    }

    private void RenderThumbStrip(BingWallpaperSnapshot snapshot)
    {
        _thumbStrip.Children.Clear();
        if (snapshot.Images.Count == 0)
        {
            return;
        }

        for (var i = 0; i < snapshot.Images.Count; i++)
        {
            var index = i;
            var img = snapshot.Images[i];
            var isSelected = index == snapshot.SelectedIndex;

            var thumbBorder = new Border
            {
                Width = 36,
                Height = 22,
                CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                BorderThickness = new Thickness(2),
                BorderBrush = isSelected
                    ? TokenBrushes.From(DockTokens.Bing)
                    : Brushes.Transparent,
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                ToolTip = $"{BingWallpaperArchive.FormatDate(img.StartDate)} · {img.Title}"
            };

            var localPath = _bing.GetLocalPath(img);
            var thumbImage = ImageThumbCache.Load(localPath, 80);
            if (thumbImage is not null)
            {
                var brush = new ImageBrush(thumbImage)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
                brush.Freeze();
                thumbBorder.Background = brush;
            }
            else
            {
                WidgetChrome.MarkQuietOverlay(thumbBorder);
            }

            if (!isSelected)
            {
                thumbBorder.MouseEnter += (_, _) =>
                {
                    if (index != _bing.Current.SelectedIndex)
                    {
                        thumbBorder.BorderBrush = TokenBrushes.From(0x8038BDF8u);
                    }
                };
                thumbBorder.MouseLeave += (_, _) =>
                {
                    if (index != _bing.Current.SelectedIndex)
                    {
                        thumbBorder.BorderBrush = Brushes.Transparent;
                    }
                };
            }

            thumbBorder.MouseLeftButtonUp += (_, e) =>
            {
                _ = _bing.SelectIndexAsync(index);
                e.Handled = true;
            };

            _thumbStrip.Children.Add(thumbBorder);
        }
    }

    private void OpenOriginalUrl()
    {
        var selected = _bing.Current.Selected;
        if (selected is null || string.IsNullOrWhiteSpace(selected.WallpaperUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(selected.WallpaperUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private async Task ApplyDesktopAsync()
    {
        if (_applyDesktop is not null)
        {
            await _applyDesktop().ConfigureAwait(true);
        }
        else
        {
            await _bing.TryApplyCurrentAsync().ConfigureAwait(true);
        }

        Render();
    }

    private async Task RestoreDesktopAsync()
    {
        var ok = await _bing.TryRestorePreviousAsync().ConfigureAwait(true);
        UserAlerts.Show(ProductInfo.Name, ok ? UiText.WallpaperRestored : UiText.WallpaperRestoreFailed);
        Render();
    }

    private void ShowThumb(ImageSource? image)
    {
        if (image is null)
        {
            WidgetChrome.MarkQuietOverlay(_thumb);
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
        _thumb.Tag = null;
        _thumb.Background = brush;
        _thumb.Child = null;
    }

    private static Border FloatingCircleButton(string glyph, string tooltip, Action click)
    {
        var icon = new PhosphorGlyph(glyph, 13)
        {
            Foreground = Brushes.White,
            Tag = "tint",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var button = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x88, 0x1A, 0x1A, 0x24)),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon,
            Opacity = 0.85
        };
        button.MouseEnter += (_, _) => button.Opacity = 1.0;
        button.MouseLeave += (_, _) => button.Opacity = 0.85;
        AutomationProperties.SetName(button, tooltip);
        button.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return button;
    }

    private static Border FloatingPillButton(string glyph, string text, string tooltip, Action click, TextBlock labelOut)
    {
        var icon = new PhosphorGlyph(glyph, 11)
        {
            Foreground = Brushes.White,
            Tag = "tint",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        labelOut.Text = text;
        labelOut.FontSize = 10.5;
        labelOut.Foreground = Brushes.White;
        labelOut.Tag = "tint";
        labelOut.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, labelOut }
        };

        var button = new Border
        {
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(7, 0, 7, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x88, 0x1A, 0x1A, 0x24)),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = stack,
            Opacity = 0.85
        };
        button.MouseEnter += (_, _) => button.Opacity = 1.0;
        button.MouseLeave += (_, _) => button.Opacity = 0.85;
        button.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return button;
    }

    private static Border IconButton(string glyph, string tooltip, Action click)
    {
        var icon = new PhosphorGlyph(glyph, 11)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var button = new Border
        {
            Width = 22,
            Height = 22,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = icon
        };
        AutomationProperties.SetName(button, tooltip);
        button.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return button;
    }

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(left, 0);
        row.Children.Add(left);

        var rightStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var element in right)
        {
            rightStack.Children.Add(element);
        }
        Grid.SetColumn(rightStack, 1);
        row.Children.Add(rightStack);

        return row;
    }
}
