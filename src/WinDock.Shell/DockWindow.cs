using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;

namespace WinDock.Shell;

public sealed class DockWindow : Window, IDockSurface
{
    private readonly Border _island;
    private readonly Grid _chrome;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _itemsHost;
    private readonly Border _overflow;
    private readonly Button _overflowBack;
    private readonly Button _overflowForward;
    private readonly Border _startGrip;
    private readonly Border _endGrip;
    private DockEdge _edge = DockEdge.Top;
    private bool _isLight;
    private DockBackdrop _backdrop = DockBackdrop.Mica;
    private int _islandTransparency = DockTokens.DefaultIslandTransparency;
    private bool _backdropOk;
    private bool _resizing;
    private bool _resizeFromStart;
    private Point _resizeOrigin;
    private double _resizeStartLength;
    private double _minLength = DockTokens.MinIslandLength;
    private double _maxLength = 4096;
    private FrameworkElement? _dragSlot;
    private int _dragFrom = -1;
    private int _dragLiveIndex = -1;
    private Point _dragOrigin;
    private bool _dragArmed;
    private bool _dragging;
    private bool _tileDragSuspended;
    private TranslateTransform? _dragLift;
    private Point _grabOffset;
    private RectLogical _pixelWorkArea;
    private RectLogical _layoutBounds;
    private bool _stripMode;
    private readonly TranslateTransform _pinNameShift = new();
    private readonly TextBlock _pinNameText = new();
    private readonly Border _pinNameHost = new();
    private readonly Popup _pinNamePopup = new();
    private readonly TextBlock _statusText = new();
    private readonly Border _statusHost = new();
    private readonly Popup _statusPopup = new();
    private DispatcherTimer _statusTimer = null!;
    private int _pinNameToken;
    private Border? _emptyHint;
    private bool _islandDragArmed;
    private bool _islandDragCaptured;
    private Point _islandDragOrigin;
    private readonly Border _insertionCaret;

    public DockWindow()
    {
        Title = ProductInfo.Name;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Closing += (_, e) =>
        {
            if (Application.Current is null || Application.Current.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            e.Cancel = true;
        };
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Background = Brushes.Transparent;
        FontFamily = UiFont.Family;
        Height = DockLayoutCalculator.DefaultThickness;

        _itemsHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.HorizontalOnly,
            Focusable = false,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Content = _itemsHost
        };
        _scroll.PreviewMouseWheel += OnMouseWheel;
        _scroll.ScrollChanged += (_, _) => UpdateOverflowChrome();
        _itemsHost.PreviewMouseLeftButtonDown += OnTileMouseDown;

        _overflowBack = OverflowGlyph(PhosphorIcon.CaretLeft, UiText.ScrollLeft, () => ScrollBy(-DockTokens.ScrollStep));
        _overflowForward = OverflowGlyph(PhosphorIcon.CaretRight, UiText.ScrollRight, () => ScrollBy(DockTokens.ScrollStep));
        _overflow = new Border
        {
            Margin = new Thickness(2, 0, DockTokens.ResizeGrip, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed,
            SnapsToDevicePixels = true,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _overflowBack, _overflowForward }
            }
        };

        _startGrip = CreateGrip(fromStart: true);
        _endGrip = CreateGrip(fromStart: false);

        _chrome = new Grid();
        Grid.SetRow(_scroll, 0);
        Grid.SetColumn(_scroll, 0);
        Grid.SetColumnSpan(_scroll, 1);
        Grid.SetRowSpan(_scroll, 1);
        Grid.SetRow(_overflow, 0);
        Grid.SetColumn(_overflow, 1);
        Grid.SetColumnSpan(_overflow, 1);
        Grid.SetRowSpan(_overflow, 1);
        Grid.SetRow(_startGrip, 0);
        Grid.SetColumn(_startGrip, 0);
        Grid.SetColumnSpan(_startGrip, 2);
        Grid.SetRow(_endGrip, 0);
        Grid.SetColumn(_endGrip, 0);
        Grid.SetColumnSpan(_endGrip, 2);
        _chrome.Children.Add(_scroll);
        _chrome.Children.Add(_overflow);
        _chrome.Children.Add(_startGrip);
        _chrome.Children.Add(_endGrip);

