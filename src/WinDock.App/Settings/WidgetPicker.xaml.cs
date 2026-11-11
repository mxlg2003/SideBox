using System.IO;
using System.Windows;
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
using WinDock.Widgets;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;

namespace WinDock.App;

public partial class WidgetPicker : UserControl
{
    private SolidColorBrush _textBrush = Freeze(DockTokens.Settings.Light.Text);
    private SolidColorBrush _mutedBrush = Freeze(DockTokens.Settings.Light.Muted);
    private SolidColorBrush _previewBrush = Freeze(DockTokens.Settings.Light.Preview);
    private SolidColorBrush _navSelected = Freeze(DockTokens.Settings.Light.NavSelected);
    private readonly SolidColorBrush _accent = Freeze(DockTokens.Settings.Accent);

    private DockSession? _session;
    private string _categoryId = WidgetCatalog.AllId;
    private string _query = string.Empty;
    private string _profileName = UiText.OnIsland;
    private bool _suppress;
    private bool _catalogActive;
    private FrameworkElement? _activeDragSlot;
    private TranslateTransform? _activeDragLift;
    private Point _activeDragOrigin;
    private Point _activeGrabOffset;
    private int _activeDragFrom = -1;
    private int _activeDragLiveIndex = -1;
    private bool _activeDragArmed;
    private bool _isActiveDragging;
    private readonly List<Action<DateTime>> _clockTicks = [];
    private DispatcherTimer? _clockTimer;

    public WidgetPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncClockTimer();
        Unloaded += (_, _) => _clockTimer?.Stop();
        IsVisibleChanged += (_, _) => SyncClockTimer();

