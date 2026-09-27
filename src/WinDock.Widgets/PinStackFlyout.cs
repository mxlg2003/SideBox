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
    private readonly StackPanel _row = new();
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
        _row.Orientation = Orientation.Horizontal;
        _host = WidgetChrome.Flyout(_row, minWidth: 0);
        _card = _host.Child as Border ?? new Border { Child = _row, Tag = WidgetChrome.FlyoutTag };
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
            _row.Children.Clear();
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
        foreach (UIElement child in _row.Children)
        {
            PaintPin(child);
        }
    }

    public void SetRunning(IReadOnlySet<string> runningIds)
    {
        _running = runningIds;
        foreach (UIElement child in _row.Children)
        {
            if (child is FrameworkElement { Tag: DockItem item } tile && FindDot(tile) is { } dot)
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
        IReadOnlySet<string> runningIds)
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

        Show(target, kind, members, icon, isLight, backdrop, runningIds);
    }

    public void Show(
        FrameworkElement target,
        DockPinStackKind kind,
        IReadOnlyList<DockItem> members,
        Func<DockItem, ImageSource?>? icon,
        bool isLight,
        DockBackdrop backdrop,
        IReadOnlySet<string> runningIds)
    {
        _kind = kind;
        _icon = icon;
        _running = runningIds;
        _popup.PlacementTarget = target;
        _row.Orientation = WidgetFlyout.Edge is DockEdge.Left or DockEdge.Right
            ? Orientation.Vertical
            : Orientation.Horizontal;
        _row.Children.Clear();
        for (var i = 0; i < members.Count; i++)
        {
            var tile = CreatePin(members[i]);
            if (i < members.Count - 1)
            {
                tile.Margin = _row.Orientation == Orientation.Horizontal
                    ? new Thickness(0, 0, DockTokens.TileGap, 0)
                    : new Thickness(0, 0, 0, DockTokens.TileGap);
            }

            _row.Children.Add(tile);
        }

        ApplyTheme(isLight, backdrop);
        SetRunning(runningIds);
        _popup.IsOpen = true;
    }

    public void Hide() => _popup.IsOpen = false;

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
            face = new TextBlock
            {
                Text = Glyph(item.Type, missing),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 26,
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
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
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
            return "\uE783";
        }

        return type switch
        {
            DockItemType.Folder => "\uE8B7",
            DockItemType.App => "\uE756",
            _ => "\uE8A5"
        };
    }

    private static Border FolderBadge(DockItem item, bool missing, double faceWidth)
    {
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.Path ?? "?" : item.Title;
        var label = new TextBlock
        {
            Tag = "pin-badge",
            Text = DockPins.FolderBadge(title),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 9,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = Math.Max(24, faceWidth - 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TokenBrushes.From(0xFF1A1A1A)
        };
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
