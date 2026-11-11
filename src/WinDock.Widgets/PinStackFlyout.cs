using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class PinStackFlyout
{
    private readonly Popup _popup = new();
    private readonly ScrollViewer _scroll = new()
    {
        Focusable = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
    };
    private readonly StackPanel _bands = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
    private readonly List<Border> _tiles = [];
    private readonly Border _host;
    private readonly Border _card;
    private readonly ScaleTransform _scale = new(1, 1);
    private DockPinStackKind? _kind;
    private DockPinStackKind? _dismissedKind;
    private DateTime _dismissedAt;
    private Func<DockItem, ImageSource?>? _icon;
    private bool _isLight;
    private DockBackdrop _backdrop = DockBackdrop.Mica;
    private IReadOnlySet<string> _running = new HashSet<string>(StringComparer.Ordinal);

    public PinStackFlyout()
    {
        _scroll.Content = _bands;
        WidgetChrome.PaintScrollViewer(_scroll, false);
        _host = WidgetChrome.Flyout(_scroll, minWidth: 0);
        _card = _host.Child as Border ?? new Border { Child = _scroll, Tag = WidgetChrome.FlyoutTag };
        _card.Padding = new Thickness(
            DockTokens.TilePaddingX,
            DockTokens.TilePaddingY,
            DockTokens.TilePaddingX,
            DockTokens.TilePaddingY);
        _card.MinWidth = 0;
        _host.RenderTransformOrigin = new Point(0.5, 0);
        _host.RenderTransform = _scale;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.Child = _host;
        WidgetFlyout.Attach(_popup);
        _popup.Opened += (_, _) =>
        {
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };
        _popup.Closed += (_, _) =>
        {
            _dismissedKind = _kind;
            _dismissedAt = DateTime.UtcNow;
            _kind = null;
            _bands.Children.Clear();
            _tiles.Clear();
            ResetScroll();
            _popup.StaysOpen = false;
        };
    }

    public event EventHandler<string>? PinActivateRequested;

    public event EventHandler<string>? PinLaunchRequested;

    public event EventHandler<string>? PinRemoveRequested;

    public bool IsOpen => _popup.IsOpen;

    public DockPinStackKind? Kind => _kind;

    public void ApplyTheme(bool isLight, DockBackdrop backdrop)
    {
        _isLight = isLight;
        _backdrop = backdrop;
        WidgetChrome.ApplyTheme(_card, isLight, backdrop);
        WidgetChrome.PaintScrollViewer(_scroll, isLight);
        foreach (var tile in _tiles)
        {
            PaintPin(tile);
        }
    }

    public void SetRunning(IReadOnlySet<string> runningIds)
    {
        _running = runningIds;
        foreach (var tile in _tiles)
        {
            if (tile.Tag is DockItem item && FindDot(tile) is { } dot)
            {
                dot.Visibility = runningIds.Contains(item.Id) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    public void Toggle(
        FrameworkElement target,
        DockPinStackKind kind,
        IReadOnlyList<DockItem> members,
        Func<DockItem, ImageSource?>? icon,
        bool isLight,
        DockBackdrop backdrop,
        IReadOnlySet<string> runningIds,
        RectLogical workArea,
        RectLogical island)
    {
        if (_popup.IsOpen && _kind == kind)
        {
            Hide();
            return;
        }

        if (!_popup.IsOpen
            && _dismissedKind == kind
            && DateTime.UtcNow - _dismissedAt < TimeSpan.FromMilliseconds(300))
        {
            return;
        }

        Show(target, kind, members, icon, isLight, backdrop, runningIds, workArea, island);
    }

    public void Show(
        FrameworkElement target,
        DockPinStackKind kind,
        IReadOnlyList<DockItem> members,
        Func<DockItem, ImageSource?>? icon,
        bool isLight,
        DockBackdrop backdrop,
        IReadOnlySet<string> runningIds,
        RectLogical workArea,
        RectLogical island)
    {
        _kind = kind;
        _icon = icon;
        _running = runningIds;
        _popup.PlacementTarget = target;
        var edge = WidgetFlyout.Edge;
        var horizontal = edge is not DockEdge.Left and not DockEdge.Right;
        _bands.Children.Clear();
        _tiles.Clear();
        ResetScroll();
        var tiles = new List<Border>(members.Count);
        foreach (var member in members)
        {
            tiles.Add(CreatePin(member));
        }

        var arrangement = PinStackGrid.Arrange(
            tiles.Select(tile => tile.Width).ToArray(),
            horizontal,
            PinStackGrid.Budget(workArea, island, edge));
        Fill(arrangement, tiles);
        ApplyScroll(arrangement);
        ApplyTheme(isLight, backdrop);
        SetRunning(runningIds);
        _popup.IsOpen = true;
    }

    public void Hide() => _popup.IsOpen = false;

    private void Fill(PinStackGrid.Arrangement arrangement, IReadOnlyList<Border> tiles)
    {
        _bands.Orientation = arrangement.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
        _tiles.AddRange(tiles);
        for (var bandIndex = 0; bandIndex < arrangement.Bands.Count; bandIndex++)
        {
            var indexes = arrangement.Bands[bandIndex];
            var line = new StackPanel
            {
                Orientation = arrangement.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            for (var i = 0; i < indexes.Count; i++)
            {
                var tile = tiles[indexes[i]];
                var last = i == indexes.Count - 1;
                tile.Margin = arrangement.Horizontal
                    ? new Thickness(0, 0, last ? 0 : DockTokens.TileGap, 0)
                    : new Thickness(0, 0, 0, last ? 0 : DockTokens.TileGap);
                line.Children.Add(tile);
            }

            var lastBand = bandIndex == arrangement.Bands.Count - 1;
            line.Margin = arrangement.Horizontal
                ? new Thickness(0, 0, 0, lastBand ? 0 : DockTokens.TileGap)
                : new Thickness(0, 0, lastBand ? 0 : DockTokens.TileGap, 0);
            _bands.Children.Add(line);
        }
    }

    private void ApplyScroll(PinStackGrid.Arrangement arrangement)
    {
        _scroll.HorizontalScrollBarVisibility = arrangement.ScrollsHorizontally
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = arrangement.ScrollsVertically
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        if (arrangement.ScrollWidth is null && arrangement.ScrollHeight is null)
        {
            return;
        }

        _bands.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var extraWidth = Math.Max(0, _bands.DesiredSize.Width - arrangement.ContentWidth);
        var extraHeight = Math.Max(0, _bands.DesiredSize.Height - arrangement.ContentHeight);
        if (arrangement.ScrollWidth is > 0 and var width)
        {
            _scroll.Width = width + (arrangement.ScrollsHorizontally ? 0 : extraWidth);
        }

        if (arrangement.ScrollHeight is > 0 and var height)
        {
            _scroll.Height = height + (arrangement.ScrollsVertically ? 0 : extraHeight);
        }
    }

    private void ResetScroll()
    {
        _scroll.ClearValue(FrameworkElement.WidthProperty);
        _scroll.ClearValue(FrameworkElement.HeightProperty);
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.ScrollToHorizontalOffset(0);
        _scroll.ScrollToVerticalOffset(0);
    }

    private Border CreatePin(DockItem item)
    {
        ImageSource? icon = null;
        try
        {
            icon = _icon?.Invoke(item);
        }
        catch (Exception)
        {
            icon = null;
        }

        var missing = string.IsNullOrWhiteSpace(item.Path)
                      || (!System.IO.File.Exists(item.Path) && !System.IO.Directory.Exists(item.Path));
        var (faceWidth, faceHeight) = FaceSize(item, icon);
        UIElement face;
        if (icon is not null)
        {
            face = new Image
            {
                Source = icon,
                Width = faceWidth,
                Height = faceHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(face, BitmapScalingMode.HighQuality);
        }
        else
        {
            face = new PhosphorGlyph(Glyph(item.Type, missing), 26)
            {
                Opacity = missing ? 0.45 : 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        UIElement content = face;
        if (item.Type == DockItemType.Folder)
        {
            var host = new Grid
            {
                Width = faceWidth,
                Height = faceHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            host.Children.Add(face);
            host.Children.Add(FolderBadge(item, missing, faceWidth));
            content = host;
        }

        var running = new Ellipse
        {
            Tag = "pin-running",
            Width = 4,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        var body = new Grid
        {
            Width = faceWidth + 16,
            Height = DockTokens.TileHeight
        };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7) });
        Grid.SetRow(content, 0);
        body.Children.Add(content);
        Grid.SetRow(running, 1);
        body.Children.Add(running);

        var tile = new Border
        {
            Tag = item,
            Width = faceWidth + 16,
            Height = DockTokens.TileHeight,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = body
        };
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.Path ?? UiText.ShortcutFallback : item.Title;
        System.Windows.Automation.AutomationProperties.SetName(tile, title);
        tile.ToolTip = title;
        var id = item.Id;
        tile.MouseLeftButtonUp += (_, e) =>
        {
            PinActivateRequested?.Invoke(this, id);
            e.Handled = true;
        };
        tile.MouseRightButtonUp += (_, e) =>
        {
            ShowMenu(tile, id);
            e.Handled = true;
        };
        PaintPin(tile);
        return tile;
    }

    private void ShowMenu(FrameworkElement target, string itemId)
    {
        var menu = new ContextMenu
        {
            FontFamily = UiFont.Family,
            FontSize = 13,
            PlacementTarget = target
        };
        var open = new MenuItem { Header = UiText.OpenNew };
        open.Click += (_, _) => PinLaunchRequested?.Invoke(this, itemId);
        var remove = new MenuItem { Header = UiText.RemoveFromDock };
        remove.Foreground = TokenBrushes.From(DockTokens.Danger);
        remove.Click += (_, _) => PinRemoveRequested?.Invoke(this, itemId);
        menu.Items.Add(open);
        menu.Items.Add(remove);
        menu.Opened += (_, _) => _popup.StaysOpen = true;
        menu.Closed += (_, _) => _popup.StaysOpen = false;
        target.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void PaintPin(object? part)
    {
        switch (part)
        {
            case Border { Child: Grid body } tile:
                tile.Background = Brushes.Transparent;
                foreach (var child in body.Children)
                {
                    PaintPin(child);
                }

                break;
            case Grid host:
                foreach (var child in host.Children)
                {
                    PaintPin(child);
                }

                break;
            case Ellipse { Tag: "pin-running" } dot:
                dot.Fill = TokenBrushes.From(_isLight ? 0xFF1A1A1A : 0xFFFFFFFF);
                break;
            case PhosphorGlyph icon:
                icon.Foreground = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
                break;
            case TextBlock text:
                if (text.Tag as string == "pin-badge")
                {
                    text.Foreground = TokenBrushes.From(0xFF1A1A1A);
                }
                else
                {
                    text.Foreground = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
                }

                break;
            case Border { Tag: "pin-badge" } badge:
                if (badge.Child is TextBlock label)
                {
                    label.Foreground = TokenBrushes.From(0xFF1A1A1A);
                }

                break;
        }
    }

    private static Ellipse? FindDot(FrameworkElement tile)
    {
        if (tile is not Border { Child: Grid body })
        {
            return null;
        }

        foreach (var part in body.Children)
        {
            if (part is Ellipse { Tag: "pin-running" } dot)
            {
                return dot;
            }
        }

        return null;
    }

    private static (double Width, double Height) FaceSize(DockItem item, ImageSource? icon)
    {
        const double height = DockTokens.ArtSize;
        if (item.Type == DockItemType.Folder
            && icon is BitmapSource bitmap
            && bitmap.PixelHeight > 0)
        {
            var width = height * bitmap.PixelWidth / (double)bitmap.PixelHeight;
            return (Math.Clamp(width, height, height * 1.65), height);
        }

        return (height, height);
    }

    private static string Glyph(DockItemType type, bool missing)
    {
        if (missing)
        {
            return PhosphorIcon.Warning;
        }

        return type switch
        {
            DockItemType.Folder => PhosphorIcon.Folder,
            DockItemType.App => PhosphorIcon.AppWindow,
            _ => PhosphorIcon.File
        };
    }

    private static Border FolderBadge(DockItem item, bool missing, double faceWidth)
    {
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.Path ?? "?" : item.Title;
        var label = new TextBlock
        {
            Tag = "pin-badge",
            FontSize = 9,
            FontWeight = FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = Math.Max(24, faceWidth - 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TokenBrushes.From(0xFF1A1A1A)
        };
        FolderCaption.Apply(label, DockPins.FolderBadge(title));
        return new Border
        {
            Tag = "pin-badge",
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = missing ? 0.45 : 1,
            Child = label
        };
    }
}
