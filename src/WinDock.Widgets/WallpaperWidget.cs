using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class WallpaperWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IWallpaperConfigurable, IWidgetThemeAware, IWidgetModeAware, IDockEdgeAware, ILocalizable, IWidgetDetachable
{
    private const string GlyphPhoto = PhosphorIcon.Image;
    private const string GlyphPrevious = PhosphorIcon.CaretLeft;
    private const string GlyphNext = PhosphorIcon.CaretRight;
    private const string GlyphApply = PhosphorIcon.Desktop;
    private const string GlyphShuffle = PhosphorIcon.ArrowsClockwise;
    private const string GlyphRestore = PhosphorIcon.ArrowCounterClockwise;
    private const double CopyWidth = 72;
    private const double ThumbSize = 38;
    private const double ThumbRadius = 8;
    private const double ContentWidth = 356;

    private readonly IWallpaperProvider _wallpaper;
    private readonly Func<Task>? _applyDesktop;

    // --- Dock Tile Elements ---
    private readonly PhosphorGlyph _artGlyph = WidgetChrome.Glyph(GlyphPhoto, DockTokens.Wallpaper);
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
    private readonly TextBlock _flyoutBadge = WidgetCopy.Secondary();
    private readonly Border _flyoutBadgeBorder;
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
    private readonly TextBlock _heroTitle = WidgetCopy.Primary();
    private readonly TextBlock _heroAuthor = WidgetCopy.Secondary();

    // Source Selector Pills
    private readonly StackPanel _sourcePillRow;
    private readonly Border _pillCurated;
    private readonly Border _pillLocal;
    private readonly Border _pillUnsplash;
    private readonly Border _pillBing;
    private readonly TextBlock _pillCuratedText;
    private readonly TextBlock _pillLocalText;
    private readonly TextBlock _pillUnsplashText;
    private readonly TextBlock _pillBingText;

    // Source Options (Local)
    private readonly StackPanel _localOptionsPanel;
    private readonly TextBlock _localFolderPath = WidgetCopy.Secondary();
    private readonly Border _localBrowseBtn;
    private readonly CheckBox _localRecursiveCheck;

    // Source Options (Unsplash)
    private readonly StackPanel _unsplashOptionsPanel;
    private readonly TextBox _unsplashKeyBox;
    private readonly TextBox _unsplashQueryBox;
    private readonly Border _unsplashApplyBtn;

    // Action Buttons
    private readonly Border _applyActionBtn;
    private readonly PhosphorGlyph _applyActionIcon;
    private readonly TextBlock _applyActionLabel;
    private readonly Border _shuffleActionBtn;
    private readonly TextBlock _shuffleActionLabel;
    private readonly Border _restoreActionBtn;
    private readonly TextBlock _restoreActionLabel;

    // Auto Desktop Card
    private WallpaperSettings _settings = WallpaperSettings.Default;
    private readonly Border _autoCard;
    private readonly TextBlock _autoTitle = WidgetCopy.Primary();
    private readonly TextBlock _autoHint = WidgetCopy.Secondary();
    private readonly Border _autoToggleTrack;
    private readonly Border _autoToggleThumb;

    // Rotation Frequency Row
    private readonly StackPanel _frequencyRow;
    private readonly TextBlock _freqLabel = WidgetCopy.Secondary();
    private readonly Border _freqDaily;
    private readonly Border _freq1Hour;
    private readonly Border _freqStartup;
    private readonly Border _freqManual;
    private readonly TextBlock _freqDailyText;
    private readonly TextBlock _freq1HourText;
    private readonly TextBlock _freqStartupText;
    private readonly TextBlock _freqManualText;

    private readonly EventHandler _changed;
    private WidgetDensity _density = WidgetDensity.Expanded;
    private bool _side;
    private bool _isLight;

    public event EventHandler<WallpaperSettings>? SettingsChanged;

    public WallpaperWidget(IWallpaperProvider wallpaper, Func<Task>? applyDesktop = null)
    {
        _wallpaper = wallpaper;
        _applyDesktop = applyDesktop;

        // Dock Copy
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.Width = CopyWidth;
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _detail.TextWrapping = TextWrapping.NoWrap;
        _detail.Width = CopyWidth;

        _copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0),
            Children = { _title, _detail }
        };

        _thumb = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new CornerRadius(ThumbRadius),
            Background = TokenBrushes.From(DockTokens.Wallpaper),
            Child = _artGlyph,
            ClipToBounds = true
        };

        _prev = MakeToolBtn(GlyphPrevious, UiText.WallpaperPrev, () => _ = _wallpaper.SelectPreviousAsync());
        _apply = MakeToolBtn(GlyphApply, UiText.WallpaperApplyDesktop, () => _ = TriggerApplyDesktopAsync());
        _restore = MakeToolBtn(GlyphRestore, UiText.WallpaperRestoreOriginal, () => _ = _wallpaper.TryRestoreOriginalDesktopAsync());
        _next = MakeToolBtn(GlyphShuffle, UiText.WallpaperNext, () => _ = _wallpaper.SelectNextAsync());

        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Children = { _prev, _apply, _restore, _next }
        };

        var tileRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _thumb, _copy, _controls }
        };

        _tile = WidgetChrome.Wrap(tileRow);
        _tile.Cursor = Cursors.Hand;
        _tile.MouseLeftButtonUp += (sender, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && IsInsideButton(dep))
            {
                return;
            }
            if (_popup?.IsOpen == true) ClosePopup();
            else Open();
            e.Handled = true;
        };

        // --- Flyout Header ---
        _flyoutTitle.Text = UiText.WallpaperHubTitle;
        _flyoutTitle.FontWeight = FontWeights.SemiBold;
        _flyoutTitle.FontSize = 14;

        _flyoutBadge.FontSize = 11;
        _flyoutBadgeBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _flyoutBadge
        };
        WidgetChrome.MarkQuietOverlay(_flyoutBadgeBorder);

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _flyoutTitle, _flyoutBadgeBorder }
        };

        _refreshBtn = WidgetChrome.Glyph(GlyphShuffle, 0xFF888888);
        _refreshBtn.Cursor = Cursors.Hand;
        _refreshBtn.MouseLeftButtonUp += (_, e) =>
        {
            _ = _wallpaper.SelectNextAsync();
            e.Handled = true;
        };

        _closeBtn = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Child = WidgetChrome.Glyph(PhosphorIcon.Minus, 0xFF888888)
        };
        WidgetChrome.MarkQuietOverlay(_closeBtn);
        _closeBtn.MouseLeftButtonUp += (_, e) => { ClosePopup(); e.Handled = true; };

        var headerRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _refreshBtn, new Border { Width = 6 }, _closeBtn }
        };

        var headerRow = new DockPanel
        {
            Width = ContentWidth,
            LastChildFill = false,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(titleStack, Dock.Left);
        DockPanel.SetDock(headerRight, Dock.Right);
        headerRow.Children.Add(titleStack);
        headerRow.Children.Add(headerRight);

        // --- Hero Card ---
        _heroImage = new Border
        {
            Width = ContentWidth,
            Height = 176,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true
        };

        _heroPlaceholderText.HorizontalAlignment = HorizontalAlignment.Center;
        _heroPlaceholder = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                WidgetChrome.Glyph(GlyphPhoto, 0x88888888),
                new Border { Height = 6 },
                _heroPlaceholderText
            }
        };

        _heroPrevBtn = MakeHeroNavBtn(GlyphPrevious, HorizontalAlignment.Left, () => _ = _wallpaper.SelectPreviousAsync());
        _heroNextBtn = MakeHeroNavBtn(GlyphNext, HorizontalAlignment.Right, () => _ = _wallpaper.SelectNextAsync());

        _heroOpenOriginalBtn = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = TokenBrushes.From(0x80000000),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 8, 0),
            Child = WidgetChrome.Glyph(PhosphorIcon.ArrowSquareOut, 0xFFFFFFFF)
        };
        _heroOpenOriginalBtn.MouseLeftButtonUp += (_, e) =>
        {
            var cur = _wallpaper.Current.Current;
            if (cur is not null && !string.IsNullOrWhiteSpace(cur.PageUrl))
            {
                try { Process.Start(new ProcessStartInfo(cur.PageUrl) { UseShellExecute = true }); } catch { }
            }
            e.Handled = true;
        };

        _heroTitle.FontWeight = FontWeights.SemiBold;
        _heroTitle.Foreground = Brushes.White;
        _heroTitle.FontSize = 13;
        _heroTitle.TextTrimming = TextTrimming.CharacterEllipsis;

        _heroAuthor.Foreground = TokenBrushes.From(0xCCFFFFFF);
        _heroAuthor.FontSize = 11;
        _heroAuthor.TextTrimming = TextTrimming.CharacterEllipsis;

        var heroInfoStrip = new Border
        {
            Background = TokenBrushes.From(0x99000000),
            CornerRadius = new CornerRadius(0, 0, 10, 10),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Children = { _heroTitle, _heroAuthor }
            }
        };

        var heroGrid = new Grid { Width = ContentWidth, Height = 176 };
        heroGrid.Children.Add(_heroImage);
        heroGrid.Children.Add(_heroPlaceholder);
        heroGrid.Children.Add(_heroPrevBtn);
        heroGrid.Children.Add(_heroNextBtn);
        heroGrid.Children.Add(_heroOpenOriginalBtn);
        heroGrid.Children.Add(heroInfoStrip);

        _heroCard = new Border
        {
            Width = ContentWidth,
            Height = 176,
            CornerRadius = new CornerRadius(10),
            Child = heroGrid,
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // --- Source Switcher Pills ---
        (_pillCurated, _pillCuratedText) = MakeSourcePill(UiText.WallpaperSourceCurated, WallpaperSource.Curated);
        (_pillLocal, _pillLocalText) = MakeSourcePill(UiText.WallpaperSourceLocal, WallpaperSource.Local);
        (_pillUnsplash, _pillUnsplashText) = MakeSourcePill(UiText.WallpaperSourceUnsplash, WallpaperSource.Unsplash);
        (_pillBing, _pillBingText) = MakeSourcePill(UiText.WallpaperSourceBing, WallpaperSource.Bing);

        _sourcePillRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { _pillCurated, _pillLocal, _pillUnsplash, _pillBing }
        };

        // --- Local Folder Options ---
        _localBrowseBtn = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = UiText.WallpaperChooseFolder, FontSize = 12 }
        };
        WidgetChrome.MarkQuietOverlay(_localBrowseBtn);
        _localBrowseBtn.MouseLeftButtonUp += (_, e) =>
        {
            BrowseLocalFolder();
            e.Handled = true;
        };

        _localFolderPath.FontSize = 11;
        _localFolderPath.TextTrimming = TextTrimming.CharacterEllipsis;
        _localFolderPath.MaxWidth = 220;

        _localRecursiveCheck = new CheckBox
        {
            Content = UiText.WallpaperRecursive,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            IsChecked = true
        };
        _localRecursiveCheck.Click += (_, _) =>
        {
            _settings = _settings with { LocalRecursive = _localRecursiveCheck.IsChecked == true };
            SaveAndBroadcastSettings();
        };

        _localOptionsPanel = new StackPanel
        {
            Width = ContentWidth,
            Margin = new Thickness(0, 0, 0, 10),
            Children =
            {
                new DockPanel
                {
                    Children =
                    {
                        _localBrowseBtn,
                        new Border { Width = 8 },
                        _localFolderPath
                    }
                },
                _localRecursiveCheck
            }
        };

        // --- Unsplash Options ---
        _unsplashKeyBox = new TextBox
        {
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 2, 0, 4),
            MaxLength = 100
        };
        _unsplashQueryBox = new TextBox
        {
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 2, 0, 4),
            MaxLength = 50
        };
        _unsplashApplyBtn = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock { Text = UiText.Confirm, FontSize = 12 }
        };
        WidgetChrome.MarkQuietOverlay(_unsplashApplyBtn);
        _unsplashApplyBtn.MouseLeftButtonUp += (_, e) =>
        {
            _settings = _settings with
            {
                UnsplashAccessKey = _unsplashKeyBox.Text.Trim(),
                UnsplashQuery = _unsplashQueryBox.Text.Trim()
            };
            SaveAndBroadcastSettings();
            _ = _wallpaper.SelectNextAsync();
            e.Handled = true;
        };

        _unsplashOptionsPanel = new StackPanel
        {
            Width = ContentWidth,
            Margin = new Thickness(0, 0, 0, 10),
            Children =
            {
                new TextBlock { Text = UiText.WallpaperUnsplashKey, FontSize = 11 },
                _unsplashKeyBox,
                new TextBlock { Text = UiText.WallpaperUnsplashQuery, FontSize = 11 },
                _unsplashQueryBox,
                _unsplashApplyBtn
            }
        };

        // --- Action Buttons ---
        _applyActionIcon = WidgetChrome.Glyph(GlyphApply, 0xFFFFFFFF);
        _applyActionLabel = new TextBlock
        {
            Text = UiText.WallpaperApplyDesktop,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(6, 0, 0, 0)
        };

        _applyActionBtn = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Background = TokenBrushes.From(DockTokens.Settings.Accent),
            Cursor = Cursors.Hand,
            Padding = new Thickness(14, 0, 14, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _applyActionIcon, _applyActionLabel }
            }
        };
        _applyActionBtn.MouseLeftButtonUp += (_, e) => { _ = TriggerApplyDesktopAsync(); e.Handled = true; };

        _shuffleActionLabel = new TextBlock { Text = UiText.WallpaperNext, FontSize = 12, Margin = new Thickness(6, 0, 0, 0) };
        _shuffleActionBtn = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { WidgetChrome.Glyph(GlyphShuffle, 0xAA888888), _shuffleActionLabel }
            }
        };
        WidgetChrome.MarkQuietOverlay(_shuffleActionBtn);
        _shuffleActionBtn.MouseLeftButtonUp += (_, e) => { _ = _wallpaper.SelectNextAsync(); e.Handled = true; };

        _restoreActionLabel = new TextBlock { Text = UiText.WallpaperRestoreOriginal, FontSize = 12, Margin = new Thickness(6, 0, 0, 0) };
        _restoreActionBtn = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { WidgetChrome.Glyph(GlyphRestore, 0xAA888888), _restoreActionLabel }
            }
        };
        WidgetChrome.MarkQuietOverlay(_restoreActionBtn);
        _restoreActionBtn.MouseLeftButtonUp += (_, e) => { _ = _wallpaper.TryRestoreOriginalDesktopAsync(); e.Handled = true; };

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { _applyActionBtn, _shuffleActionBtn, _restoreActionBtn }
        };

        // --- Auto Sync Desktop Card ---
        _autoToggleThumb = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0)
        };

        _autoToggleTrack = new Border
        {
            Width = 36,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _autoToggleThumb
        };

        var autoCardRow = new DockPanel
        {
            LastChildFill = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_autoToggleTrack, Dock.Right);
        autoCardRow.Children.Add(_autoToggleTrack);
        autoCardRow.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _autoTitle, _autoHint }
        });

        _autoCard = new Border
        {
            Width = ContentWidth,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius + 2),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Cursor = Cursors.Hand,
            Child = autoCardRow
        };
        WidgetChrome.MarkQuietOverlay(_autoCard);
        _autoCard.MouseLeftButtonUp += (_, e) => { ToggleAutoSync(); e.Handled = true; };

        // --- Rotation Frequency Selector ---
        (_freqDaily, _freqDailyText) = MakeFreqPill(UiText.WallpaperRotationDaily, WallpaperRotation.Daily);
        (_freq1Hour, _freq1HourText) = MakeFreqPill(UiText.WallpaperRotation1Hour, WallpaperRotation.Interval1Hour);
        (_freqStartup, _freqStartupText) = MakeFreqPill(UiText.WallpaperRotationStartup, WallpaperRotation.Startup);
        (_freqManual, _freqManualText) = MakeFreqPill(UiText.WallpaperRotationManual, WallpaperRotation.Manual);

        _frequencyRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _freqDaily, _freq1Hour, _freqStartup, _freqManual }
        };

        var freqContainer = new DockPanel
        {
            Width = ContentWidth,
            LastChildFill = false,
            Margin = new Thickness(0, 0, 0, 4)
        };
        DockPanel.SetDock(_freqLabel, Dock.Left);
        DockPanel.SetDock(_frequencyRow, Dock.Right);
        freqContainer.Children.Add(_freqLabel);
        freqContainer.Children.Add(_frequencyRow);

        var flyoutBody = new StackPanel
        {
            Width = ContentWidth,
            Children =
            {
                headerRow,
                _heroCard,
                _sourcePillRow,
                _localOptionsPanel,
                _unsplashOptionsPanel,
                actionRow,
                _autoCard,
                freqContainer
            }
        };

        _panel = WidgetChrome.Flyout(flyoutBody, ContentWidth);
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);
        _panel.Focusable = true;

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
        _popup.Closed += (_, _) => InvalidateVisual();

        _changed = UiPump.Coalesce(Dispatcher, Render);

        Loaded += (_, _) =>
        {
            _wallpaper.Changed += _changed;
            Render();
            _ = _wallpaper.RefreshAsync();
        };

        Unloaded += (_, _) =>
        {
            _wallpaper.Changed -= _changed;
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

    public event EventHandler<string>? PaperChanged { add { } remove { } }

    public void ApplySettings(WallpaperSettings settings)
    {
        _settings = WallpaperSettings.Normalize(settings);
        _localRecursiveCheck.IsChecked = _settings.LocalRecursive;
        _unsplashKeyBox.Text = _settings.UnsplashAccessKey;
        _unsplashQueryBox.Text = _settings.UnsplashQuery;
        UpdateVisualStates();
    }

    public void ClosePopup() => _popup.IsOpen = false;

    public void Detach()
    {
        _wallpaper.Changed -= _changed;
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
        UpdateVisualStates();
        Render();
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.WallpaperHubTitle;
        _pillCuratedText.Text = UiText.WallpaperSourceCurated;
        _pillLocalText.Text = UiText.WallpaperSourceLocal;
        _pillUnsplashText.Text = UiText.WallpaperSourceUnsplash;
        _pillBingText.Text = UiText.WallpaperSourceBing;
        _applyActionLabel.Text = UiText.WallpaperApplyDesktop;
        _shuffleActionLabel.Text = UiText.WallpaperNext;
        _restoreActionLabel.Text = UiText.WallpaperRestoreOriginal;
        _autoTitle.Text = UiText.WallpaperAutoApply;
        _autoHint.Text = UiText.WallpaperAutoApplyHint;
        _freqLabel.Text = UiText.WallpaperRotationLabel;
        _freqDailyText.Text = UiText.WallpaperRotationDaily;
        _freq1HourText.Text = UiText.WallpaperRotation1Hour;
        _freqStartupText.Text = UiText.WallpaperRotationStartup;
        _freqManualText.Text = UiText.WallpaperRotationManual;
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

        _controls.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _controls.Orientation = side ? Orientation.Vertical : Orientation.Horizontal;
        _controls.Margin = side ? new Thickness(0, 4, 0, 0) : new Thickness(4, 0, 0, 0);

        WidgetChrome.FitEdge(_tile, side, 0);
        _tile.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void Render()
    {
        var snapshot = _wallpaper.Current;
        var current = snapshot.Current;

        _prev.IsEnabled = !snapshot.IsBusy;
        _next.IsEnabled = !snapshot.IsBusy;
        _heroPrevBtn.IsEnabled = !snapshot.IsBusy;
        _heroNextBtn.IsEnabled = !snapshot.IsBusy;
        _apply.IsEnabled = !snapshot.IsBusy && current is not null;
        _applyActionBtn.IsEnabled = !snapshot.IsBusy && current is not null;

        var canRestore = _wallpaper.CanRestoreOriginalWallpaper;
        _restore.Visibility = canRestore ? Visibility.Visible : Visibility.Collapsed;
        _restoreActionBtn.Visibility = canRestore ? Visibility.Visible : Visibility.Collapsed;

        if (current is null)
        {
            _title.Text = UiText.WidgetWallpaper;
            _detail.Text = snapshot.Error ?? (snapshot.IsBusy ? UiText.Loading : UiText.ClickRetry);
            _heroPlaceholderText.Text = snapshot.Error ?? (snapshot.IsBusy ? UiText.Loading : UiText.ClickRetry);
            _heroPlaceholder.Visibility = Visibility.Visible;
            _heroImage.Background = Brushes.Transparent;
            _heroOpenOriginalBtn.Visibility = Visibility.Collapsed;
            ShowThumb(null);
            UpdateVisualStates();
            return;
        }

        var isApplied = string.Equals(snapshot.AppliedKey, current.Id, StringComparison.Ordinal);

        // Dock Tile
        _title.Text = string.IsNullOrWhiteSpace(current.Title) ? UiText.WidgetWallpaper : current.Title;
        _detail.Text = snapshot.Error
            ?? (isApplied ? UiText.SetAsDesktopDone : (snapshot.IsBusy ? UiText.Loading : current.Author));
        ToolTip = $"{current.Title} · {current.Author}";

        var localPath = _wallpaper.GetLocalPath(current);
        ShowThumb(ImageThumbCache.Load(localPath, 96));

        // Hero Card
        var heroBitmap = ImageThumbCache.Load(localPath, 800);
        if (heroBitmap is not null)
        {
            _heroImage.Background = new ImageBrush(heroBitmap) { Stretch = Stretch.UniformToFill };
            _heroPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            _heroImage.Background = TokenBrushes.From(HexColor(current.DominantColor));
            _heroPlaceholderText.Text = snapshot.IsBusy ? UiText.Loading : current.Title;
            _heroPlaceholder.Visibility = Visibility.Visible;
        }

        _heroTitle.Text = string.IsNullOrWhiteSpace(current.Title) ? UiText.WidgetWallpaper : current.Title;
        _heroAuthor.Text = string.IsNullOrWhiteSpace(current.Author) ? "" : $"by {current.Author}";
        _heroOpenOriginalBtn.Visibility = !string.IsNullOrWhiteSpace(current.PageUrl) ? Visibility.Visible : Visibility.Collapsed;

        // Badge
        _flyoutBadge.Text = current.Source switch
        {
            WallpaperSource.Curated => UiText.WallpaperSourceCurated,
            WallpaperSource.Local => UiText.WallpaperSourceLocal,
            WallpaperSource.Unsplash => UiText.WallpaperSourceUnsplash,
            WallpaperSource.Bing => UiText.WallpaperSourceBing,
            _ => ""
        };

        UpdateVisualStates();
    }

    private void UpdateVisualStates()
    {
        // Source pill active states
        SetPillActive(_pillCurated, _pillCuratedText, _settings.Source == WallpaperSource.Curated);
        SetPillActive(_pillLocal, _pillLocalText, _settings.Source == WallpaperSource.Local);
        SetPillActive(_pillUnsplash, _pillUnsplashText, _settings.Source == WallpaperSource.Unsplash);
        SetPillActive(_pillBing, _pillBingText, _settings.Source == WallpaperSource.Bing);

        _localOptionsPanel.Visibility = _settings.Source == WallpaperSource.Local ? Visibility.Visible : Visibility.Collapsed;
        _unsplashOptionsPanel.Visibility = _settings.Source == WallpaperSource.Unsplash ? Visibility.Visible : Visibility.Collapsed;

        _localFolderPath.Text = string.IsNullOrWhiteSpace(_settings.LocalFolder) ? UiText.WallpaperSelectFolder : _settings.LocalFolder;

        // Auto Sync Visual
        var autoOn = _settings.AutoApplyDesktop;
        if (autoOn)
        {
            _autoToggleTrack.Background = TokenBrushes.From(DockTokens.Settings.Accent);
            _autoToggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
            _autoToggleThumb.Background = Brushes.White;
        }
        else
        {
            _autoToggleTrack.Background = TokenBrushes.From(_isLight ? 0x24000000u : 0x33FFFFFFu);
            _autoToggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
            _autoToggleThumb.Background = TokenBrushes.From(_isLight ? 0x88000000u : 0xCCFFFFFFu);
        }

        // Frequency active states
        SetFreqActive(_freqDaily, _freqDailyText, _settings.Rotation == WallpaperRotation.Daily);
        SetFreqActive(_freq1Hour, _freq1HourText, _settings.Rotation == WallpaperRotation.Interval1Hour);
        SetFreqActive(_freqStartup, _freqStartupText, _settings.Rotation == WallpaperRotation.Startup);
        SetFreqActive(_freqManual, _freqManualText, _settings.Rotation == WallpaperRotation.Manual);
    }

    private void SetPillActive(Border pill, TextBlock text, bool active)
    {
        if (active)
        {
            pill.Background = TokenBrushes.From(DockTokens.Settings.Accent);
            text.Foreground = Brushes.White;
        }
        else
        {
            pill.Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x20FFFFFFu);
            text.Foreground = TokenBrushes.From(_isLight ? 0xCC000000u : 0xCCFFFFFFu);
        }
    }

    private void SetFreqActive(Border pill, TextBlock text, bool active)
    {
        if (active)
        {
            pill.Background = TokenBrushes.From(DockTokens.Settings.Accent);
            text.Foreground = Brushes.White;
        }
        else
        {
            pill.Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x20FFFFFFu);
            text.Foreground = TokenBrushes.From(_isLight ? 0xAA000000u : 0xAAFFFFFFu);
        }
    }

    private void ToggleAutoSync()
    {
        var next = !_settings.AutoApplyDesktop;
        _settings = _settings with { AutoApplyDesktop = next };
        SaveAndBroadcastSettings();
        if (next)
        {
            _ = _wallpaper.TryApplyCurrentDesktopAsync();
        }
    }

    private void BrowseLocalFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = UiText.WallpaperChooseFolder
        };
        if (!string.IsNullOrWhiteSpace(_settings.LocalFolder) && Directory.Exists(_settings.LocalFolder))
        {
            dialog.InitialDirectory = _settings.LocalFolder;
        }

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _settings = _settings with
            {
                LocalFolder = dialog.FolderName,
                LocalCurrent = "",
                LocalSeen = []
            };
            SaveAndBroadcastSettings();
            _ = _wallpaper.SelectNextAsync();
        }
    }

    private async Task TriggerApplyDesktopAsync()
    {
        if (_applyDesktop is not null)
        {
            await _applyDesktop();
        }
        else
        {
            var ok = await _wallpaper.TryApplyCurrentDesktopAsync();
            if (ok)
            {
                _applyActionLabel.Text = UiText.SetAsDesktopDone;
                _ = Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() => _applyActionLabel.Text = UiText.WallpaperApplyDesktop));
            }
        }
    }

    private void SaveAndBroadcastSettings()
    {
        UpdateVisualStates();
        _ = _wallpaper.ApplySettingsAsync(_settings);
        SettingsChanged?.Invoke(this, _settings);
    }

    private void ShowThumb(ImageSource? bitmap)
    {
        if (bitmap is not null)
        {
            _thumb.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            _artGlyph.Visibility = Visibility.Collapsed;
        }
        else
        {
            _thumb.Background = TokenBrushes.From(DockTokens.Wallpaper);
            _artGlyph.Visibility = Visibility.Visible;
        }
    }

    private (Border Pill, TextBlock Text) MakeSourcePill(string label, WallpaperSource source)
    {
        var text = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.Medium };
        var pill = new Border
        {
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(3, 0, 3, 0),
            CornerRadius = new CornerRadius(14),
            Cursor = Cursors.Hand,
            Child = text
        };
        pill.MouseLeftButtonUp += (_, e) =>
        {
            _settings = _settings with { Source = source };
            SaveAndBroadcastSettings();
            e.Handled = true;
        };
        return (pill, text);
    }

    private (Border Pill, TextBlock Text) MakeFreqPill(string label, WallpaperRotation rotation)
    {
        var text = new TextBlock { Text = label, FontSize = 11 };
        var pill = new Border
        {
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(2, 0, 2, 0),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Child = text
        };
        pill.MouseLeftButtonUp += (_, e) =>
        {
            _settings = _settings with { Rotation = rotation };
            SaveAndBroadcastSettings();
            e.Handled = true;
        };
        return (pill, text);
    }

    private static Border MakeHeroNavBtn(string glyph, HorizontalAlignment align, Action onClick)
    {
        var btn = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = TokenBrushes.From(0x80000000),
            Cursor = Cursors.Hand,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(align == HorizontalAlignment.Left ? 8 : 0, 0, align == HorizontalAlignment.Right ? 8 : 0, 0),
            Child = WidgetChrome.Glyph(glyph, 0xFFFFFFFF)
        };
        btn.MouseLeftButtonUp += (_, e) => { onClick(); e.Handled = true; };
        return btn;
    }

    private static Border MakeToolBtn(string glyph, string toolTip, Action onClick)
    {
        var border = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            ToolTip = toolTip,
            Margin = new Thickness(1, 0, 1, 0),
            Child = WidgetChrome.Glyph(glyph, 0xAA888888)
        };
        WidgetChrome.MarkQuietOverlay(border);
        border.MouseLeftButtonUp += (_, e) => { onClick(); e.Handled = true; };
        return border;
    }

    private static bool IsInsideButton(DependencyObject obj)
    {
        var current = obj;
        while (current is not null)
        {
            if (current is Button || (current is FrameworkElement fe && fe.Cursor == Cursors.Hand)) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static uint HexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith('#')) return 0xFF1E293B;
        try
        {
            var raw = hex[1..];
            if (raw.Length == 6) return 0xFF000000u | Convert.ToUInt32(raw, 16);
            if (raw.Length == 8) return Convert.ToUInt32(raw, 16);
        }
        catch { }
        return 0xFF1E293B;
    }
}
