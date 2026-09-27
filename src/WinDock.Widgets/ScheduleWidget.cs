using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class ScheduleWidget : UserControl, IWidgetOpenable, IScheduleConfigurable, IWidgetThemeAware, ILocalizable, IWidgetDetachable
{
    private readonly ScheduleEngine _engine = new();
    private bool _isLight;
    private ScheduleColorId _selectedAddColor = ScheduleColorId.Orange;
    private bool _isAllDayExpanded = true;
    private int _selectedTab; // 0 = Events, 1 = Countdowns

    // Dock Island Tile elements
    private readonly Border _tile;
    private readonly Border _dateBadge = new();
    private readonly TextBlock _badgeWeekday = new();
    private readonly TextBlock _badgeDay = new();
    private readonly StackPanel _agendaHost = new();

    // Flyout & Popover
    private readonly Popup _popup;
    private readonly Border _panel;
    private readonly StackPanel _agendaView = new();
    private readonly StackPanel _categoriesView = new();

    // Agenda View Elements
    private readonly TextBlock _flyoutTitle = HeaderLabel(UiText.ScheduleTodayHeader);
    private readonly Border _btnCalendars = new();
    private readonly TextBlock _btnCalendarsText = new();
    private readonly Border _tabEvents;
    private readonly Border _tabCountdowns;
    private readonly TextBlock _tabEventsLabel = new();
    private readonly TextBlock _tabCountdownsLabel = new();
    private readonly StackPanel _eventsContainer = new();
    private readonly StackPanel _eventsList = new();
    private readonly StackPanel _allDayContainer = new();
    private readonly StackPanel _allDayList = new();
    private readonly TextBlock _allDayChevron = new();
    private readonly TextBlock _allDayHeaderLabel = new();
    private readonly StackPanel _countdownsList = new();
    private readonly ScrollViewer _scrollList = new()
    {
        MaxHeight = 240,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Margin = new Thickness(0, 4, 0, 6)
    };

    // Quick Add Bar Elements
    private readonly Border _addColorBtn = new();
    private readonly Ellipse _addColorDot = new();
    private readonly TextBox _addInput = new()
    {
        FontSize = 12,
        Padding = new Thickness(6, 4, 6, 4),
        Margin = new Thickness(0, 0, 6, 0)
    };
    private readonly DatePicker _datePicker = new()
    {
        SelectedDate = DateTime.Today.AddDays(7),
        Margin = new Thickness(0, 0, 6, 0),
        Visibility = Visibility.Collapsed
    };

    // Category Settings Elements
    private readonly TextBox[] _categoryInputs = new TextBox[ScheduleColors.Count];
    private readonly TextBlock _categoriesHelp = new();

    private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    public event EventHandler<ScheduleSettings>? SettingsChanged;

    public ScheduleWidget()
    {
        // Date Badge setup (Image 1, 3)
        _dateBadge.CornerRadius = new CornerRadius(5);
        _dateBadge.Width = 36;
        _dateBadge.Height = 36;
        _dateBadge.Margin = new Thickness(0, 0, 8, 0);
        _dateBadge.VerticalAlignment = VerticalAlignment.Center;
        _dateBadge.Background = TokenBrushes.From(0x1C000000u);

        _badgeWeekday.FontSize = 9.5;
        _badgeWeekday.FontWeight = FontWeights.Bold;
        _badgeWeekday.Foreground = TokenBrushes.From(0xFFFF453A); // Red / Coral accent
        _badgeWeekday.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeWeekday.Margin = new Thickness(0, 2, 0, 0);

        _badgeDay.FontSize = 15;
        _badgeDay.FontWeight = FontWeights.Bold;
        _badgeDay.Foreground = Brushes.White;
        _badgeDay.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeDay.Margin = new Thickness(0, -1, 0, 2);

        var badgeStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _badgeWeekday, _badgeDay }
        };
        _dateBadge.Child = badgeStack;

        _agendaHost.VerticalAlignment = VerticalAlignment.Center;

        var tileRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _dateBadge, _agendaHost }
        };

        _tile = WidgetChrome.Wrap(tileRow, minWidth: 120);
        _tile.MaxWidth = 260;

        _tile.MouseLeftButtonUp += (_, e) =>
        {
            if (_popup?.IsOpen == true)
            {
                ClosePopup();
            }
            else
            {
                Open();
            }
            e.Handled = true;
        };

        // Tabs
        _tabEventsLabel.Text = UiText.ScheduleTabEvents;
        _tabEventsLabel.FontSize = 11;
        _tabEventsLabel.FontWeight = FontWeights.SemiBold;
        _tabEvents = TabItem(_tabEventsLabel, () => SwitchTab(0));

        _tabCountdownsLabel.Text = UiText.ScheduleTabCountdowns;
        _tabCountdownsLabel.FontSize = 11;
        _tabCountdownsLabel.FontWeight = FontWeights.SemiBold;
        _tabCountdowns = TabItem(_tabCountdownsLabel, () => SwitchTab(1));

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8
        };
        WidgetFlyout.Attach(_popup);

        _panel = BuildPanel();
        _popup.Child = WidgetChrome.Shadow(_panel);

        _tickTimer.Tick += (_, _) => Dispatcher.Invoke(UpdateTile);
        _tickTimer.Start();

        Content = new Grid { Children = { _tile, _popup } };

        SwitchTab(0);
        UpdateTile();
    }

    public void Detach()
    {
        _tickTimer.Stop();
        _popup.IsOpen = false;
    }

    public void ApplySettings(ScheduleSettings settings)
    {
        _engine.ApplySettings(settings);
        UpdateTile();
        RenderList();
        UpdateAddColorButton();
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        _dateBadge.Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x24FFFFFFu);
        _badgeDay.Foreground = _isLight ? TokenBrushes.From(DockTokens.Light.Text) : Brushes.White;
        UpdateTile();
        RenderList();
        UpdateAddColorButton();
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.ScheduleTodayHeader;
        _btnCalendarsText.Text = UiText.ScheduleCalendars;
        _tabEventsLabel.Text = UiText.ScheduleTabEvents;
        _tabCountdownsLabel.Text = UiText.ScheduleTabCountdowns;
        _allDayHeaderLabel.Text = UiText.ScheduleAllDaySection;
        _categoriesHelp.Text = UiText.ScheduleColorDesc;
        _addInput.ToolTip = _selectedTab == 0 ? UiText.ScheduleAddEventPlaceholder : UiText.ScheduleAddCountdownPlaceholder;
        UpdateTile();
        RenderList();
        UpdateAddColorButton();
    }

    public void Open()
    {
        _popup.IsOpen = true;
        _categoriesView.Visibility = Visibility.Collapsed;
        _agendaView.Visibility = Visibility.Visible;
        RenderList();
    }

    private void ClosePopup() => _popup.IsOpen = false;

    private Border BuildPanel()
    {
        // 1. Build Agenda View
        BuildAgendaView();

        // 2. Build Categories Management View
        BuildCategoriesView();

        var container = new Grid
        {
            Width = 320,
            Children = { _agendaView, _categoriesView }
        };

        var border = new Border
        {
            Tag = WidgetChrome.FlyoutTag,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 14, 16, 16),
            SnapsToDevicePixels = true,
            Child = container
        };
        WidgetChrome.Round(border, DockTokens.IslandCornerRadius);
        return border;
    }

    private void BuildAgendaView()
    {
        // Header (Image 2): "Today" on left, "Calendars..." capsule button and close button on right
        var close = HeaderGlyph("\u00D7", ClosePopup, 16);

        _btnCalendarsText.Text = UiText.ScheduleCalendars;
        _btnCalendarsText.FontSize = 11;
        _btnCalendarsText.FontWeight = FontWeights.Medium;
        _btnCalendarsText.VerticalAlignment = VerticalAlignment.Center;

        _btnCalendars.CornerRadius = new CornerRadius(10);
        _btnCalendars.Padding = new Thickness(8, 2, 8, 2);
        _btnCalendars.Margin = new Thickness(0, 0, 6, 0);
        _btnCalendars.Background = TokenBrushes.From(0x18000000u);
        _btnCalendars.Cursor = Cursors.Hand;
        _btnCalendars.Child = _btnCalendarsText;
        _btnCalendars.MouseLeftButtonUp += (_, e) =>
        {
            OpenCategoriesView();
            e.Handled = true;
        };

        var header = HeaderRow(_flyoutTitle, _btnCalendars, close);

        // Tabs (Today's Schedule vs Countdowns)
        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 4),
            Children = { _tabEvents, _tabCountdowns }
        };

        // All-day collapsible section
        _allDayHeaderLabel.Text = UiText.ScheduleAllDaySection;
        _allDayHeaderLabel.FontSize = 11;
        _allDayHeaderLabel.FontWeight = FontWeights.SemiBold;
        _allDayHeaderLabel.Opacity = 0.75;
        _allDayHeaderLabel.VerticalAlignment = VerticalAlignment.Center;

        _allDayChevron.Text = "\uE70D"; // ChevronDown
        _allDayChevron.FontFamily = new FontFamily("Segoe MDL2 Assets");
        _allDayChevron.FontSize = 10;
        _allDayChevron.Opacity = 0.6;
        _allDayChevron.VerticalAlignment = VerticalAlignment.Center;

        var allDayHeaderRow = new DockPanel
        {
            Margin = new Thickness(0, 6, 0, 2),
            Cursor = Cursors.Hand,
            LastChildFill = false
        };
        allDayHeaderRow.Children.Add(_allDayHeaderLabel);
        DockPanel.SetDock(_allDayChevron, Dock.Right);
        allDayHeaderRow.Children.Add(_allDayChevron);

        allDayHeaderRow.MouseLeftButtonUp += (_, e) =>
        {
            _isAllDayExpanded = !_isAllDayExpanded;
            _allDayList.Visibility = _isAllDayExpanded ? Visibility.Visible : Visibility.Collapsed;
            _allDayChevron.Text = _isAllDayExpanded ? "\uE70E" : "\uE70D";
            e.Handled = true;
        };

        _allDayContainer.Children.Add(allDayHeaderRow);
        _allDayContainer.Children.Add(_allDayList);
        _allDayContainer.Visibility = Visibility.Collapsed;

        _eventsContainer.Children.Clear();
        _eventsContainer.Children.Add(_eventsList);
        _eventsContainer.Children.Add(_allDayContainer);

        // Quick add bar
        _addColorDot.Width = 10;
        _addColorDot.Height = 10;
        _addColorDot.VerticalAlignment = VerticalAlignment.Center;
        _addColorDot.HorizontalAlignment = HorizontalAlignment.Center;

        _addColorBtn.Width = 20;
        _addColorBtn.Height = 20;
        _addColorBtn.CornerRadius = new CornerRadius(10);
        _addColorBtn.Background = TokenBrushes.From(0x18000000u);
        _addColorBtn.Margin = new Thickness(0, 0, 6, 0);
        _addColorBtn.Cursor = Cursors.Hand;
        _addColorBtn.Child = _addColorDot;
        _addColorBtn.MouseLeftButtonUp += (_, e) =>
        {
            // Cycle color 0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0
            _selectedAddColor = (ScheduleColorId)(((int)_selectedAddColor + 1) % ScheduleColors.Count);
            UpdateAddColorButton();
            e.Handled = true;
        };
        UpdateAddColorButton();

        var addBtn = HeaderGlyph("\uE710", AddFromInput, 13);
        addBtn.ToolTip = UiText.ScheduleAdd;
        addBtn.Background = TokenBrushes.From(0x22000000u);

        _addInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AddFromInput();
                e.Handled = true;
            }
        };

        var addRow = new DockPanel
        {
            Margin = new Thickness(0, 6, 0, 0),
            LastChildFill = true
        };
        DockPanel.SetDock(addBtn, Dock.Right);
        addRow.Children.Add(addBtn);
        DockPanel.SetDock(_addColorBtn, Dock.Left);
        addRow.Children.Add(_addColorBtn);
        addRow.Children.Add(_datePicker);
        addRow.Children.Add(_addInput);

        _agendaView.Children.Add(header);
        _agendaView.Children.Add(tabs);
        _agendaView.Children.Add(_scrollList);
        _agendaView.Children.Add(addRow);

        _scrollList.Content = _eventsContainer;
    }

    private void BuildCategoriesView()
    {
        _categoriesView.Visibility = Visibility.Collapsed;

        var backBtn = HeaderLabel(UiText.ScheduleBack);
        backBtn.FontSize = 12;
        backBtn.Cursor = Cursors.Hand;
        backBtn.MouseLeftButtonUp += (_, e) =>
        {
            SaveCategoriesAndClose();
            e.Handled = true;
        };

        var catTitle = HeaderLabel(UiText.ScheduleCustomCategories);
        catTitle.FontSize = 13;

        var doneBtn = HeaderLabel(UiText.ScheduleDone);
        doneBtn.FontSize = 12;
        doneBtn.Cursor = Cursors.Hand;
        doneBtn.Foreground = TokenBrushes.From(0xFF0078D4);
        doneBtn.MouseLeftButtonUp += (_, e) =>
        {
            SaveCategoriesAndClose();
            e.Handled = true;
        };

        var catHeader = HeaderRow(backBtn, catTitle, doneBtn);

        _categoriesHelp.Text = UiText.ScheduleColorDesc;
        _categoriesHelp.FontSize = 11;
        _categoriesHelp.Opacity = 0.65;
        _categoriesHelp.TextWrapping = TextWrapping.Wrap;
        _categoriesHelp.Margin = new Thickness(0, 6, 0, 10);

        var listStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        for (var i = 0; i < ScheduleColors.Count; i++)
        {
            var colorId = (ScheduleColorId)i;
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = TokenBrushes.From(ScheduleColors.Argb(colorId, _isLight))
            };
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);

            var box = new TextBox
            {
                FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            _categoryInputs[i] = box;
            row.Children.Add(box);

            listStack.Children.Add(row);
        }

        var resetBtn = new Button
        {
            Content = UiText.ScheduleResetDefaults,
            FontSize = 11,
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 4, 0, 0)
        };
        resetBtn.Click += (_, _) =>
        {
            for (var i = 0; i < ScheduleColors.Count; i++)
            {
                _categoryInputs[i].Text = "";
            }
        };

        _categoriesView.Children.Add(catHeader);
        _categoriesView.Children.Add(_categoriesHelp);
        _categoriesView.Children.Add(listStack);
        _categoriesView.Children.Add(resetBtn);
    }

    private void OpenCategoriesView()
    {
        for (var i = 0; i < ScheduleColors.Count; i++)
        {
            var colorId = (ScheduleColorId)i;
            var cat = _engine.Settings.Categories.FirstOrDefault(c => c.ColorId == colorId);
            _categoryInputs[i].Text = cat?.Title ?? "";
        }

        _agendaView.Visibility = Visibility.Collapsed;
        _categoriesView.Visibility = Visibility.Visible;
    }

    private void SaveCategoriesAndClose()
    {
        var cats = new List<ScheduleCategory>();
        for (var i = 0; i < ScheduleColors.Count; i++)
        {
            cats.Add(new ScheduleCategory
            {
                ColorId = (ScheduleColorId)i,
                Title = _categoryInputs[i].Text.Trim()
            });
        }

        var next = ScheduleSettings.Normalize(_engine.Settings with { Categories = cats });
        _engine.ApplySettings(next);
        SettingsChanged?.Invoke(this, next);

        _categoriesView.Visibility = Visibility.Collapsed;
        _agendaView.Visibility = Visibility.Visible;

        UpdateTile();
        RenderList();
        UpdateAddColorButton();
    }

    private void UpdateAddColorButton()
    {
        _addColorDot.Fill = TokenBrushes.From(ScheduleColors.Argb(_selectedAddColor, _isLight));
        var title = _engine.Settings.GetCategoryTitle(_selectedAddColor);
        _addColorBtn.ToolTip = $"{title} ({UiText.ScheduleCalendars})";
    }

    private void SwitchTab(int index)
    {
        _selectedTab = index;
        _tabEvents.Opacity = index == 0 ? 1.0 : 0.55;
        _tabCountdowns.Opacity = index == 1 ? 1.0 : 0.55;

        _btnCalendars.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _addColorBtn.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _datePicker.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        _addInput.ToolTip = index == 0 ? UiText.ScheduleAddEventPlaceholder : UiText.ScheduleAddCountdownPlaceholder;

        _scrollList.Content = index == 0
            ? _eventsContainer
            : _countdownsList;

        RenderList();
    }

    private void UpdateDateBadge(DateTime now)
    {
        var isChinese = UiLocale.IsChinese;
        string weekday;
        if (isChinese)
        {
            weekday = now.DayOfWeek switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                DayOfWeek.Sunday => "周日",
                _ => "周日"
            };
        }
        else
        {
            weekday = now.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        _badgeWeekday.Text = weekday;
        _badgeDay.Text = now.Day.ToString();
    }

    private void UpdateTile()
    {
        var now = DateTime.Now;
        UpdateDateBadge(now);

        _agendaHost.Children.Clear();
        var upcoming = _engine.GetTodayUpcomingEvents(now, limit: 3);
        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

        if (upcoming.Count > 1)
        {
            // Multi-item mode (Image 1)
            foreach (var item in upcoming)
            {
                var grid = new Grid
                {
                    Height = 12,
                    Margin = new Thickness(0, 0.5, 0, 0.5)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var dot = new Ellipse
                {
                    Width = 4.5,
                    Height = 4.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                    Fill = TokenBrushes.From(ScheduleColors.Argb(item.ColorId, _isLight))
                };
                Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);

                var title = new TextBlock
                {
                    Text = item.Title,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = textBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(title, 1);
                grid.Children.Add(title);

                var timeStr = item.Time.HasValue ? item.Time.Value.ToString(@"hh\:mm") : UiText.ScheduleAllDay;
                var timeBlock = new TextBlock
                {
                    Text = timeStr,
                    FontSize = 9,
                    Opacity = 0.65,
                    Margin = new Thickness(4, 0, 0, 0),
                    Foreground = textBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(timeBlock, 2);
                grid.Children.Add(timeBlock);

                _agendaHost.Children.Add(grid);
            }
        }
        else if (upcoming.Count == 1)
        {
            // Single-item compact mode (Image 3)
            var item = upcoming[0];
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dot = new Ellipse
            {
                Width = 5.5,
                Height = 5.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Fill = TokenBrushes.From(ScheduleColors.Argb(item.ColorId, _isLight))
            };
            topRow.Children.Add(dot);
            topRow.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = textBrush
            });

            var timeStr = item.Time.HasValue ? item.Time.Value.ToString(@"hh\:mm") : UiText.ScheduleAllDay;
            var subBlock = new TextBlock
            {
                Text = timeStr,
                FontSize = 10,
                Opacity = 0.7,
                Margin = new Thickness(10, 1, 0, 0),
                Foreground = textBrush
            };

            _agendaHost.Children.Add(topRow);
            _agendaHost.Children.Add(subBlock);
        }
        else
        {
            // Empty state or nearest countdown
            var (title, sub, isCountdown) = _engine.GetDockSummary(now);
            var topBlock = new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = textBrush
            };
            var subBlock = new TextBlock
            {
                Text = sub,
                FontSize = 10,
                Opacity = 0.65,
                Foreground = textBrush,
                Margin = new Thickness(0, 1, 0, 0)
            };

            _agendaHost.Children.Add(topBlock);
            _agendaHost.Children.Add(subBlock);
        }
    }

    private void RenderList()
    {
        if (_selectedTab == 0)
        {
            RenderEvents();
        }
        else
        {
            RenderCountdowns();
        }
    }

    private void RenderEvents()
    {
        _eventsList.Children.Clear();
        _allDayList.Children.Clear();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdue = _engine.GetOverdueEvents(today);
        var (timed, allDay) = _engine.GetTodayEvents(today);

        if (timed.Count == 0 && allDay.Count == 0 && overdue.Count == 0)
        {
            _eventsList.Children.Add(new TextBlock
            {
                Text = UiText.ScheduleEmptyEvents,
                FontSize = 12,
                Opacity = 0.6,
                Margin = new Thickness(0, 16, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _allDayContainer.Visibility = Visibility.Collapsed;
            return;
        }

        if (overdue.Count > 0)
        {
            _eventsList.Children.Add(new TextBlock
            {
                Text = UiText.ScheduleOverdue,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.65,
                Margin = new Thickness(0, 4, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
            foreach (var item in overdue)
            {
                _eventsList.Children.Add(CreateEventRow(item, today));
            }
        }

        // Render timed events
        foreach (var item in timed)
        {
            _eventsList.Children.Add(CreateEventRow(item, today));
        }

        // Render all-day events
        if (allDay.Count > 0)
        {
            _allDayContainer.Visibility = Visibility.Visible;
            foreach (var item in allDay)
            {
                _allDayList.Children.Add(CreateEventRow(item, today));
            }
        }
        else
        {
            _allDayContainer.Visibility = Visibility.Collapsed;
        }
    }

    private UIElement CreateEventRow(ScheduleItem item, DateOnly today)
    {
        var id = item.Id;
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand };

        var trash = HeaderGlyph("\uE74D", () =>
        {
            var next = _engine.Remove(id);
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        }, 12);
        trash.Opacity = 0.35;
        DockPanel.SetDock(trash, Dock.Right);
        row.Children.Add(trash);

        var check = new CheckBox
        {
            IsChecked = item.IsCompleted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        check.Click += (_, _) =>
        {
            var next = _engine.ToggleComplete(id);
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        };
        row.Children.Add(check);

        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Fill = TokenBrushes.From(ScheduleColors.Argb(item.ColorId, _isLight))
        };
        row.Children.Add(dot);

        var copyStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = item.Title,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = item.IsCompleted ? 0.45 : 1.0
        };
        if (item.IsCompleted)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
        }
        copyStack.Children.Add(title);

        var timeText = item.IsAllDay || item.Time == null ? UiText.ScheduleAllDay : item.Time.Value.ToString(@"hh\:mm");
        var categoryTitle = _engine.Settings.GetCategoryTitle(item.ColorId);
        var subText = $"{timeText} \u2022 {categoryTitle}";

        var subtitle = new TextBlock
        {
            Text = subText,
            FontSize = 10.5,
            Opacity = 0.6,
            Margin = new Thickness(0, 1, 0, 0)
        };
        copyStack.Children.Add(subtitle);

        row.Children.Add(copyStack);
        return row;
    }

    private void RenderCountdowns()
    {
        _countdownsList.Children.Clear();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = _engine.Settings.Items
            .Where(i => i.Kind == ScheduleKind.Countdown)
            .OrderBy(i => Math.Abs(i.Date.DayNumber - today.DayNumber))
            .ToList();

        if (items.Count == 0)
        {
            _countdownsList.Children.Add(new TextBlock
            {
                Text = UiText.ScheduleEmptyCountdowns,
                FontSize = 12,
                Opacity = 0.6,
                Margin = new Thickness(0, 16, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (var item in items)
        {
            var id = item.Id;
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4), Cursor = Cursors.Hand };

            var trash = HeaderGlyph("\uE74D", () =>
            {
                var next = _engine.Remove(id);
                SettingsChanged?.Invoke(this, next);
                UpdateTile();
                RenderList();
            }, 12);
            trash.Opacity = 0.35;
            DockPanel.SetDock(trash, Dock.Right);
            row.Children.Add(trash);

            var diffText = ScheduleFormat.DaysRemaining(item.Date, today);
            var pill = new Border
            {
                Background = TokenBrushes.From(0x280078D4u),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = diffText, FontSize = 11, FontWeight = FontWeights.SemiBold }
            };
            DockPanel.SetDock(pill, Dock.Right);
            row.Children.Add(pill);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = item.Date.ToString("yyyy-MM-dd"),
                FontSize = 10,
                Opacity = 0.6
            });

            row.Children.Add(titleStack);
            _countdownsList.Children.Add(row);
        }
    }

    private void AddFromInput()
    {
        var raw = _addInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (_selectedTab == 0)
        {
            // Parse event: e.g. "14:30 方案评审" or "9:00 开会"
            TimeSpan? time = null;
            var isAllDay = true;
            var title = raw;

            var space = raw.IndexOf(' ');
            if (space > 0)
            {
                var prefix = raw[..space].Trim();
                if (ScheduleFormat.TryParseClock(prefix, out var parsed))
                {
                    time = parsed;
                    isAllDay = false;
                    title = raw[(space + 1)..].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var item = new ScheduleItem
            {
                Title = title,
                Kind = ScheduleKind.Event,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Time = time,
                IsAllDay = isAllDay,
                ColorId = _selectedAddColor
            };

            if (!_engine.TryAdd(item, DateOnly.FromDateTime(DateTime.Today), out var next))
            {
                UserAlerts.Show("WinDock", UiText.ScheduleFull);
                return;
            }

            _addInput.Text = "";
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        }
        else
        {
            var date = _datePicker.SelectedDate.HasValue
                ? DateOnly.FromDateTime(_datePicker.SelectedDate.Value)
                : DateOnly.FromDateTime(DateTime.Today.AddDays(7));

            var item = new ScheduleItem
            {
                Title = raw,
                Kind = ScheduleKind.Countdown,
                Date = date,
                ColorId = _selectedAddColor
            };

            if (!_engine.TryAdd(item, DateOnly.FromDateTime(DateTime.Today), out var next))
            {
                UserAlerts.Show("WinDock", UiText.ScheduleFull);
                return;
            }

            _addInput.Text = "";
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        }
    }

    private static TextBlock HeaderLabel(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid();
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

    private static Border TabItem(TextBlock label, Action click)
    {
        var border = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            Child = label
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return border;
    }

    private static TextBlock HeaderGlyph(string glyph, Action click, double size = 13)
    {
        var block = new TextBlock
        {
            Text = glyph,
            FontFamily = glyph == "\u00D7" ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = size,
            Opacity = 0.7,
            Padding = new Thickness(6, 4, 6, 4),
            Background = Brushes.Transparent,
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