        ActiveWidgetsScroll.PreviewMouseLeftButtonDown += OnActiveWidgetMouseDown;
        ActiveWidgetsScroll.PreviewMouseMove += OnActiveWidgetMouseMove;
        ActiveWidgetsScroll.PreviewMouseLeftButtonUp += OnActiveWidgetMouseUp;
        ActiveWidgetsScroll.LostMouseCapture += OnActiveWidgetLostMouseCapture;
        PreviewKeyDown += OnWidgetPickerPreviewKeyDown;
    }

    public void Attach(DockSession session) => _session = session;

    public void SetProfileName(string name)
    {
        _profileName = string.IsNullOrWhiteSpace(name) ? UiText.OnIsland : name;
    }

    public void SetCatalogActive(bool active)
    {
        _catalogActive = active;
        SyncClockTimer();
    }

    public void ApplyTheme(bool isLight, bool rebuild = true)
    {
        _textBrush = Freeze(DockTokens.Settings.Text(isLight));
        _mutedBrush = Freeze(DockTokens.Settings.Muted(isLight));
        _previewBrush = Freeze(DockTokens.Settings.Preview(isLight));
        _navSelected = Freeze(DockTokens.Settings.NavSelected(isLight));
        if (TopBarBorder is not null)
        {
            TopBarBorder.BorderBrush = Freeze(DockTokens.Settings.Stroke(isLight));
        }

        if (ActiveWidgetsPanel is not null)
        {
            ActiveWidgetsPanel.Background = Freeze(DockTokens.Settings.Card(isLight));
            ActiveWidgetsPanel.BorderBrush = Freeze(DockTokens.Settings.Stroke(isLight));
        }

        if (CatalogScroll is not null)
        {
            CatalogScroll.Background = Brushes.Transparent;
            WidgetChrome.PaintScrollViewer(CatalogScroll, isLight);
        }

        if (ActiveWidgetsScroll is not null)
        {
            WidgetChrome.PaintScrollViewer(ActiveWidgetsScroll, isLight);
        }

        if (SearchHost is not null)
        {
            SettingsChrome.ApplySearch(SearchHost, SearchPlaceholder, SearchBox, isLight);
        }

        if (rebuild)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        _suppress = true;
        if (SearchBox.Text != _query)
        {
            SearchBox.Text = _query;
        }

        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        SearchPlaceholder.Text = UiText.SearchWidgets;
        if (SearchClearBtn is not null)
        {
            SearchClearBtn.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
        System.Windows.Automation.AutomationProperties.SetName(SearchBox, UiText.SearchWidgets);

        RebuildNav();
        RebuildCatalog();
        _suppress = false;
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (SearchClearBtn is not null)
        {
            SearchClearBtn.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
        RebuildCatalog();
    }

    private void SearchClearBtn_OnClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus();
    }

    private void CatalogScroll_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var cols = CatalogScroll.ActualWidth >= 560 ? 3 : 2;
        foreach (var child in CatalogHost.Children)
        {
            if (child is UniformGrid ug && ug.Columns != cols)
            {
                ug.Columns = cols;
            }
        }
    }

    private void RebuildNav()
    {
        NavHost.Children.Clear();
        var isLight = _session?.IsLightTheme() ?? true;
        foreach (var category in WidgetCatalog.Nav)
        {
            var selected = string.Equals(category.Id, _categoryId, StringComparison.Ordinal);
            var normalBg = selected ? _accent : (isLight ? Freeze(0x14000000) : Freeze(0x26FFFFFF));
            var hoverBg = selected ? _accent : (isLight ? Freeze(0x22000000) : Freeze(0x38FFFFFF));

            var pill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 4, 12, 4),
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Background = normalBg,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = category.Id,
                Child = new TextBlock
                {
                    Text = category.Title,
                    FontSize = 12,
                    FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = selected ? Brushes.White : _textBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            pill.MouseEnter += (_, _) =>
            {
                if (!string.Equals((string)pill.Tag, _categoryId, StringComparison.Ordinal))
                {
                    pill.Background = hoverBg;
                }
            };
            pill.MouseLeave += (_, _) =>
            {
                if (!string.Equals((string)pill.Tag, _categoryId, StringComparison.Ordinal))
                {
                    pill.Background = normalBg;
                }
            };

            pill.MouseLeftButtonUp += (_, _) =>
            {
                if (pill.Tag is not string id || string.Equals(_categoryId, id, StringComparison.Ordinal))
                {
                    return;
                }

                _categoryId = id;
                RebuildNav();
                RebuildCatalog();
            };
            NavHost.Children.Add(pill);
        }
    }

    private void RebuildCatalog()
    {
        CatalogHost.Children.Clear();
        _clockTicks.Clear();
        if (_session is null)
        {
            return;
        }

        var groups = WidgetCatalog.VisibleGroups(_categoryId, _query);
        if (groups.Count == 0)
        {
            CatalogHost.Children.Add(EmptyState(UiText.CatalogEmptyTitle, UiText.CatalogEmptyHint));
        }
        else
        {
            var cols = CatalogScroll.ActualWidth >= 560 ? 3 : 2;
            foreach (var group in groups)
            {
                CatalogHost.Children.Add(new TextBlock
                {
                    Text = group.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textBrush,
                    Margin = new Thickness(4, 12, 0, 8)
                });

                var grid = new UniformGrid
                {
                    Columns = cols,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                foreach (var id in group.WidgetIds)
                {
                    grid.Children.Add(CreateCard(id));
                }

                CatalogHost.Children.Add(grid);
            }
        }

        RebuildActiveWidgets();
        SyncClockTimer();
        PulseClocks();
    }

    private Button CreateGhostActionButton(string phosphorIcon, string tooltip, bool isDanger, bool isEnabled, Action onClick)
    {
        var btn = new Button
        {
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = isEnabled ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            ToolTip = tooltip,
            IsEnabled = isEnabled,
            FocusVisualStyle = null
        };

        var glyph = new PhosphorGlyph(phosphorIcon, 12)
        {
            Foreground = _mutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = isEnabled ? 1.0 : 0.25
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Child = glyph
        };

        btn.Content = border;

        if (isEnabled)
        {
            btn.MouseEnter += (_, _) =>
            {
                var isLight = _session?.IsLightTheme() ?? true;
                if (isDanger)
                {
                    border.Background = Freeze(0x1FDC2626);
                    glyph.Foreground = Freeze(0xFFDC2626);
                }
                else
                {
                    border.Background = isLight ? Freeze(0x14000000) : Freeze(0x26FFFFFF);
                    glyph.Foreground = _textBrush;
                }
            };
            btn.MouseLeave += (_, _) =>
            {
                border.Background = Brushes.Transparent;
                glyph.Foreground = _mutedBrush;
            };
        }

        btn.Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return btn;
    }

    private void RebuildActiveWidgets()
    {
        ActiveWidgetsHost.Children.Clear();
        if (_session?.CurrentDiff is null)
        {
            return;
        }

        var allDockItems = _session.CurrentDiff.Dock.Items;
        var items = allDockItems
            .Where(i => !string.IsNullOrWhiteSpace(i.WidgetId))
            .ToList();

        ActiveWidgetsTitle.Text = UiText.ActiveWidgetsTitle;
        ActiveWidgetsCount.Text = $"{items.Count} 个";

        if (items.Count == 0)
        {
            var emptyHost = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 36, 0, 0)
            };
            emptyHost.Children.Add(new PhosphorGlyph(PhosphorIcon.AppWindow, 24)
            {
                Foreground = _mutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.5,
                Margin = new Thickness(0, 0, 0, 8)
            });
            emptyHost.Children.Add(new TextBlock
            {
                Text = UiText.ProfileNoModules,
                FontSize = 12,
                Foreground = _mutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            ActiveWidgetsHost.Children.Add(emptyHost);
            return;
        }

        var isLight = _session.IsLightTheme();
        var unselectedBg = _previewBrush;
        var hoverBg = isLight ? Freeze(0x0C000000) : Freeze(0x14FFFFFF);

        var prevOffset = ActiveWidgetsScroll?.VerticalOffset ?? 0;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var itemId = item.Id;

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Col 0: Icon tile
            var iconTile = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = isLight ? Freeze(0x10000000) : Freeze(0x1CFFFFFF),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Child = new PhosphorGlyph(BuiltInWidgets.Glyph(item.WidgetId!), 13)
                {
                    Foreground = _accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconTile, 0);
            grid.Children.Add(iconTile);

            // Col 1: Text info (flex, single line ellipsis)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var titleBlock = new TextBlock
            {
                Text = BuiltInWidgets.DisplayName(item.WidgetId!),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _textBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            textStack.Children.Add(titleBlock);

            var summary = GetWidgetSummary(item);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = summary,
                    FontSize = 10,
                    Foreground = _mutedBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    Margin = new Thickness(0, 1, 0, 0)
                });
            }
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Col 2: Action button (Only delete button; Up/Down removed in favor of drag & drop)
            var removeBtn = CreateGhostActionButton(PhosphorIcon.Trash, UiText.Delete, isDanger: true, isEnabled: true, () =>
            {
                _session.RemoveDockItem(itemId);
                Refresh();
            });
            removeBtn.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;

            Grid.SetColumn(removeBtn, 2);
            grid.Children.Add(removeBtn);

            // Border wrapper with live drag & drop capability
            var border = new Border
            {
                Tag = itemId,
                Background = unselectedBg,
                BorderBrush = Freeze(DockTokens.Settings.Stroke(isLight)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 5, 5, 5),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand,
                ToolTip = "按住拖拽可调整在 Dock 上的前后顺序",
                Child = grid
            };

            border.MouseEnter += (_, _) =>
            {
                if (!_isActiveDragging)
                {
                    border.Background = hoverBg;
                }
            };
            border.MouseLeave += (_, _) =>
            {
                if (!_isActiveDragging)
                {
                    border.Background = unselectedBg;
                }
            };

            ActiveWidgetsHost.Children.Add(border);
        }

        if (prevOffset > 0)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActiveWidgetsScroll?.ScrollToVerticalOffset(prevOffset);
            }), DispatcherPriority.Loaded);
        }
    }

    private void OnActiveWidgetMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject origin && FindAncestor<Button>(origin) is not null)
        {
            return;
        }

        var slot = FindActiveWidgetSlot(e.OriginalSource as DependencyObject);
        if (slot is null)
        {
            return;
        }

        _activeDragSlot = slot;
        _activeDragFrom = ActiveWidgetsHost.Children.IndexOf(slot);
        _activeDragLiveIndex = _activeDragFrom;
        _activeDragOrigin = e.GetPosition(ActiveWidgetsHost);
        _activeGrabOffset = e.GetPosition(slot);
        _activeDragArmed = _activeDragFrom >= 0;
        _isActiveDragging = false;
    }

    private void OnActiveWidgetMouseMove(object sender, MouseEventArgs e)
    {
        if (_activeDragArmed && e.LeftButton == MouseButtonState.Pressed && _activeDragSlot is not null)
        {
            var pos = e.GetPosition(ActiveWidgetsHost);
            if (!_isActiveDragging)
            {
                var deltaX = pos.X - _activeDragOrigin.X;
                var deltaY = pos.Y - _activeDragOrigin.Y;
                if (!DockDragConstraints.IsDragStart(deltaX, deltaY))
                {
                    return;
                }

                BeginActiveWidgetDrag();
            }

            // Auto-scroll ActiveWidgetsScroll when dragging near top/bottom edges
            var scrollPos = e.GetPosition(ActiveWidgetsScroll);
            if (scrollPos.Y < 24)
            {
                ActiveWidgetsScroll.ScrollToVerticalOffset(ActiveWidgetsScroll.VerticalOffset - 6);
            }
            else if (scrollPos.Y > ActiveWidgetsScroll.ActualHeight - 24)
            {
                ActiveWidgetsScroll.ScrollToVerticalOffset(ActiveWidgetsScroll.VerticalOffset + 6);
            }

            PreviewLiveActiveWidgetOrder(pos);
            LiftActiveWidgetDragSlot(pos);
            e.Handled = true;
        }
    }

    private void BeginActiveWidgetDrag()
    {
        if (_isActiveDragging || _activeDragSlot is null)
        {
            return;
        }

        _isActiveDragging = true;
        _activeDragLift = new TranslateTransform();
        _activeDragSlot.RenderTransform = _activeDragLift;
        _activeDragSlot.Effect = new DropShadowEffect
        {
            BlurRadius = 20,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = Colors.Black
        };
        _activeDragSlot.Opacity = 0.96;
        _activeDragSlot.IsHitTestVisible = false;
        Panel.SetZIndex(_activeDragSlot, 1000);
        Mouse.OverrideCursor = Cursors.SizeNS;
        ActiveWidgetsScroll.CaptureMouse();
        LiftActiveWidgetDragSlot(Mouse.GetPosition(ActiveWidgetsHost));
    }

    private void LiftActiveWidgetDragSlot(Point mouseInHost)
    {
        if (_activeDragSlot is null || _activeDragLift is null)
        {
            return;
        }

        var origin = SlotLayoutOrigin(_activeDragSlot);
        var desiredY = mouseInHost.Y - _activeGrabOffset.Y;
        var minY = 0.0;
        var maxY = Math.Max(0, ActiveWidgetsHost.ActualHeight - _activeDragSlot.ActualHeight);
        var y = Math.Clamp(desiredY, minY, maxY);

        _activeDragLift.X = 0;
        _activeDragLift.Y = y - origin.Y;
    }

    private Point SlotLayoutOrigin(FrameworkElement slot)
    {
        var visual = slot.TranslatePoint(new Point(0, 0), ActiveWidgetsHost);
        if (_activeDragLift is null)
        {
            return visual;
        }

        return new Point(visual.X - _activeDragLift.X, visual.Y - _activeDragLift.Y);
    }

    private void PreviewLiveActiveWidgetOrder(Point positionInHost)
    {
        if (_activeDragSlot is null || _activeDragLiveIndex < 0)
        {
            return;
        }

        var insert = HitInsertIndex(positionInHost);
        var to = insert > _activeDragLiveIndex ? insert - 1 : insert;
        to = Math.Clamp(to, 0, Math.Max(0, ActiveWidgetsHost.Children.Count - 1));
        if (to == _activeDragLiveIndex)
        {
            return;
        }

        SlideActiveWidgetSlot(_activeDragSlot, to);
        _activeDragLiveIndex = to;
    }

    private int HitInsertIndex(Point pos)
    {
        var axis = pos.Y;
        var acc = 0.0;
        for (var i = 0; i < ActiveWidgetsHost.Children.Count; i++)
        {
            if (ActiveWidgetsHost.Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var leading = child.Margin.Top;
            var size = child.ActualHeight;
            var trailing = child.Margin.Bottom;
            if (axis < acc + leading + (size / 2))
            {
                return i;
            }

            acc += leading + size + trailing;
        }

        return ActiveWidgetsHost.Children.Count;
    }

    private void SlideActiveWidgetSlot(FrameworkElement slot, int to)
    {
        var from = ActiveWidgetsHost.Children.IndexOf(slot);
        if (from < 0 || from == to)
        {
            return;
        }

        var visuals = new Dictionary<FrameworkElement, Point>();
        foreach (UIElement child in ActiveWidgetsHost.Children)
        {
            if (child is FrameworkElement element && !ReferenceEquals(element, slot))
            {
                visuals[element] = element.TranslatePoint(new Point(0, 0), ActiveWidgetsHost);
            }
        }

        ActiveWidgetsHost.Children.Remove(slot);
        ActiveWidgetsHost.Children.Insert(to, slot);

        foreach (var element in visuals.Keys)
        {
            StopActiveWidgetShift(element);
        }

        ActiveWidgetsHost.UpdateLayout();

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        foreach (var (element, origin) in visuals)
        {
            var now = element.TranslatePoint(new Point(0, 0), ActiveWidgetsHost);
            var dy = origin.Y - now.Y;
            if (Math.Abs(dy) < 0.5)
            {
                continue;
            }

            var shift = new TranslateTransform(0, dy);
            element.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, duration) { EasingFunction = ease });
        }
    }

    private void ClearActiveWidgetShifts()
    {
        foreach (UIElement child in ActiveWidgetsHost.Children)
        {
            if (child is FrameworkElement element)
            {
                StopActiveWidgetShift(element);
            }
        }
    }

    private static void StopActiveWidgetShift(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
        }

        element.RenderTransform = Transform.Identity;
    }

    private void OnActiveWidgetMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_activeDragArmed)
        {
            return;
        }

        EndActiveWidgetDrag(commit: true);
        e.Handled = true;
    }

    private void EndActiveWidgetDrag(bool commit)
    {
        if (!_activeDragArmed)
        {
            return;
        }

        if (!_isActiveDragging)
        {
            DisarmActiveWidgetDrag();
            return;
        }

        var from = _activeDragFrom;
        var to = _activeDragLiveIndex;
        var dragged = _isActiveDragging;
        _activeDragArmed = false;
        _isActiveDragging = false;
        _activeDragFrom = -1;
        _activeDragLiveIndex = -1;
        Mouse.OverrideCursor = null;
        _activeDragLift = null;
        ClearActiveWidgetShifts();

        if (_activeDragSlot is not null)
        {
            _activeDragSlot.Effect = null;
            _activeDragSlot.Opacity = 1;
            _activeDragSlot.IsHitTestVisible = true;
            Panel.SetZIndex(_activeDragSlot, 0);
        }

        var slot = _activeDragSlot;
        _activeDragSlot = null;
        if (ActiveWidgetsScroll.IsMouseCaptured)
        {
            ActiveWidgetsScroll.ReleaseMouseCapture();
        }

        if (!commit || !dragged || slot is null || from < 0)
        {
            return;
        }

        to = Math.Clamp(to, 0, Math.Max(0, ActiveWidgetsHost.Children.Count - 1));
        if (to == from)
        {
            return;
        }

        var orderedIds = ActiveWidgetsHost.Children
            .OfType<Border>()
            .Select(b => b.Tag as string)
            .Where(id => !string.IsNullOrEmpty(id))
            .Cast<string>()
            .ToList();

        CommitReorderedWidgetIds(orderedIds);
    }

    private void DisarmActiveWidgetDrag()
    {
        _activeDragArmed = false;
        _isActiveDragging = false;
        _activeDragFrom = -1;
        _activeDragLiveIndex = -1;
        _activeDragSlot = null;
        _activeDragLift = null;
    }

    private void OnActiveWidgetLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isActiveDragging)
        {
            EndActiveWidgetDrag(commit: false);
            RebuildActiveWidgets();
        }
        else
        {
            DisarmActiveWidgetDrag();
        }
    }

    private void OnWidgetPickerPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isActiveDragging)
        {
            EndActiveWidgetDrag(commit: false);
            RebuildActiveWidgets();
            e.Handled = true;
        }
    }

    private void CommitReorderedWidgetIds(List<string> orderedIds)
    {
        if (_session?.CurrentDiff is null) return;
        var currentAll = _session.CurrentDiff.Dock.Items.ToList();
        var itemMap = currentAll.ToDictionary(i => i.Id);

        int widgetIdx = 0;
        var newItems = new List<DockItem>();
        foreach (var item in currentAll)
        {
            if (!string.IsNullOrWhiteSpace(item.WidgetId) && widgetIdx < orderedIds.Count)
            {
                var nextId = orderedIds[widgetIdx++];
                if (itemMap.TryGetValue(nextId, out var nextItem))
                {
                    newItems.Add(nextItem);
                }
                else
                {
                    newItems.Add(item);
                }
            }
            else
            {
                newItems.Add(item);
            }
        }

        _session.ReplaceDockItems(newItems);
        Refresh();
        RebuildActiveWidgets();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            if (current is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            else
            {
                current = LogicalTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private Border? FindActiveWidgetSlot(DependencyObject? origin)
    {
        var current = origin;
        while (current is not null && !ReferenceEquals(current, ActiveWidgetsHost))
        {
            if (current is Border border && ActiveWidgetsHost.Children.Contains(border))
            {
                return border;
            }
            if (current is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            else
            {
                current = LogicalTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private static string GetWidgetSummary(DockItem item) => item.WidgetId switch
    {
        BuiltInWidgets.Weather => string.IsNullOrWhiteSpace(item.Weather?.City) ? "自动定位" : item.Weather.City,
        BuiltInWidgets.WhiteNoise => $"音量 {(int)((item.WhiteNoise?.MasterVolume ?? 0.75) * 100)}%",
        BuiltInWidgets.Clock => string.IsNullOrWhiteSpace(item.TimeZoneId) ? "本地时间" : item.TimeZoneId,
        BuiltInWidgets.SysActivity => item.SysActivity?.Layout switch
        {
            SysActivityLayouts.Rings => "双圆环仪表",
            SysActivityLayouts.Bars => "条形负载",
            _ => "数字百分比"
        },
        _ => item.Density == WidgetDensity.Compact ? "紧凑药丸" : "展开卡片"
    };













    private UIElement EmptyState(string title, string hint)
    {
        var icon = new PhosphorGlyph(PhosphorIcon.MagnifyingGlass, 24)
        {
            Foreground = _mutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = _textBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var body = new TextBlock
        {
            Text = hint,
            FontSize = 12,
            Foreground = _mutedBrush,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 6, 0, 12)
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(24, 48, 24, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { icon, heading, body }
        };

        if (!string.IsNullOrWhiteSpace(_query) || !string.Equals(_categoryId, WidgetCatalog.AllId, StringComparison.Ordinal))
        {
            var clearBtn = new Button
            {
                Content = "重置筛选条件",
                FontSize = 12,
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            if (TryFindResource("Settings.Button") is Style btnStyle) clearBtn.Style = btnStyle;
            clearBtn.Click += (_, _) =>
            {
                _categoryId = WidgetCatalog.AllId;
                _query = string.Empty;
                SearchBox.Text = string.Empty;
                RebuildNav();
                RebuildCatalog();
            };
            stack.Children.Add(clearBtn);
        }

        return stack;
    }

    private UIElement CreateCard(string widgetId)
    {
        var alreadyOnDock = _session?.CurrentDiff is { Dock.Items: var items }
            && SettingsEditor.HasWidgetChip(items, widgetId);
        var isSingleton = BuiltInWidgets.IsSingleton(widgetId);
        var blocked = isSingleton && alreadyOnDock;
        var count = _session?.CurrentDiff?.Dock.Items.Count(item =>
                        item.Type == DockItemType.WidgetChip
                        && string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal)) ?? 0;

        var title = BuiltInWidgets.DisplayName(widgetId);
        var desc = BuiltInWidgets.Blurb(widgetId);
        var glyph = BuiltInWidgets.Glyph(widgetId);

        var isLight = _session?.IsLightTheme() ?? true;
        var iconTint = isLight ? Freeze(0x182563EB) : Freeze(0x333B82F6);

        // Header Row
        var headerDock = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };

        var iconBox = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(5),
            Background = iconTint,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new PhosphorGlyph(glyph, 13)
            {
                Foreground = _accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        if (count > 0)
        {
            var badgeText = isSingleton ? "已挂载" : $"已挂载 {count}";
            var badge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Background = isLight ? Freeze(0x1810B981) : Freeze(0x2E10B981),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = badgeText,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Freeze(0xFF10B981)
                }
            };
            DockPanel.SetDock(badge, Dock.Right);
            headerDock.Children.Add(badge);
        }

        DockPanel.SetDock(iconBox, Dock.Left);
        headerDock.Children.Add(iconBox);

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = _textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = title
        };
        headerDock.Children.Add(titleBlock);

        // Live Preview Box
        var preview = new Border
        {
            Height = 68,
            CornerRadius = new CornerRadius(6),
            Background = _previewBrush,
            Padding = new Thickness(8, 4, 8, 4),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = desc,
            Child = CreatePreview(widgetId)
        };

        // Description
        var descBlock = new TextBlock
        {
            Text = desc,
            FontSize = 11,
            Foreground = _mutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Height = 28,
            LineHeight = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = desc,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Button
        var addBtn = new Button
        {
            Height = 28,
            FontSize = 12,
            IsEnabled = !blocked,
            Cursor = blocked ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        if (blocked)
        {
            addBtn.Content = "✓ 已在 Dock 上";
            if (TryFindResource("Settings.Button") is Style btnStyle) addBtn.Style = btnStyle;
            addBtn.Opacity = 0.65;
        }
        else
        {
            addBtn.Content = count > 0 ? "+ 再次添加到 Dock" : "+ 添加到 Dock";
            if (TryFindResource("Settings.PrimaryButton") is Style priStyle) addBtn.Style = priStyle;
        }

        addBtn.Click += (_, _) =>
        {
            _session?.AddWidget(widgetId);
            Refresh();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActiveWidgetsScroll?.ScrollToBottom();
            }), DispatcherPriority.Loaded);
        };

        var cardBody = new StackPanel();
        cardBody.Children.Add(headerDock);
        cardBody.Children.Add(preview);
        cardBody.Children.Add(descBlock);
        cardBody.Children.Add(addBtn);

        var cardBorder = new Border
        {
            Margin = new Thickness(4, 4, 4, 8),
            Background = Freeze(DockTokens.Settings.Card(isLight)),
            BorderBrush = Freeze(DockTokens.Settings.Stroke(isLight)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = cardBody
        };

        cardBorder.MouseEnter += (_, _) =>
        {
            cardBorder.BorderBrush = _accent;
        };
        cardBorder.MouseLeave += (_, _) =>
        {
            cardBorder.BorderBrush = Freeze(DockTokens.Settings.Stroke(_session?.IsLightTheme() ?? true));
        };

        return cardBorder;
    }

    private UIElement CreatePreview(string widgetId)
    {
        var hub = _session!.Hub;
        return widgetId switch
        {
            BuiltInWidgets.Clock => ClockPreview(hub.Clock.LocalNow),
            BuiltInWidgets.Notes => NotesPreview(hub.Notes.Load("preview")),
            BuiltInWidgets.Power => PowerPreview(hub.Power.Current),
            BuiltInWidgets.SysActivity => SysActivityPreview(hub.Perf.Current),
            BuiltInWidgets.Network => NetworkPreview(hub.Network.Current),
            BuiltInWidgets.Disk => DiskPreview(hub.Disk.Current),
            BuiltInWidgets.Gpu => GpuPreview(hub.Gpu.Current),
            BuiltInWidgets.Processes => ProcessPreview(hub.Processes.Current),
            BuiltInWidgets.NowPlaying => MediaPreview(hub.Media.Current),
            BuiltInWidgets.Wallpaper => WallpaperPreview(hub.Wallpaper.Current),
            BuiltInWidgets.BingWallpaper => BingPreview(hub.Bing.Current),
            BuiltInWidgets.Stopwatch => StopwatchPreview(),
            BuiltInWidgets.Countdown => CountdownPreview(),
            BuiltInWidgets.Alarm => AlarmPreview(),
            BuiltInWidgets.FocusTimer => FocusTimerPreview(),
            BuiltInWidgets.TimeProgress => TimeProgressPreview(hub.Clock.LocalNow),
            BuiltInWidgets.Weather => WeatherPreview(null),
            BuiltInWidgets.Schedule => SchedulePreview(),
            BuiltInWidgets.Stocks => StocksPreview(),
            BuiltInWidgets.WhiteNoise => WhiteNoisePreview(),
            _ => Label(BuiltInWidgets.DisplayName(widgetId), 14, FontWeights.SemiBold)
        };
    }

    private UIElement WhiteNoisePreview()
    {
        var glyph = new PhosphorGlyph(PhosphorIcon.Headphones, 20)
        {
            Foreground = _accent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var title = Label("自然雨声 · 白噪音", 13, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var sub = Label("专注氛围 · 音量 75%", 11, FontWeights.Normal);
        sub.Foreground = _mutedBrush;
        sub.TextTrimming = TextTrimming.CharacterEllipsis;
        return PreviewRow(glyph, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, sub } });
    }

    private UIElement ClockPreview(DateTime now)
    {
        var time = Label(ClockFormat.Time(now), 20, FontWeights.SemiBold);
        time.TextTrimming = TextTrimming.CharacterEllipsis;
        var date = Label(ClockFormat.ExpandedDate(now), 11, FontWeights.Normal);
        date.Foreground = _mutedBrush;
        date.TextTrimming = TextTrimming.CharacterEllipsis;
        var face = new AnalogClockFace(40)
        {
            Time = now,
            Stroke = _textBrush,
            Margin = new Thickness(0, 0, 12, 0)
        };
        _clockTicks.Add(next =>
        {
            time.Text = ClockFormat.Time(next);
            date.Text = ClockFormat.ExpandedDate(next);
            face.Time = next;
        });
        return PreviewRow(face, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, date } });
    }

    private UIElement FocusTimerPreview()
    {
        var ring = new ProgressRing(40)
        {
            Progress = 0.86,
            Track = Freeze(DockTokens.QuietFill),
            Fill = Freeze(FocusTimerColors.Fill(FocusTimerColors.Default)),
            Margin = new Thickness(0, 0, 12, 0)
        };
        var time = Label(FocusTimerFormat.Time(TimeSpan.FromMinutes(25)), 20, FontWeights.SemiBold);
        var status = Label(FocusTimerFormat.Phase(FocusTimerPhase.Focus), 11, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return PreviewRow(ring, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, status } });
    }

    private UIElement TimeProgressPreview(DateTime now)
    {
        var percent = Label(TimeProgressFormat.PercentLabel(now, TimeProgressPeriods.Year), 18, FontWeights.SemiBold);
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Label(TimeProgressFormat.Caption(now, TimeProgressPeriods.Year), 11, FontWeights.Normal);
        caption.Foreground = _mutedBrush;
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        var bars = new TimeProgressBars
        {
            Width = DockTokens.Settings.CatalogCardWidth - (2 * DockTokens.Settings.CatalogPreviewPad),
            Height = 12,
            BarCount = 28,
            Progress = TimeProgressFormat.Fraction(now, TimeProgressPeriods.Year),
            Fill = Freeze(_session?.IsLightTheme() == false ? DockTokens.Dark.Text : DockTokens.Light.Text),
            Track = Freeze(DockTokens.QuietFill),
            Margin = new Thickness(0, 6, 0, 0)
        };
        _clockTicks.Add(next =>
        {
            percent.Text = TimeProgressFormat.PercentLabel(next, TimeProgressPeriods.Year);
            caption.Text = TimeProgressFormat.Caption(next, TimeProgressPeriods.Year);
            bars.Progress = TimeProgressFormat.Fraction(next, TimeProgressPeriods.Year);
        });
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { percent, caption, bars }
        };
    }

    private UIElement StopwatchPreview()
    {
        var time = Label(StopwatchFormat.Time(TimeSpan.Zero), 22, FontWeights.SemiBold);
        var status = Label(StopwatchFormat.Ready, 12, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { time, status }
        };
    }

    private UIElement CountdownPreview()
    {
        var glyph = new PhosphorGlyph(PhosphorIcon.Timer, 22)
        {
            Foreground = _textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        var time = Label(CountdownFormat.Time(TimeSpan.FromMinutes(5)), 20, FontWeights.SemiBold);
        var status = Label(CountdownFormat.Ready, 11, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return PreviewRow(glyph, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, status } });
    }

    private UIElement AlarmPreview()
    {
        var glyph = new PhosphorGlyph(PhosphorIcon.Alarm, 22)
        {
            Foreground = _textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var title = Label(AlarmFormat.IdleTitle, 18, FontWeights.SemiBold);
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title } };
        return PreviewRow(copy, glyph);
    }

    private UIElement SysActivityPreview(PerfSnapshot snapshot)
    {
        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cpu = PreviewMeter(SysActivityFormat.Percent(snapshot.CpuPercent, snapshot.CpuAvailable), "CPU", DockTokens.Cpu);
        var mem = PreviewMeter(SysActivityFormat.Percent(snapshot.MemoryPercent, snapshot.MemoryAvailable), UiText.Memory, DockTokens.Memory);
        row.Children.Add(cpu);
        Grid.SetColumn(mem, 1);
        row.Children.Add(mem);
        return row;
    }

    private UIElement PreviewMeter(string percent, string caption, uint tint)
    {
        var value = Label(percent, 18, FontWeights.SemiBold);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = Freeze(tint),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var name = Label(caption, 11, FontWeights.Normal);
        name.Foreground = _mutedBrush;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { dot, name }
        };
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { value, row }
        };
    }

    private UIElement NotesPreview(string text)
    {
        var preview = Label(NotesText.DockPreview(text), 13, FontWeights.SemiBold);
        preview.Foreground = Freeze(NotePapers.Ink);
        preview.TextWrapping = TextWrapping.Wrap;
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        return new Border
        {
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Background = Freeze(NotePapers.Fill(NotePapers.Default)),
            Padding = new Thickness(12, 10, 12, 10),
            Child = preview
        };
    }

    private UIElement DiskPreview(DiskSnapshot snapshot)
    {
        var visible = DiskWidgetSettings.Visible(snapshot.Volumes, DiskWidgetSettings.Default);
        var value = Label(DiskFormat.DockValue(visible, DiskWidgetSettings.Default), 20, FontWeights.SemiBold);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Label(UiText.DiskUsedPercent, 11, FontWeights.Normal);
        caption.Foreground = _mutedBrush;
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { value, caption }
        };
    }

    private UIElement GpuPreview(GpuSnapshot snapshot)
    {
        var value = Label(GpuFormat.Percent(snapshot.Percent, snapshot.PercentAvailable && snapshot.Supported), 20, FontWeights.SemiBold);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Label("GPU", 11, FontWeights.Normal);
        caption.Foreground = _mutedBrush;
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { value, caption }
        };
    }

    private UIElement ProcessPreview(ProcessListSnapshot snapshot)
    {
        if (!snapshot.Allowed)
        {
            var off = Label(UiText.ProcessNamesOff, 14, FontWeights.SemiBold);
            off.HorizontalAlignment = HorizontalAlignment.Center;
            return off;
        }

        var top = ProcessFormat.Rank(snapshot.Rows, ProcessSorts.Cpu).FirstOrDefault();
        var name = Label(top?.Name ?? "—", 16, FontWeights.SemiBold);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var metric = Label(top is null ? "CPU" : ProcessFormat.Cpu(top.CpuPercent), 11, FontWeights.Normal);
        metric.Foreground = _mutedBrush;
        metric.HorizontalAlignment = HorizontalAlignment.Center;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { name, metric }
        };
    }

    private UIElement NetworkPreview(NetworkSnapshot snapshot)
    {
        var downArrow = Label("↓", 13, FontWeights.SemiBold);
        downArrow.Foreground = Freeze(DockTokens.Network);
        downArrow.Margin = new Thickness(0, 0, 6, 0);
        var upArrow = Label("↑", 13, FontWeights.SemiBold);
        upArrow.Foreground = Freeze(DockTokens.NetworkUp);
        upArrow.Margin = new Thickness(0, 0, 6, 0);
        var down = Label(snapshot.IsConnected ? ByteRate.Format(snapshot.ReceiveBytesPerSecond) : UiText.Disconnected, 15, FontWeights.SemiBold);
        down.TextTrimming = TextTrimming.CharacterEllipsis;
        var up = Label(snapshot.IsConnected ? ByteRate.Format(snapshot.SendBytesPerSecond) : string.Empty, 15, FontWeights.SemiBold);
        up.TextTrimming = TextTrimming.CharacterEllipsis;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { downArrow, down } },
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0), Children = { upArrow, up } }
            }
        };
    }

    private UIElement PowerPreview(PowerSnapshot snapshot)
    {
        var devices = snapshot.Devices.Count > 0 ? snapshot.Devices : PreviewDevices(snapshot);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var device in devices.Take(2))
        {
            row.Children.Add(PowerPreviewCell(device));
        }

        return row;
    }

    private static IReadOnlyList<PowerDeviceSnapshot> PreviewDevices(PowerSnapshot snapshot) =>
    [
        new()
        {
            Id = "preview-laptop",
            Name = UiText.Laptop,
            Kind = PowerDeviceKind.Laptop,
            Percent = snapshot.PercentRemaining ?? 76
        },
        new()
        {
            Id = "preview-audio",
            Name = UiText.Headphones,
            Kind = PowerDeviceKind.Headphones,
            Percent = 85
        }
    ];

    private UIElement PowerPreviewCell(PowerDeviceSnapshot device)
    {
        var ring = new ProgressRing(28)
        {
            Clockwise = true,
            Progress = (device.Percent ?? 0) / 100.0,
            Fill = Freeze(device.Percent is <= 15 ? DockTokens.BatteryCritical : DockTokens.BatteryOk),
            Track = Freeze(DockTokens.QuietFill)
        };
        var icon = new PhosphorGlyph(device.Kind switch
        {
            PowerDeviceKind.Laptop => PhosphorIcon.Laptop,
            PowerDeviceKind.Headphones => PhosphorIcon.Headphones,
            PowerDeviceKind.Case => PhosphorIcon.ComputerTower,
            PowerDeviceKind.Keyboard => PhosphorIcon.Keyboard,
            _ => PhosphorIcon.ComputerTower
        }, 11)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var face = new Grid
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { ring, icon }
        };
        var percent = Label(PowerFormat.Percent(device.Percent), 10, FontWeights.SemiBold);
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        percent.Margin = new Thickness(0, 2, 0, 0);
        return new StackPanel
        {
            Width = 40,
            Margin = new Thickness(4, 0, 4, 0),
            Children = { face, percent }
        };
    }

    private UIElement MediaPreview(MediaSessionSnapshot? session)
    {
        var art = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = Freeze(DockTokens.QuietFill),
            Child = new PhosphorGlyph(PhosphorIcon.Headphones, 16)
            {
                Foreground = _accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var title = Label(string.IsNullOrWhiteSpace(session?.Title) ? UiText.NotPlaying : session!.Title, 14, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        var artist = Label(session?.Artist ?? string.Empty, 11, FontWeights.Normal);
        artist.Foreground = _mutedBrush;
        artist.TextTrimming = TextTrimming.CharacterEllipsis;
        artist.TextWrapping = TextWrapping.NoWrap;
        return PreviewRow(art, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, artist } });
    }

    private UIElement BingPreview(BingWallpaperSnapshot snapshot)
    {
        var thumb = new Border
        {
            Width = 52,
            Height = 36,
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = Freeze(DockTokens.Bing),
            Margin = new Thickness(0, 0, 12, 0)
        };
        if (!string.IsNullOrWhiteSpace(snapshot.LocalPath) && File.Exists(snapshot.LocalPath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 52;
            bitmap.UriSource = new Uri(snapshot.LocalPath);
            bitmap.EndInit();
            bitmap.Freeze();
            thumb.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }

        var title = Label(string.IsNullOrWhiteSpace(snapshot.Selected?.Title) ? UiText.WidgetBing : snapshot.Selected!.Title, 13, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        return PreviewRow(thumb, title);
    }

    private UIElement WallpaperPreview(WallpaperSnapshot snapshot)
    {
        var thumb = new Border
        {
            Width = 52,
            Height = 36,
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = Freeze(DockTokens.Wallpaper),
            Margin = new Thickness(0, 0, 12, 0)
        };
        var path = snapshot.Current?.LocalPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 52;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            thumb.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }

        var title = Label(string.IsNullOrWhiteSpace(snapshot.Current?.Title) ? UiText.WidgetWallpaper : snapshot.Current!.Title, 13, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;
        return PreviewRow(thumb, title);
    }

    private static Grid PreviewRow(UIElement left, UIElement right)
    {
        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(left);
        Grid.SetColumn(right, 1);
        row.Children.Add(right);
        return row;
    }

    private TextBlock Label(string text, double size, FontWeight weight) => new()
    {
        Text = text,
        FontFamily = WinDock.Widgets.UiFont.Family,
        FontSize = size,
        FontWeight = weight,
        Foreground = _textBrush,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void SyncClockTimer()
    {
        _clockTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick -= OnClockTick;
        var run = _catalogActive && IsVisible && IsLoaded && _clockTicks.Count > 0;
        if (!run)
        {
            _clockTimer.Stop();
            return;
        }

        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private void OnClockTick(object? sender, EventArgs e) => PulseClocks();

    private void PulseClocks()
    {
        if (_session is null || _clockTicks.Count == 0 || !_catalogActive || !IsVisible)
        {
            return;
        }

        var now = _session.Hub.Clock.LocalNow;
        foreach (var tick in _clockTicks)
        {
            tick(now);
        }
    }

    private UIElement WeatherPreview(WeatherSnapshot? _)
    {
        var tempStr = "--°";
        var text = UiText.WeatherNeedCity;
        var glyph = "\u2600";

        var glyphBlock = Label(glyph, 20, FontWeights.Normal);
        glyphBlock.FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe MDL2 Assets");
        glyphBlock.Margin = new Thickness(0, 0, 10, 0);
        glyphBlock.VerticalAlignment = VerticalAlignment.Center;

        var temp = Label(tempStr, 18, FontWeights.SemiBold);
        var sub = Label(text, 11, FontWeights.Normal);
        sub.Foreground = _mutedBrush;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                glyphBlock,
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { temp, sub } }
            }
        };
    }

    private UIElement StocksPreview()
    {
        var name = Label("长鑫科技", 13, FontWeights.SemiBold);
        var meta = Label("A   688825   科创", 10, FontWeights.Normal);
        meta.Foreground = _mutedBrush;
        meta.Margin = new Thickness(0, 2, 0, 0);
        var price = Label("54.79", 14, FontWeights.SemiBold);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        var change = Label("-1.37%", 11, FontWeights.SemiBold);
        change.Foreground = SettingsChrome.Brush(DockTokens.StockDown);
        change.HorizontalAlignment = HorizontalAlignment.Right;
        change.Margin = new Thickness(0, 2, 0, 0);
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, meta } };
        var right = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 0, 0),
            Children = { price, change }
        };
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private UIElement SchedulePreview()
    {
        var glyphBlock = new PhosphorGlyph(PhosphorIcon.Calendar, 20)
        {
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _textBrush
        };

        var title = Label("14:30 方案评审", 15, FontWeights.SemiBold);
        var sub = Label("28m 后 · 今日还有 3 项日程", 11, FontWeights.Normal);
        sub.Foreground = _mutedBrush;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                glyphBlock,
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, sub } }
            }
        };
    }

    private static SolidColorBrush Freeze(uint argb)
    {
        var (a, r, g, b) = DockTokens.Split(argb);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