        _insertionCaret = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = SystemColors.HighlightBrush,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Height = DockTokens.TileHeight - 12
        };
        Panel.SetZIndex(_insertionCaret, 99);
        _chrome.Children.Add(_insertionCaret);

        _island = new HitIsland
        {
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(DockTokens.IslandPaddingX, DockTokens.IslandPaddingY, DockTokens.IslandPaddingX, DockTokens.IslandPaddingY),
            SnapsToDevicePixels = true,
            Child = _chrome
        };
        _island.SizeChanged += (_, _) =>
        {
            _island.Clip = new RectangleGeometry(
                new Rect(0, 0, _island.ActualWidth, _island.ActualHeight),
                DockTokens.IslandCornerRadius,
                DockTokens.IslandCornerRadius);
            UpdateOverflowChrome();
        };

        Content = _island;
        PreparePinName();
        PrepareStatus();
        ApplyTheme(false);
        SourceInitialized += (_, _) => ApplyTheme(_isLight, _backdrop);
        Loaded += (_, _) => ApplyTheme(_isLight, _backdrop);
        MouseMove += OnWindowMouseMove;
        AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowMouseUp), handledEventsToo: true);
        PreviewMouseLeftButtonDown += OnStripMouseDown;
        _island.PreviewMouseLeftButtonDown += OnIslandBackgroundDown;
        LostMouseCapture += (_, _) =>
        {
            if (_resizing)
            {
                EndResize(commit: true);
            }
            else if (_dragArmed)
            {
                EndTileDrag(commit: true);
            }
            else if (_islandDragArmed)
            {
                CancelIslandMonitorDrag();
            }
        };
        MouseRightButtonUp += OnDockRightClick;
        AllowDrop = true;
        DragOver += OnFileDragOver;
        DragLeave += OnFileDragLeave;
        Drop += OnFileDrop;
        ApplyProfileMenu([], "");
    }

    public Func<DockItem, IReadOnlyList<DockWindowEntry>>? WindowEnumerator { get; set; }

    public event EventHandler<nint>? WindowActivateRequested;

    public event EventHandler<nint>? WindowCloseRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<string>? ProfileSwitchRequested;

    public event EventHandler<double?>? IslandLengthChanged;

    public event EventHandler<DockReorderRequest>? ItemsReordered;

    public event EventHandler<string>? WidgetOpenRequested;

    public event EventHandler<string>? WidgetCustomizeRequested;

    public event EventHandler<string>? WidgetRemoveRequested;

    public event EventHandler<WidgetDensityRequest>? WidgetDensityRequested;

    public event EventHandler<WidgetCreateFailedEventArgs>? WidgetCreateFailed;

    public event EventHandler<string>? PinLaunchRequested;

    public event EventHandler<string>? PinActivateRequested;

    public event EventHandler<PinStackToggleRequest>? PinStackToggleRequested;

    public event EventHandler<PinPickKind>? PinAddRequested;

    public event EventHandler<PinDropRequest>? PinsDropped;

    public event EventHandler<string>? IslandMonitorRequested;

    public event EventHandler? StripActivated;

    public event EventHandler<MouseWheelEventArgs>? IslandMouseWheel;

    public bool IsStripMode => _stripMode;

    public System.Windows.Size MeasureIsland()
    {
        _overflow.Visibility = Visibility.Collapsed;
        _itemsHost.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _itemsHost.DesiredSize;
        var width = Math.Max(1, size.Width + (DockTokens.IslandPaddingX * 2) + 2);
        var height = Math.Max(1, size.Height + (DockTokens.IslandPaddingY * 2) + 2);
        var thickness = DockLayoutCalculator.ThicknessFor(_edge);
        if (IsHorizontal(_edge))
        {
            height = Math.Min(height, thickness);
        }
        else
        {
            width = Math.Min(width, thickness);
        }

        return new System.Windows.Size(width, height);
    }

    public void SetEdge(DockEdge edge)
    {
        _edge = edge;
        ApplyOrientation();
    }

    public void ApplyLayout(RectLogical bounds, DockEdge edge) =>
        ApplyLayout(bounds, edge, DockTokens.MinIslandLength, IsHorizontal(edge) ? bounds.Width : bounds.Height);

    public void ApplyLayout(RectLogical bounds, DockEdge edge, double minLength, double maxLength)
    {
        _edge = edge;
        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(_minLength, maxLength);
        _layoutBounds = bounds;
        ApplyPlacement();
        ApplyOrientation();
        Dispatcher.BeginInvoke((Action)UpdateOverflowChrome, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ApplyZorder(DockZorderState state)
    {
        switch (state)
        {
            case DockZorderState.FullscreenYield:
                Hide();
                Topmost = false;
                break;
            case DockZorderState.AutoHidden:
                _stripMode = true;
                Opacity = 0.55;
                Topmost = true;
                ApplyPlacement();
                Show();
                break;
            case DockZorderState.Peeking:
                _stripMode = false;
                ApplyPlacement();
                Show();
                Topmost = true;
                Opacity = 0.85;
                break;
            default:
                _stripMode = false;
                ApplyPlacement();
                Opacity = 1;
                Topmost = true;
                Show();
                break;
        }
    }

    private void ApplyPlacement()
    {
        var bounds = _layoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (_stripMode)
        {
            const double strip = DockTokens.AutoHideStrip;
            Left = _edge switch
            {
                DockEdge.Left => bounds.X - DockTokens.IslandMargin,
                DockEdge.Right => bounds.X + bounds.Width + DockTokens.IslandMargin - strip,
                _ => bounds.X
            };
            Top = _edge switch
            {
                DockEdge.Top => bounds.Y - DockTokens.IslandMargin,
                DockEdge.Bottom => bounds.Y + bounds.Height + DockTokens.IslandMargin - strip,
                _ => bounds.Y
            };
            Width = _edge is DockEdge.Left or DockEdge.Right ? Math.Max(2, strip) : Math.Max(8, bounds.Width);
            Height = _edge is DockEdge.Left or DockEdge.Right ? Math.Max(8, bounds.Height) : Math.Max(2, strip);
            return;
        }

        Left = bounds.X;
        Top = bounds.Y;
        Width = Math.Max(32, bounds.Width);
        Height = Math.Max(8, bounds.Height);
    }

    public void ApplyItems(IReadOnlyList<DockItem> items) => ApplyItems(items, null);

    public void ApplyItems(IReadOnlyList<DockItem> items, Func<DockItem, UIElement>? widgetFactory) =>
        ApplyItems(items, widgetFactory, null);

    public void ApplyItems(
        IReadOnlyList<DockItem> items,
        Func<DockItem, UIElement>? widgetFactory,
        Func<DockItem, ImageSource?>? pinIcon) =>
        ApplyItems(DockPinCollapse.Project(items, false, false), widgetFactory, pinIcon);

    public void ApplyItems(
        IReadOnlyList<DockVisibleSlot> slots,
        Func<DockItem, UIElement>? widgetFactory,
        Func<DockItem, ImageSource?>? pinIcon) =>
        ApplyItems(slots, widgetFactory, pinIcon, null);

    public void ApplyItems(
        IReadOnlyList<DockVisibleSlot> slots,
        Func<DockItem, UIElement>? widgetFactory,
        Func<DockItem, ImageSource?>? pinIcon,
        Func<DockPinStackKind, ImageSource?>? stockIcon)
    {
        HidePinName(immediate: true);
        var existing = new Dictionary<string, Border>(StringComparer.Ordinal);
        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is Border { Tag: DockVisibleSlot tagged } slot && !string.IsNullOrWhiteSpace(tagged.Key))
            {
                existing[tagged.Key] = slot;
            }
        }

        var next = new List<Border>(slots.Count);
        foreach (var view in slots)
        {
            if (existing.TryGetValue(view.Key, out var slot) && CanReuseSlot(slot, view))
            {
                slot.Tag = view;
                var name = SlotName(view);
                AutomationProperties.SetName(slot, name);
                if (slot.Child is FrameworkElement face)
                {
                    AutomationProperties.SetName(face, name);
                }

                next.Add(slot);
                continue;
            }

            next.Add(CreateItemSlot(view, widgetFactory, pinIcon, stockIcon));
        }

        var keep = next.ToHashSet();
        for (var i = _itemsHost.Children.Count - 1; i >= 0; i--)
        {
            if (_itemsHost.Children[i] is Border slot && !keep.Contains(slot))
            {
                _itemsHost.Children.RemoveAt(i);
            }
        }

        for (var i = 0; i < next.Count; i++)
        {
            var slot = next[i];
            if (i < _itemsHost.Children.Count && ReferenceEquals(_itemsHost.Children[i], slot))
            {
                continue;
            }

            _itemsHost.Children.Remove(slot);
            if (i >= _itemsHost.Children.Count)
            {
                _itemsHost.Children.Add(slot);
            }
            else
            {
                _itemsHost.Children.Insert(i, slot);
            }
        }

        SyncSlotGaps();
        ShowEmptyHint(slots.Count == 0);
        LayoutEdgeSlots();
    }

    private void ShowEmptyHint(bool empty)
    {
        if (!empty)
        {
            if (_emptyHint is not null)
            {
                _itemsHost.Children.Remove(_emptyHint);
            }

            return;
        }

        _emptyHint ??= new Border
        {
            Tag = "empty-cta",
            Padding = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = UiText.EmptyDock,
                FontSize = 12,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };
        if (_emptyHint.Child is TextBlock hint)
        {
            hint.Text = UiText.EmptyDock;
        }

        AutomationProperties.SetName(_emptyHint, UiText.EmptyDockName);
        if (!ReferenceEquals(_emptyHint.Parent, _itemsHost))
        {
            _itemsHost.Children.Add(_emptyHint);
        }
    }

    public void SetHostWorkArea(RectLogical pixelWorkArea) => _pixelWorkArea = pixelWorkArea;

    public bool IsPointerOverIsland()
    {
        if (!IsVisible)
        {
            return false;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetCursorPos(out var pt) || !GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        return pt.X >= rect.Left && pt.X < rect.Right && pt.Y >= rect.Top && pt.Y < rect.Bottom;
    }

    public nint Handle => new WindowInteropHelper(this).Handle;

    public bool IsPointerInHotzone()
    {
        if (!GetCursorPos(out var pt))
        {
            return false;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        var island = default(RectLogical);
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
        {
            island = new RectLogical(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        return DockHostGeometry.InIslandApproachHotzone(pt.X, pt.Y, _pixelWorkArea, _edge, island);
    }

    public bool HasOpenMenu()
    {
        if (ContextMenu?.IsOpen == true)
        {
            return true;
        }

        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is FrameworkElement element && element.ContextMenu?.IsOpen == true)
            {
                return true;
            }
        }

        return false;
    }

    private Border CreateItemSlot(
        DockVisibleSlot view,
        Func<DockItem, UIElement>? widgetFactory,
        Func<DockItem, ImageSource?>? pinIcon,
        Func<DockPinStackKind, ImageSource?>? stockIcon)
    {
        if (view.IsStack && view.Stack is { } kind)
        {
            ImageSource? icon = null;
            try
            {
                icon = stockIcon?.Invoke(kind);
            }
            catch (Exception)
            {
                icon = null;
            }

            return WrapSlot(view, CreateStackPin(kind, view.Members.Count, icon, stockIcon));
        }

        var item = view.Item;
        if (item is null)
        {
            return WrapSlot(view, CreatePlaceholder(string.Empty, null));
        }

        if (DockPins.IsPin(item.Type))
        {
            return WrapSlot(view, CreatePin(item, ResolveIcon(item, pinIcon)));
        }

        if (item.Type != DockItemType.WidgetChip || string.IsNullOrWhiteSpace(item.WidgetId))
        {
            return WrapSlot(view, CreatePlaceholder(item.WidgetId ?? string.Empty, item.Title));
        }

        var widgetId = item.WidgetId;
        if (widgetFactory is not null && BuiltInWidgets.IsImplemented(widgetId))
        {
            try
            {
                var widget = widgetFactory(item);
                Detach(widget);
                return WrapSlot(view, widget);
            }
            catch (Exception ex)
            {
                WidgetCreateFailed?.Invoke(this, new WidgetCreateFailedEventArgs(widgetId, ex));
                return WrapSlot(view, CreatePlaceholder(widgetId, item.Title));
            }
        }

        return WrapSlot(view, CreatePlaceholder(widgetId, item.Title));
    }

    private static ImageSource? ResolveIcon(DockItem item, Func<DockItem, ImageSource?>? pinIcon)
    {
        try
        {
            return pinIcon?.Invoke(item);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool CanReuseSlot(Border slot, DockVisibleSlot view)
    {
        if (slot.Tag is not DockVisibleSlot tagged || tagged.IsStack != view.IsStack)
        {
            return false;
        }

        if (view.IsStack)
        {
            return tagged.Stack == view.Stack;
        }

        if (tagged.Item is null || view.Item is null || tagged.Item.Type != view.Item.Type)
        {
            return false;
        }

        var item = view.Item;
        if (item.Type == DockItemType.WidgetChip)
        {
            return !IsPlaceholderFace(slot.Child)
                && string.Equals(tagged.Item.WidgetId, item.WidgetId, StringComparison.Ordinal);
        }

        if (DockPins.IsPin(item.Type))
        {
            return SamePinFace(tagged.Item, item);
        }

        return true;
    }

    private static bool SamePinFace(DockItem tagged, DockItem item) =>
        string.Equals(tagged.Id, item.Id, StringComparison.Ordinal)
        && string.Equals(tagged.Path, item.Path, StringComparison.OrdinalIgnoreCase)
        && string.Equals(tagged.Url, item.Url, StringComparison.OrdinalIgnoreCase)
        && string.Equals(tagged.Title, item.Title, StringComparison.Ordinal)
        && string.Equals(tagged.ShortcutPath, item.ShortcutPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsPlaceholderFace(UIElement? face) =>
        face is FrameworkElement { Tag: BuiltInWidgets.PlaceholderTag };

    public void ApplyWidgetDensity(string itemId, WidgetDensity density)
    {
        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is Border slot && slot.Tag is DockVisibleSlot { Item: { } tagged } visible
                && tagged.Id == itemId)
            {
                slot.Tag = visible with { Item = tagged with { Density = density } };
                if (slot.Child is FrameworkElement view)
                {
                    view.InvalidateMeasure();
                }

                slot.InvalidateMeasure();
                break;
            }
        }
    }

    public void ApplyTheme(bool isLight) => ApplyTheme(isLight, _backdrop, _islandTransparency);

    public void ApplyTheme(bool isLight, DockBackdrop backdrop) =>
        ApplyTheme(isLight, backdrop, _islandTransparency);

    public void ApplyTheme(bool isLight, DockBackdrop backdrop, int islandTransparency)
    {
        _isLight = isLight;
        _backdrop = backdrop;
        _islandTransparency = Math.Clamp(islandTransparency, 0, 100);
        Background = Brushes.Transparent;
        _backdropOk = DwmBackdrop.TryApply(this);

        var stroke = isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke;
        _island.BorderBrush = TokenBrushes.From(stroke);
        _island.BorderThickness = new Thickness(1);
        _island.Background = TokenBrushes.From(
            DockTokens.ResolveIslandFill(isLight, _backdropOk, backdrop, _islandTransparency));
        var glyph = ForegroundBrush();
        _overflowBack.Foreground = glyph;
        _overflowForward.Foreground = glyph;
        PaintOverflowChrome();

        foreach (var child in _itemsHost.Children)
        {
            if (child is Border { Child: Button button })
            {
                button.Background = ChipBrush();
                button.Foreground = ForegroundBrush();
            }
            else if (child is Border { Child: Border tile, Tag: DockVisibleSlot view }
                     && (view.IsStack || (view.Item is { } pin && DockPins.IsPin(pin.Type))))
            {
                PaintPinFace(tile);
            }
        }

        PaintPinName();
        if (_emptyHint?.Child is TextBlock hint)
        {
            hint.Text = UiText.EmptyDock;
            AutomationProperties.SetName(_emptyHint, UiText.EmptyDockName);
        }
    }

    public void ApplyProfileMenu(IReadOnlyList<Profile> profiles, string activeProfileId, bool canUndo = false)
    {
        ContextMenu = DockContextMenuBuilder.BuildProfileMenu(
            profiles,
            activeProfileId,
            _isLight,
            _backdrop,
            _backdropOk,
            kind => PinAddRequested?.Invoke(this, kind),
            () => SettingsRequested?.Invoke(this, EventArgs.Empty),
            id => ProfileSwitchRequested?.Invoke(this, id),
            () => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public void ShowStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _statusText.Text = text;
        _statusHost.Background = TokenBrushes.From(DockTokens.ResolveMenuFill(_isLight, _backdropOk, _backdrop));
        _statusHost.BorderBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke);
        _statusHost.BorderThickness = new Thickness(1);
        _statusText.Foreground = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        _statusPopup.IsOpen = true;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void OnDockMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FindSlot(e.OriginalSource as DependencyObject) is not null)
        {
            e.Handled = true;
        }
    }

    private void OnDockRightClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || FindSlot(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (ContextMenu is not null)
        {
            ContextMenu.IsOpen = true;
        }
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            var pos = e.GetPosition(_itemsHost);
            var insert = HitInsertIndex(pos);
            ShowInsertionCaret(insert);
            e.Handled = true;
            return;
        }

        HideInsertionCaret();
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDragLeave(object sender, DragEventArgs e)
    {
        HideInsertionCaret();
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        HideInsertionCaret();
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] paths
            || paths.Length == 0)
        {
            return;
        }

        var insert = HitInsertIndex(e.GetPosition(_itemsHost));
        PinsDropped?.Invoke(this, new PinDropRequest { Paths = paths, InsertAt = insert });
        e.Handled = true;
    }

    private void ShowInsertionCaret(int insert)
    {
        var count = _itemsHost.Children.Count;
        if (count == 0)
        {
            _insertionCaret.Visibility = Visibility.Collapsed;
            return;
        }

        double targetX = 0;
        double targetY = 0;

        if (insert < count && _itemsHost.Children[insert] is FrameworkElement child)
        {
            var pt = child.TranslatePoint(new Point(0, 0), _chrome);
            targetX = pt.X - 2;
            targetY = pt.Y;
        }
        else if (_itemsHost.Children[count - 1] is FrameworkElement last)
        {
            var pt = last.TranslatePoint(new Point(last.ActualWidth, 0), _chrome);
            targetX = pt.X + 2;
            targetY = pt.Y;
        }
        else
        {
            HideInsertionCaret();
            return;
        }

        _insertionCaret.Margin = Horizontal
            ? new Thickness(Math.Max(0, targetX), 0, 0, 0)
            : new Thickness(0, Math.Max(0, targetY), 0, 0);
        _insertionCaret.Visibility = Visibility.Visible;

        for (var i = 0; i < count; i++)
        {
            if (_itemsHost.Children[i] is FrameworkElement el)
            {
                if (i >= insert)
                {
                    el.RenderTransform = new TranslateTransform(Horizontal ? 12 : 0, Horizontal ? 0 : 12);
                }
                else
                {
                    el.RenderTransform = Transform.Identity;
                }
            }
        }
    }

    private void HideInsertionCaret()
    {
        _insertionCaret.Visibility = Visibility.Collapsed;
        ClearTileShifts();
    }

    private void ShowWidgetMenu(Border slot, DockItem item)
    {
        if (string.IsNullOrWhiteSpace(item.WidgetId))
        {
            return;
        }

        var menu = DockContextMenuBuilder.BuildWidgetMenu(
            item,
            _isLight,
            _backdrop,
            _backdropOk,
            id => WidgetOpenRequested?.Invoke(this, id),
            id => WidgetCustomizeRequested?.Invoke(this, id),
            req => WidgetDensityRequested?.Invoke(this, req),
            id => WidgetRemoveRequested?.Invoke(this, id));

        slot.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void ShowPinMenu(Border slot, DockItem item)
    {
        HidePinName(immediate: true);
        var windows = WindowEnumerator?.Invoke(item) ?? [];
        var menu = DockContextMenuBuilder.BuildPinMenu(
            item,
            _isLight,
            _backdrop,
            _backdropOk,
            id => PinLaunchRequested?.Invoke(this, id),
            id => WidgetRemoveRequested?.Invoke(this, id),
            () => SettingsRequested?.Invoke(this, EventArgs.Empty),
            () => ExitRequested?.Invoke(this, EventArgs.Empty),
            windows,
            hwnd => WindowActivateRequested?.Invoke(this, hwnd),
            hwnd => WindowCloseRequested?.Invoke(this, hwnd));

        slot.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private ContextMenu NewDockMenu() =>
        DockMenu.Create(_isLight, _backdrop, _backdropOk);

    public void Dispose()
    {
        HidePinName(immediate: true);
        Close();
    }

    private bool Horizontal => IsHorizontal(_edge);

    private static bool IsHorizontal(DockEdge edge) => edge is not (DockEdge.Left or DockEdge.Right);

    private void ApplyOrientation()
    {
        var horizontal = Horizontal;
        _itemsHost.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        _scroll.HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        _scroll.PanningMode = horizontal ? PanningMode.HorizontalOnly : PanningMode.VerticalOnly;
        ((StackPanel)_overflow.Child).Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        SizeOverflowButtons(horizontal);
        _overflow.Margin = horizontal ? new Thickness(2, 0, DockTokens.ResizeGrip, 0) : new Thickness(0, 2, 0, DockTokens.ResizeGrip);
        _overflow.HorizontalAlignment = horizontal ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        _overflow.VerticalAlignment = horizontal ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _overflowBack.Margin = new Thickness(0);
        _overflowForward.Margin = horizontal
            ? new Thickness(DockTokens.OverflowButtonGap, 0, 0, 0)
            : new Thickness(0, DockTokens.OverflowButtonGap, 0, 0);
        _overflowBack.Content = OverflowGlyphText(horizontal ? PhosphorIcon.CaretLeft : PhosphorIcon.CaretUp);
        _overflowForward.Content = OverflowGlyphText(horizontal ? PhosphorIcon.CaretRight : PhosphorIcon.CaretDown);
        _overflowBack.ToolTip = horizontal ? UiText.ScrollLeft : UiText.ScrollUp;
        _overflowForward.ToolTip = horizontal ? UiText.ScrollRight : UiText.ScrollDown;

        _chrome.ColumnDefinitions.Clear();
        _chrome.RowDefinitions.Clear();
        if (horizontal)
        {
            _chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_scroll, 0);
            Grid.SetRow(_scroll, 0);
            Grid.SetColumnSpan(_scroll, 1);
            Grid.SetRowSpan(_scroll, 1);
            Grid.SetColumn(_overflow, 1);
            Grid.SetRow(_overflow, 0);
            Grid.SetColumnSpan(_overflow, 1);
            Grid.SetRowSpan(_overflow, 1);
            PlaceGrip(_startGrip, HorizontalAlignment.Left, VerticalAlignment.Stretch, DockTokens.ResizeGrip, double.NaN, Cursors.SizeWE);
            PlaceGrip(_endGrip, HorizontalAlignment.Right, VerticalAlignment.Stretch, DockTokens.ResizeGrip, double.NaN, Cursors.SizeWE);
            Grid.SetColumnSpan(_startGrip, 2);
            Grid.SetRowSpan(_startGrip, 1);
            Grid.SetColumnSpan(_endGrip, 2);
            Grid.SetRowSpan(_endGrip, 1);
        }
        else
        {
            _chrome.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _chrome.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(_scroll, 0);
            Grid.SetRow(_scroll, 0);
            Grid.SetColumnSpan(_scroll, 1);
            Grid.SetRowSpan(_scroll, 1);
            Grid.SetColumn(_overflow, 0);
            Grid.SetRow(_overflow, 1);
            Grid.SetColumnSpan(_overflow, 1);
            Grid.SetRowSpan(_overflow, 1);
            PlaceGrip(_startGrip, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, DockTokens.ResizeGrip, Cursors.SizeNS);
            PlaceGrip(_endGrip, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, DockTokens.ResizeGrip, Cursors.SizeNS);
            Grid.SetColumnSpan(_startGrip, 1);
            Grid.SetRowSpan(_startGrip, 2);
            Grid.SetColumnSpan(_endGrip, 1);
            Grid.SetRowSpan(_endGrip, 2);
        }

        SyncSlotGaps();
        LayoutEdgeSlots();
    }

    private void LayoutEdgeSlots()
    {
        var side = !Horizontal;
        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is Border { Tag: "empty-cta" } hint)
            {
                hint.Height = double.NaN;
                hint.MaxWidth = side ? DockTokens.SideTileWidth : double.PositiveInfinity;
                if (hint.Child is TextBlock copy)
                {
                    copy.TextWrapping = side ? TextWrapping.Wrap : TextWrapping.NoWrap;
                    copy.TextAlignment = side ? TextAlignment.Center : TextAlignment.Left;
                    copy.MaxWidth = side ? DockTokens.SideTileWidth - 8 : double.PositiveInfinity;
                }

                continue;
            }

            if (child is not Border { Tag: DockVisibleSlot view } slot)
            {
                continue;
            }

            var pin = view.IsStack || (view.Item is { } item && DockPins.IsPin(item.Type));
            if (pin && slot.Child is Border { Child: Grid body })
            {
                LayoutPinBody(body, slot.Child as FrameworkElement, side);
                slot.Width = side ? DockTokens.SideTileWidth : double.NaN;
                slot.Height = side ? DockTokens.SidePinHeight : DockTokens.TileHeight;
                continue;
            }

            slot.Width = double.NaN;
            slot.Height = side ? double.NaN : DockTokens.TileHeight;
        }
    }

    private void LayoutPinBody(Grid body, FrameworkElement? tile, bool side)
    {
        Ellipse? dot = null;
        FrameworkElement? face = null;
        foreach (var child in body.Children)
        {
            if (child is Ellipse { Tag: "pin-running" } running)
            {
                dot = running;
            }
            else if (child is FrameworkElement element)
            {
                face = element;
            }
        }

        if (dot is null)
        {
            return;
        }

        var faceWidth = face?.Width > 0 ? face.Width : DockTokens.ArtSize;
        if (side)
        {
            body.RowDefinitions.Clear();
            body.Width = DockTokens.SideTileWidth;
            body.Height = DockTokens.SidePinHeight;
            if (face is not null)
            {
                Grid.SetRow(face, 0);
                face.HorizontalAlignment = HorizontalAlignment.Center;
                face.VerticalAlignment = VerticalAlignment.Center;
            }

            Grid.SetRow(dot, 0);
            dot.HorizontalAlignment = _edge == DockEdge.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(4, 0, 4, 0);
        }
        else
        {
            body.RowDefinitions.Clear();
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7) });
            body.Width = faceWidth + 16;
            body.Height = DockTokens.TileHeight;
            if (face is not null)
            {
                Grid.SetRow(face, 0);
                face.HorizontalAlignment = HorizontalAlignment.Center;
                face.VerticalAlignment = VerticalAlignment.Center;
            }

            Grid.SetRow(dot, 1);
            dot.HorizontalAlignment = HorizontalAlignment.Center;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(0);
        }

        if (tile is not null)
        {
            tile.Width = body.Width;
            tile.Height = body.Height;
        }
    }

    private void UpdateOverflowChrome()
    {
        _itemsHost.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var content = Horizontal ? _itemsHost.DesiredSize.Width : _itemsHost.DesiredSize.Height;
        var inner = Horizontal
            ? Math.Max(0, _island.ActualWidth - (DockTokens.IslandPaddingX * 2) - 2)
            : Math.Max(0, _island.ActualHeight - (DockTokens.IslandPaddingY * 2) - 2);
        var needed = inner > 1 && content > inner + 2;
        var visibility = needed ? Visibility.Visible : Visibility.Collapsed;
        if (_overflow.Visibility != visibility)
        {
            _overflow.Visibility = visibility;
        }

        _itemsHost.HorizontalAlignment = needed && Horizontal ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _itemsHost.VerticalAlignment = needed && !Horizontal ? VerticalAlignment.Top : VerticalAlignment.Center;

        if (!needed)
        {
            return;
        }

        if (Horizontal)
        {
            _overflowBack.IsEnabled = _scroll.HorizontalOffset > 1;
            _overflowForward.IsEnabled = _scroll.HorizontalOffset + _scroll.ViewportWidth < _scroll.ExtentWidth - 1;
        }
        else
        {
            _overflowBack.IsEnabled = _scroll.VerticalOffset > 1;
            _overflowForward.IsEnabled = _scroll.VerticalOffset + _scroll.ViewportHeight < _scroll.ExtentHeight - 1;
        }

        PaintOverflowChrome();
    }

    private void SizeOverflowButtons(bool horizontal)
    {
        var width = horizontal ? DockTokens.OverflowButtonWidth : DockTokens.OverflowButtonHeight;
        var height = horizontal ? DockTokens.OverflowButtonHeight : DockTokens.OverflowButtonWidth;
        SizeOverflowButton(_overflowBack, width, height);
        SizeOverflowButton(_overflowForward, width, height);
    }

    private static void SizeOverflowButton(Button button, double width, double height)
    {
        button.Width = width;
        button.Height = height;
        button.MinWidth = width;
        button.MinHeight = height;
        button.MaxWidth = width;
        button.MaxHeight = height;
    }

    private void PaintOverflowChrome()
    {
        PaintOverflowButton(_overflowBack);
        PaintOverflowButton(_overflowForward);
    }

    private void PaintOverflowButton(Button button)
    {
        var hovering = button.IsMouseOver;
        button.Background = hovering
            ? TokenBrushes.From(_isLight ? 0x66FFFFFFu : 0x33000000u)
            : Brushes.Transparent;
        button.BorderBrush = hovering
            ? TokenBrushes.From(_isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke)
            : Brushes.Transparent;
        button.BorderThickness = new Thickness(1);
        PaintOverflowGlyph(button, hovering);
    }

    private void ScrollBy(double delta)
    {
        if (Horizontal)
        {
            _scroll.ScrollToHorizontalOffset(Math.Max(0, _scroll.HorizontalOffset + delta));
        }
        else
        {
            _scroll.ScrollToVerticalOffset(Math.Max(0, _scroll.VerticalOffset + delta));
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        IslandMouseWheel?.Invoke(this, e);
        if (e.Handled)
        {
            return;
        }

        ScrollBy(-Math.Sign(e.Delta) * DockTokens.ScrollStep);
        e.Handled = true;
    }

    private Border CreateGrip(bool fromStart)
    {
        var grip = new Border
        {
            Background = Brushes.Transparent,
            Tag = fromStart
        };
        grip.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount >= 2)
            {
                IslandLengthChanged?.Invoke(this, null);
                e.Handled = true;
                return;
            }

            BeginResize(fromStart);
            e.Handled = true;
        };
        return grip;
    }

    private static void PlaceGrip(
        Border grip,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        double width,
        double height,
        Cursor cursor)
    {
        grip.HorizontalAlignment = horizontal;
        grip.VerticalAlignment = vertical;
        grip.Width = width;
        grip.Height = height;
        grip.Cursor = cursor;
        Grid.SetColumn(grip, 0);
        Grid.SetRow(grip, 0);
    }

    private void OnStripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_stripMode)
        {
            return;
        }

        StripActivated?.Invoke(this, EventArgs.Empty);
    }

    private void BeginResize(bool fromStart)
    {
        _resizing = true;
        _resizeFromStart = fromStart;
        _resizeOrigin = PointToScreen(Mouse.GetPosition(this));
        _resizeStartLength = Horizontal ? Width : Height;
        CaptureMouse();
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragArmed && e.LeftButton == MouseButtonState.Pressed && _dragSlot is not null)
        {
            var pos = e.GetPosition(_itemsHost);
            if (!_dragging)
            {
                if (_tileDragSuspended || IsPointerOutsideDockAxis(pos))
                {
                    DisarmTileDrag();
                    return;
                }

                var deltaX = pos.X - _dragOrigin.X;
                var deltaY = pos.Y - _dragOrigin.Y;
                if (!DockDragConstraints.IsDragStart(deltaX, deltaY))
                {
                    return;
                }

                BeginTileDrag();
            }

            PreviewLiveOrder(pos);
            LiftDragSlot(pos);
            e.Handled = true;
            return;
        }

        if (!_resizing || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var now = PointToScreen(e.GetPosition(this));
        var delta = Horizontal ? now.X - _resizeOrigin.X : now.Y - _resizeOrigin.Y;
        if (_resizeFromStart)
        {
            delta = -delta;
        }

        var length = Math.Clamp(_resizeStartLength + (delta * 2), _minLength, _maxLength);
        ApplyLiveLength(length);
    }

    private void OnWindowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizing)
        {
            EndResize(commit: true);
            e.Handled = true;
            return;
        }

        if (_dragArmed)
        {
            CancelIslandMonitorDrag();
            var wasDragging = _dragging;
            EndTileDrag(commit: true);
            e.Handled = wasDragging;
            return;
        }

        if (_islandDragArmed)
        {
            EndIslandMonitorDrag();
            e.Handled = true;
        }
    }

    private void OnIslandBackgroundDown(object sender, MouseButtonEventArgs e)
    {
        if (_resizing || _dragArmed || _tileDragSuspended || !IsMouseOver || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.OriginalSource is Button or TextBox or TextBoxBase)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (FindSlot(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject origin
            && (ReferenceEquals(origin, _startGrip) || ReferenceEquals(origin, _endGrip)
                || FindAncestor<Border>(origin) is { } grip
                && (ReferenceEquals(grip, _startGrip) || ReferenceEquals(grip, _endGrip))))
        {
            return;
        }

        _islandDragArmed = true;
        _islandDragOrigin = CursorPixels();
        _islandDragCaptured = CaptureMouse();
    }

    private void EndIslandMonitorDrag()
    {
        if (!_islandDragArmed)
        {
            return;
        }

        var origin = _islandDragOrigin;
        var captured = _islandDragCaptured;
        _islandDragArmed = false;
        _islandDragCaptured = false;
        ReleaseIslandCapture();
        if (!captured)
        {
            return;
        }

        var now = CursorPixels();
        if (!DockDragConstraints.IsIslandMonitorDrag(now.X - origin.X, now.Y - origin.Y))
        {
            return;
        }

        IslandMonitorRequested?.Invoke(this, NativeMonitors.GetCurrentCursorMonitorId());
    }

    public void CancelIslandMonitorDrag()
    {
        _islandDragArmed = false;
        _islandDragCaptured = false;
        ReleaseIslandCapture();
    }

    private static Point CursorPixels()
    {
        if (NativeMonitors.GetCursorPos(out var pt))
        {
            return new Point(pt.X, pt.Y);
        }

        return default;
    }

    private void ReleaseIslandCapture()
    {
        if (!_dragging && !_resizing && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void EndResize(bool commit)
    {
        if (!_resizing)
        {
            return;
        }

        _resizing = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (commit)
        {
            IslandLengthChanged?.Invoke(this, Horizontal ? Width : Height);
        }
    }

    private void ApplyLiveLength(double length)
    {
        if (Horizontal)
        {
            var center = Left + (Width / 2);
            Width = length;
            Left = center - (length / 2);
        }
        else
        {
            var center = Top + (Height / 2);
            Height = length;
            Top = center - (length / 2);
        }

        UpdateOverflowChrome();
    }

    private Button CreatePlaceholder(string widgetId, string? title)
    {
        var chip = new Button
        {
            Content = (title ?? BuiltInWidgets.DisplayName(widgetId)) + UiText.LaterSuffix,
            Tag = BuiltInWidgets.PlaceholderTag,
            Margin = new Thickness(0),
            Padding = new Thickness(DockTokens.TilePaddingX, DockTokens.TilePaddingY, DockTokens.TilePaddingX, DockTokens.TilePaddingY),
            FontSize = 12,
            Opacity = 0.55,
            IsEnabled = false,
            BorderThickness = new Thickness(0),
            Background = ChipBrush(),
            Foreground = ForegroundBrush()
        };
        return chip;
    }

    private Border CreatePin(DockItem item, ImageSource? icon, bool activateOnClick = true)
    {
        var missing = string.IsNullOrWhiteSpace(item.Path)
                      || (!File.Exists(item.Path) && !Directory.Exists(item.Path));
        var (faceWidth, faceHeight) = PinFaceSize(item, icon);
        UIElement face;
        if (icon is not null)
        {
            face = new System.Windows.Controls.Image
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
            face = new PhosphorGlyph(PinGlyph(item.Type, missing), 26)
            {
                Opacity = missing ? 0.45 : 0.9,
                Foreground = ForegroundBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "pin-glyph"
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
            Panel.SetZIndex(face, 0);
            host.Children.Add(face);
            var badge = FolderBadge(item, missing, faceWidth);
            Panel.SetZIndex(badge, 1);
            host.Children.Add(badge);
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
        PaintRunningDot(running);
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
            Width = faceWidth + 16,
            Height = DockTokens.TileHeight,
            Background = Brushes.Transparent,
            Child = body
        };
        tile.Focusable = true;
        AutomationProperties.SetName(tile, PinTitle(item));
        if (activateOnClick)
        {
            tile.MouseLeftButtonUp += (_, e) =>
            {
                if (_dragging)
                {
                    return;
                }

                PinActivateRequested?.Invoke(this, item.Id);
                e.Handled = true;
            };
            tile.PreviewKeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Space))
                {
                    return;
                }

                PinActivateRequested?.Invoke(this, item.Id);
                e.Handled = true;
            };
        }

        return tile;
    }

    private Border CreateStackPin(
        DockPinStackKind kind,
        int count,
        ImageSource? icon,
        Func<DockPinStackKind, ImageSource?>? stockIcon)
    {
        var type = kind == DockPinStackKind.Folders ? DockItemType.Folder : DockItemType.App;
        var measure = icon;
        if (kind != DockPinStackKind.Folders)
        {
            try
            {
                measure = stockIcon?.Invoke(DockPinStackKind.Folders) ?? icon;
            }
            catch (Exception)
            {
                measure = icon;
            }
        }

        var (faceWidth, faceHeight) = StackFaceSize(measure);
        UIElement content;
        if (icon is not null)
        {
            content = new System.Windows.Controls.Image
            {
                Source = icon,
                Width = faceWidth,
                Height = faceHeight,
                Stretch = kind == DockPinStackKind.Folders ? Stretch.Uniform : Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(content, BitmapScalingMode.HighQuality);
        }
        else
        {
            faceWidth = DockTokens.ArtSize;
            faceHeight = DockTokens.ArtSize;
            content = new PhosphorGlyph(PinGlyph(type, missing: false), 26)
            {
                Opacity = 0.9,
                Foreground = ForegroundBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "pin-glyph"
            };
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
        PaintRunningDot(running);
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
            Width = faceWidth + 16,
            Height = DockTokens.TileHeight,
            Background = Brushes.Transparent,
            Child = body
        };
        tile.Focusable = true;
        AutomationProperties.SetName(tile, DockPinCollapse.StackTitle(kind, count));
        return tile;
    }

    private static (double Width, double Height) StackFaceSize(ImageSource? icon)
    {
        const double height = DockTokens.ArtSize;
        if (icon is BitmapSource bitmap && bitmap.PixelHeight > 0)
        {
            var width = height * bitmap.PixelWidth / (double)bitmap.PixelHeight;
            return (Math.Clamp(width, height, height * 1.65), height);
        }

        return (height, height);
    }

    private static (double Width, double Height) PinFaceSize(DockItem item, ImageSource? icon)
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

    private Border FolderBadge(DockItem item, bool missing, double faceWidth)
    {
        var label = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = Math.Max(24, faceWidth - 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        FolderCaption.Apply(label, DockPins.FolderBadge(PinTitle(item)));
        var badge = new Border
        {
            Tag = "pin-badge",
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = missing ? 0.45 : 1,
            Child = label
        };
        PaintFolderBadge(badge);
        return badge;
    }

    public void SetPinRunning(IReadOnlySet<string> runningIds)
    {
        foreach (var child in _itemsHost.Children)
        {
            if (child is not Border { Tag: DockVisibleSlot view } slot)
            {
                continue;
            }

            var on = view.IsStack
                ? view.Members.Any(member => runningIds.Contains(member.Id))
                : view.Item is { } item && DockPins.IsPin(item.Type) && runningIds.Contains(item.Id);
            if (FindRunningDot(slot) is { } dot)
            {
                dot.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private static Ellipse? FindRunningDot(Border slot)
    {
        if (slot.Child is not Border tile || tile.Child is not Grid body)
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

    private void PaintPinFace(Border tile)
    {
        if (tile.Child is Grid body)
        {
            foreach (var part in body.Children)
            {
                if (part is Ellipse { Tag: "pin-running" } dot)
                {
                    PaintRunningDot(dot);
                }
                else
                {
                    PaintPinGlyph(part);
                }
            }

            return;
        }

        PaintPinGlyph(tile.Child);
    }

    private void PaintPinGlyph(object? part)
    {
        switch (part)
        {
            case TextBlock glyph:
                glyph.Foreground = ForegroundBrush();
                break;
            case PhosphorGlyph icon:
                icon.Foreground = ForegroundBrush();
                break;
            case Grid host:
                foreach (var nested in host.Children)
                {
                    if (nested is TextBlock glyph)
                    {
                        glyph.Foreground = ForegroundBrush();
                    }
                    else if (nested is PhosphorGlyph icon)
                    {
                        icon.Foreground = ForegroundBrush();
                    }
                    else if (nested is Border { Tag: "pin-badge" } badge)
                    {
                        PaintFolderBadge(badge);
                    }
                }

                break;
        }
    }

    private void PaintRunningDot(Ellipse dot) =>
        dot.Fill = TokenBrushes.From(_isLight ? 0xFF1A1A1A : 0xFFFFFFFF);

    private void PaintFolderBadge(Border badge)
    {
        badge.Background = Brushes.Transparent;
        if (badge.Child is TextBlock label)
        {
            label.Foreground = TokenBrushes.From(0xFF1A1A1A);
        }
    }

    private void PreparePinName()
    {
        _pinNameText.FontFamily = UiFont.Family;
        _pinNameText.FontSize = 12;
        _pinNameText.TextWrapping = TextWrapping.Wrap;
        _pinNameText.TextTrimming = TextTrimming.CharacterEllipsis;
        _pinNameText.MaxWidth = 220;
        _pinNameHost.CornerRadius = new CornerRadius(DockTokens.TileCornerRadius);
        _pinNameHost.Padding = new Thickness(8, 4, 8, 4);
        _pinNameHost.SnapsToDevicePixels = true;
        _pinNameHost.IsHitTestVisible = false;
        _pinNameHost.RenderTransform = _pinNameShift;
        _pinNameHost.Child = _pinNameText;
        _pinNameHost.Effect = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 2,
            Direction = 270,
            Opacity = 0.22,
            Color = Colors.Black
        };
        _pinNamePopup.AllowsTransparency = true;
        _pinNamePopup.StaysOpen = true;
        _pinNamePopup.Focusable = false;
        _pinNamePopup.IsHitTestVisible = false;
        _pinNamePopup.Placement = PlacementMode.Custom;
        _pinNamePopup.CustomPopupPlacementCallback = PlaceCenteredPinName;
        _pinNamePopup.Child = _pinNameHost;
        PaintPinName();
    }

    private void PrepareStatus()
    {
        _statusText.FontFamily = UiFont.Family;
        _statusText.FontSize = 12;
        _statusText.TextWrapping = TextWrapping.NoWrap;
        _statusHost.CornerRadius = new CornerRadius(DockTokens.TileCornerRadius);
        _statusHost.Padding = new Thickness(10, 6, 10, 6);
        _statusHost.IsHitTestVisible = false;
        _statusHost.Child = _statusText;
        _statusPopup.AllowsTransparency = true;
        _statusPopup.StaysOpen = true;
        _statusPopup.Focusable = false;
        _statusPopup.IsHitTestVisible = false;
        _statusPopup.Placement = PlacementMode.Bottom;
        _statusPopup.PlacementTarget = _island;
        _statusPopup.VerticalOffset = 8;
        _statusPopup.Child = _statusHost;
        _statusPopup.Opened += (_, _) => PopupTopmost.TryElevate(_statusPopup);
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _statusPopup.IsOpen = false;
        };
    }

    private void PaintPinName()
    {
        _pinNameHost.Background = TokenBrushes.From(DockTokens.ResolveMenuFill(_isLight, _backdropOk, _backdrop));
        _pinNameHost.BorderBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke);
        _pinNameHost.BorderThickness = new Thickness(1);
        _pinNameText.Foreground = ForegroundBrush();
    }

    private static string PinTitle(DockItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ? item.Path ?? UiText.ShortcutFallback : item.Title;

    private void ShowPinName(FrameworkElement slot, string title)
    {
        if (_dragging || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _pinNameToken++;
        _pinNameText.Text = title;
        PaintPinName();
        PlacePinName(slot);
        var (fromX, fromY) = PinNameSlide();
        var reopen = !_pinNamePopup.IsOpen;
        _pinNamePopup.IsOpen = true;
        _pinNameHost.BeginAnimation(UIElement.OpacityProperty, null);
        _pinNameShift.BeginAnimation(TranslateTransform.XProperty, null);
        _pinNameShift.BeginAnimation(TranslateTransform.YProperty, null);
        if (reopen)
        {
            _pinNameHost.Opacity = 0;
            _pinNameShift.X = fromX;
            _pinNameShift.Y = fromY;
        }

        var duration = TimeSpan.FromMilliseconds(reopen ? 160 : 90);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        _pinNameHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, duration) { EasingFunction = ease });
        _pinNameShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, duration) { EasingFunction = ease });
        _pinNameShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = ease });
    }

    private void HidePinName(bool immediate = false)
    {
        var token = ++_pinNameToken;
        if (!_pinNamePopup.IsOpen)
        {
            return;
        }

        if (immediate)
        {
            _pinNameHost.BeginAnimation(UIElement.OpacityProperty, null);
            _pinNameShift.BeginAnimation(TranslateTransform.XProperty, null);
            _pinNameShift.BeginAnimation(TranslateTransform.YProperty, null);
            _pinNamePopup.IsOpen = false;
            return;
        }

        var (toX, toY) = PinNameSlide();
        var duration = TimeSpan.FromMilliseconds(140);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            if (token == _pinNameToken)
            {
                _pinNamePopup.IsOpen = false;
            }
        };
        _pinNameHost.BeginAnimation(UIElement.OpacityProperty, fade);
        _pinNameShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(toX, duration) { EasingFunction = ease });
        _pinNameShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(toY, duration) { EasingFunction = ease });
    }

    private void PlacePinName(FrameworkElement slot)
    {
        _pinNamePopup.PlacementTarget = PinNameAnchor(slot);
        _pinNamePopup.HorizontalOffset = 0;
        _pinNamePopup.VerticalOffset = 0;
    }

    private CustomPopupPlacement[] PlaceCenteredPinName(System.Windows.Size popupSize, System.Windows.Size targetSize, Point offset)
    {
        const double gap = 6;
        var point = _edge switch
        {
            DockEdge.Bottom => new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - gap),
            DockEdge.Left => new Point(targetSize.Width + gap, (targetSize.Height - popupSize.Height) / 2),
            DockEdge.Right => new Point(-popupSize.Width - gap, (targetSize.Height - popupSize.Height) / 2),
            _ => new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height + gap)
        };
        return [new CustomPopupPlacement(point, PopupPrimaryAxis.None)];
    }

    private static FrameworkElement PinNameAnchor(FrameworkElement slot)
    {
        if (slot is Border { Child: Border { Child: Grid body } }
            && body.Children.Count > 0
            && body.Children[0] is FrameworkElement face)
        {
            return face;
        }

        return slot;
    }

    private (double X, double Y) PinNameSlide() => _edge switch
    {
        DockEdge.Bottom => (0, 12),
        DockEdge.Left => (-12, 0),
        DockEdge.Right => (12, 0),
        _ => (0, -12)
    };

    private static string PinGlyph(DockItemType type, bool missing)
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

    private Border WrapSlot(DockVisibleSlot view, UIElement face)
    {
        if (face is FrameworkElement inner)
        {
            inner.Margin = new Thickness(0);
            inner.VerticalAlignment = VerticalAlignment.Center;
            inner.HorizontalAlignment = HorizontalAlignment.Center;
            if (view.IsStack || view.Item is { Type: DockItemType.WidgetChip } ||
                (view.Item is { } pin && DockPins.IsPin(pin.Type)))
            {
                inner.Cursor = Cursors.Hand;
            }
        }

        var interactive = view.IsStack
                          || view.Item is { Type: DockItemType.WidgetChip }
                          || (view.Item is { } item && DockPins.IsPin(item.Type));
        var slot = new Border
        {
            Tag = view,
            Background = Brushes.Transparent,
            Height = DockTokens.TileHeight,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = false,
            Cursor = interactive ? Cursors.Hand : Cursors.Arrow,
            Child = face
        };
        if (interactive)
        {
            var pressScale = new ScaleTransform(1, 1);
            slot.RenderTransform = pressScale;
            slot.RenderTransformOrigin = new Point(0.5, 0.5);
            slot.PreviewMouseLeftButtonDown += (_, _) =>
            {
                pressScale.ScaleX = 0.93;
                pressScale.ScaleY = 0.93;
            };
            slot.PreviewMouseLeftButtonUp += (_, _) =>
            {
                pressScale.ScaleX = 1.0;
                pressScale.ScaleY = 1.0;
            };
            slot.MouseLeave += (_, _) =>
            {
                pressScale.ScaleX = 1.0;
                pressScale.ScaleY = 1.0;
            };
        }
        AutomationProperties.SetName(slot, SlotName(view));
        if (view.IsStack || (view.Item is { } pinItem && DockPins.IsPin(pinItem.Type)))
        {
            slot.Focusable = true;
            slot.PreviewKeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Space) || slot.Tag is not DockVisibleSlot current)
                {
                    return;
                }

                RaiseSlotActivate(slot, current);
                e.Handled = true;
            };
        }

        if (view.Item is { Type: DockItemType.WidgetChip })
        {
            slot.PreviewMouseRightButtonUp += (_, e) =>
            {
                if (slot.Tag is DockVisibleSlot { Item: { } current })
                {
                    ShowWidgetMenu(slot, current);
                }

                e.Handled = true;
            };
        }
        else if (view.Item is { } pin && DockPins.IsPin(pin.Type))
        {
            slot.PreviewMouseRightButtonUp += (_, e) =>
            {
                if (slot.Tag is DockVisibleSlot { Item: { } current })
                {
                    ShowPinMenu(slot, current);
                }

                e.Handled = true;
            };
            slot.MouseEnter += (_, _) =>
            {
                if (slot.Tag is DockVisibleSlot current)
                {
                    ShowPinName(slot, SlotTitle(current));
                }
            };
            slot.MouseLeave += (_, _) => HidePinName();
        }
        else if (view.IsStack)
        {
            slot.MouseLeftButtonUp += (_, e) =>
            {
                if (_dragging || slot.Tag is not DockVisibleSlot current)
                {
                    return;
                }

                RaiseSlotActivate(slot, current);
                e.Handled = true;
            };
            slot.MouseEnter += (_, _) =>
            {
                if (slot.Tag is DockVisibleSlot current)
                {
                    ShowPinName(slot, SlotTitle(current));
                }
            };
            slot.MouseLeave += (_, _) => HidePinName();
        }

        return slot;
    }

    private void RaiseSlotActivate(FrameworkElement target, DockVisibleSlot view)
    {
        if (view.IsStack && view.Stack is { } kind)
        {
            HidePinName(immediate: true);
            PinStackToggleRequested?.Invoke(this, new PinStackToggleRequest
            {
                Kind = kind,
                Members = view.Members,
                Target = target
            });
            return;
        }

        if (view.Item is { } item && DockPins.IsPin(item.Type))
        {
            PinActivateRequested?.Invoke(this, item.Id);
        }
    }

    private static string SlotName(DockVisibleSlot view)
    {
        if (view.IsStack && view.Stack is { } kind)
        {
            return DockPinCollapse.StackTitle(kind, view.Members.Count);
        }

        return view.Item is { } item ? SlotName(item) : UiText.PinFallback;
    }

    private static string SlotTitle(DockVisibleSlot view)
    {
        if (view.IsStack && view.Stack is { } kind)
        {
            return DockPinCollapse.StackTitle(kind, view.Members.Count);
        }

        return view.Item is { } item ? PinTitle(item) : string.Empty;
    }

    private static string SlotName(DockItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title;
        }

        if (item.Type == DockItemType.WidgetChip)
        {
            return BuiltInWidgets.DisplayName(item.WidgetId ?? string.Empty);
        }

        return item.Path ?? UiText.PinFallback;
    }

    private void SyncSlotGaps()
    {
        var count = _itemsHost.Children.Count;
        for (var i = 0; i < count; i++)
        {
            if (_itemsHost.Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var trailing = i < count - 1 ? DockTokens.TileGap : 0;
            child.Margin = Horizontal
                ? new Thickness(0, 0, trailing, 0)
                : new Thickness(0, 0, 0, trailing);
        }
    }

    public void SetTileDragSuspended(bool suspended)
    {
        _tileDragSuspended = suspended;
        if (!_dragging)
        {
            DisarmTileDrag();
        }

        CancelIslandMonitorDrag();
    }

    private bool IsPointerOutsideDockAxis(Point pos)
    {
        var cross = Horizontal ? pos.Y : pos.X;
        var extent = Horizontal ? _itemsHost.ActualHeight : _itemsHost.ActualWidth;
        return DockDragConstraints.IsCrossAxisOutside(cross, extent);
    }

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_resizing || _tileDragSuspended || e.OriginalSource is Button or TextBox or TextBoxBase)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject origin && FindAncestor<Button>(origin) is not null)
        {
            return;
        }

        var slot = FindSlot(e.OriginalSource as DependencyObject);
        if (slot is null)
        {
            return;
        }

        CancelIslandMonitorDrag();
        _dragSlot = slot;
        _dragFrom = _itemsHost.Children.IndexOf(slot);
        _dragLiveIndex = _dragFrom;
        _dragOrigin = e.GetPosition(_itemsHost);
        _grabOffset = e.GetPosition(slot);
        _dragArmed = _dragFrom >= 0;
        _dragging = false;
    }

    private void BeginTileDrag()
    {
        if (_dragging || _dragSlot is null)
        {
            return;
        }

        _dragging = true;
        HidePinName(immediate: true);
        _dragLift = new TranslateTransform();
        _dragSlot.RenderTransform = _dragLift;
        _dragSlot.Effect = new DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 0,
            Opacity = 0.32,
            Color = Colors.Black
        };
        _dragSlot.Opacity = 0.92;
        _dragSlot.IsHitTestVisible = false;
        Panel.SetZIndex(_dragSlot, 100);
        Mouse.OverrideCursor = Horizontal ? Cursors.SizeWE : Cursors.SizeNS;
        CaptureMouse();
        LiftDragSlot(Mouse.GetPosition(_itemsHost));
    }

    private void LiftDragSlot(Point mouseInHost)
    {
        if (_dragSlot is null || _dragLift is null)
        {
            return;
        }

        var origin = SlotLayoutOrigin(_dragSlot);
        var lift = DockDragConstraints.ConstrainLift(
            origin.X,
            origin.Y,
            mouseInHost.X - _grabOffset.X,
            mouseInHost.Y - _grabOffset.Y,
            _dragSlot.ActualWidth,
            _dragSlot.ActualHeight,
            DragViewportInHost(),
            Horizontal);
        _dragLift.X = lift.X;
        _dragLift.Y = lift.Y;
    }

    private RectLogical DragViewportInHost()
    {
        var width = _scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : _scroll.ActualWidth;
        var height = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : _scroll.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return new RectLogical(0, 0, Math.Max(1, _itemsHost.ActualWidth), Math.Max(1, _itemsHost.ActualHeight));
        }

        var topLeft = _scroll.TranslatePoint(new Point(0, 0), _itemsHost);
        return new RectLogical(topLeft.X, topLeft.Y, width, height);
    }

    private Point SlotLayoutOrigin(FrameworkElement slot)
    {
        var visual = slot.TranslatePoint(new Point(0, 0), _itemsHost);
        if (_dragLift is null)
        {
            return visual;
        }

        return new Point(visual.X - _dragLift.X, visual.Y - _dragLift.Y);
    }

    private void EndTileDrag(bool commit)
    {
        if (!_dragArmed)
        {
            return;
        }

        if (!_dragging)
        {
            DisarmTileDrag();
            return;
        }

        var from = _dragFrom;
        var to = _dragLiveIndex;
        var dragged = _dragging;
        _dragArmed = false;
        _dragging = false;
        _dragFrom = -1;
        _dragLiveIndex = -1;
        Mouse.OverrideCursor = null;
        _dragLift = null;
        ClearTileShifts();
        if (_dragSlot is not null)
        {
            _dragSlot.Effect = null;
            _dragSlot.Opacity = 1;
            _dragSlot.IsHitTestVisible = true;
            Panel.SetZIndex(_dragSlot, 0);
        }

        var slot = _dragSlot;
        _dragSlot = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (!commit || !dragged || slot is null || from < 0)
        {
            return;
        }

        to = Math.Clamp(to, 0, Math.Max(0, _itemsHost.Children.Count - 1));
        if (to == from)
        {
            return;
        }

        ItemsReordered?.Invoke(this, new DockReorderRequest { From = from, To = to });
    }

    private void DisarmTileDrag()
    {
        _dragArmed = false;
        _dragging = false;
        _dragFrom = -1;
        _dragLiveIndex = -1;
        _dragSlot = null;
        _dragLift = null;
    }

    private void PreviewLiveOrder(Point positionInHost)
    {
        if (_dragSlot is null || _dragLiveIndex < 0)
        {
            return;
        }

        var insert = HitInsertIndex(positionInHost);
        var to = insert > _dragLiveIndex ? insert - 1 : insert;
        to = Math.Clamp(to, 0, Math.Max(0, _itemsHost.Children.Count - 1));
        if (to == _dragLiveIndex)
        {
            return;
        }

        SlideSlot(_dragSlot, to);
        _dragLiveIndex = to;
    }

    private void SlideSlot(FrameworkElement slot, int to)
    {
        var from = _itemsHost.Children.IndexOf(slot);
        if (from < 0 || from == to)
        {
            return;
        }

        var visuals = new Dictionary<FrameworkElement, Point>();
        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is FrameworkElement element && !ReferenceEquals(element, slot))
            {
                visuals[element] = element.TranslatePoint(new Point(0, 0), _itemsHost);
            }
        }

        _itemsHost.Children.Remove(slot);
        _itemsHost.Children.Insert(to, slot);
        SyncSlotGaps();
        foreach (var element in visuals.Keys)
        {
            StopShift(element);
        }

        _itemsHost.UpdateLayout();

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        foreach (var (element, origin) in visuals)
        {
            var now = element.TranslatePoint(new Point(0, 0), _itemsHost);
            var dx = origin.X - now.X;
            var dy = origin.Y - now.Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
            {
                continue;
            }

            var shift = new TranslateTransform(dx, dy);
            element.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, duration) { EasingFunction = ease });
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, duration) { EasingFunction = ease });
        }
    }

    private void ClearTileShifts()
    {
        foreach (UIElement child in _itemsHost.Children)
        {
            if (child is FrameworkElement element)
            {
                StopShift(element);
            }
        }
    }

    private static void StopShift(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
        }

        element.RenderTransform = Transform.Identity;
    }

    private int HitInsertIndex(Point pos)
    {
        var axis = Horizontal ? pos.X : pos.Y;
        var acc = 0.0;
        for (var i = 0; i < _itemsHost.Children.Count; i++)
        {
            if (_itemsHost.Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var leading = Horizontal ? child.Margin.Left : child.Margin.Top;
            var size = Horizontal ? child.ActualWidth : child.ActualHeight;
            var trailing = Horizontal ? child.Margin.Right : child.Margin.Bottom;
            if (axis < acc + leading + (size / 2))
            {
                return i;
            }

            acc += leading + size + trailing;
        }

        return _itemsHost.Children.Count;
    }

    private Border? FindSlot(DependencyObject? origin)
    {
        var current = origin;
        while (current is not null && !ReferenceEquals(current, _itemsHost))
        {
            if (current is Border { Tag: DockVisibleSlot } border && _itemsHost.Children.Contains(border))
            {
                return border;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private Border? FindWidgetSlot(DependencyObject? origin)
    {
        var slot = FindSlot(origin);
        return slot?.Tag is DockVisibleSlot { Item.Type: DockItemType.WidgetChip } ? slot : null;
    }

    private static T? FindAncestor<T>(DependencyObject? origin) where T : DependencyObject
    {
        var current = origin;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private Button OverflowGlyph(string glyph, string tooltip, Action click)
    {
        var button = new Button
        {
            Content = OverflowGlyphText(glyph),
            Width = DockTokens.OverflowButtonWidth,
            Height = DockTokens.OverflowButtonHeight,
            MinWidth = DockTokens.OverflowButtonWidth,
            MinHeight = DockTokens.OverflowButtonHeight,
            MaxWidth = DockTokens.OverflowButtonWidth,
            MaxHeight = DockTokens.OverflowButtonHeight,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Focusable = true,
            ClipToBounds = true,
            SnapsToDevicePixels = true,
            OverridesDefaultStyle = true,
            Opacity = 0.62,
            Template = OverflowButtonTemplate()
        };
        AutomationProperties.SetName(button, tooltip);
        button.Click += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        button.MouseEnter += (_, _) => PaintOverflowButton(button);
        button.MouseLeave += (_, _) => PaintOverflowButton(button);
        button.IsEnabledChanged += (_, _) => PaintOverflowButton(button);
        return button;
    }

    private static ControlTemplate OverflowButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(DockTokens.TileCornerRadius));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetValue(UIElement.ClipToBoundsProperty, true);
        border.SetBinding(Border.BackgroundProperty, Templated("Background"));
        border.SetBinding(Border.BorderBrushProperty, Templated("BorderBrush"));
        border.SetBinding(Border.BorderThicknessProperty, Templated("BorderThickness"));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(UIElement.IsHitTestVisibleProperty, false);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static Binding Templated(string property) => new(property)
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
    };

    private static void PaintOverflowGlyph(Button button, bool hovering = false)
    {
        button.Opacity = !button.IsEnabled ? 0.28 : hovering ? 1 : 0.62;
    }

    private static PhosphorGlyph OverflowGlyphText(string glyph) => new(glyph, DockTokens.OverflowGlyphSize)
    {
        Margin = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };

    private SolidColorBrush ChipBrush() => TokenBrushes.From(DockTokens.ResolveTileFill(_isLight, _backdrop));

    private SolidColorBrush ForegroundBrush() => TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

    private static void Detach(UIElement element)
    {
        switch (element)
        {
            case FrameworkElement { Parent: Panel panel } fe:
                panel.Children.Remove(fe);
                break;
            case FrameworkElement { Parent: Decorator decorator }:
                decorator.Child = null;
                break;
            case FrameworkElement { Parent: ContentControl content }:
                content.Content = null;
                break;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public sealed class PinStackToggleRequest
{
    public required DockPinStackKind Kind { get; init; }

    public required IReadOnlyList<DockItem> Members { get; init; }

    public required FrameworkElement Target { get; init; }
}

public sealed class DockReorderRequest
{
    public required int From { get; init; }

    public required int To { get; init; }
}

public sealed class PinDropRequest
{
    public required IReadOnlyList<string> Paths { get; init; }

    public int? InsertAt { get; init; }
}

/// <summary>Unpainted island that still receives clicks so DWM glass can show through.</summary>
internal sealed class HitIsland : Border
{
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
        new PointHitTestResult(this, hitTestParameters.HitPoint);
}

internal static class TokenBrushes
{
    public static SolidColorBrush From(uint argb)
    {
        var (a, r, g, b) = DockTokens.Split(argb);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
