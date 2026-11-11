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

public sealed class ScheduleWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IScheduleConfigurable, IWidgetThemeAware, ILocalizable, IWidgetDetachable, IIslandLayoutAware, IDockEdgeAware
{
    private readonly ScheduleEngine _engine = new();
    private bool _isLight;
    private ScheduleColorId _selectedAddColor = ScheduleColorId.Orange;
    private bool _isAllDayExpanded = true;
    private int _selectedTab; // 0 = Events, 1 = Countdowns

    // Dock Island Tile elements
    private readonly Border _tile;
    private readonly StackPanel _dateColumn = new();
    private readonly TextBlock _badgeWeekday = new();
    private readonly TextBlock _badgeDay = new();
    private readonly StackPanel _agendaHost = new();
    private readonly StackPanel _tileRow;
    private bool _side;

    // Flyout & Popover
    private readonly Popup _popup;
    private readonly Border _panel;
    private Border _card = null!;
    private readonly List<Border> _chromeBorders = [];
    private readonly Ellipse[] _categoryDots = new Ellipse[ScheduleColors.Count];
    private Border _dateHost = null!;
    private Border _addField = null!;
    private PhosphorGlyph _addGlyph = null!;
    private bool _painting;
    private readonly StackPanel _agendaView = new();
    private readonly StackPanel _categoriesView = new();
    private readonly StackPanel _layoutsView = new();
    private readonly List<(string Layout, Border Row, PhosphorGlyph Check, TextBlock Title)> _layoutRows = [];
    private PhosphorGlyph _btnLayouts = null!;
    private Border _btnClose = null!;
    private string? _paintedLayout;

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
    private readonly PhosphorGlyph _allDayChevron = new(PhosphorIcon.CaretUp, 10);
    private readonly TextBlock _allDayHeaderLabel = new();
    private readonly StackPanel _upcomingContainer = new();
    private readonly StackPanel _upcomingList = new();
    private readonly TextBlock _upcomingHeaderLabel = new();
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
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        Padding = new Thickness(8, 0, 8, 0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly TextBox _addYear = ClockDigit(4, 40);
    private readonly TextBox _addMonth = ClockDigit(2, 24);
    private readonly TextBox _addDay = ClockDigit(2, 24);
    private readonly TextBox _addHour = ClockDigit();
    private readonly TextBox _addMinute = ClockDigit();
    private readonly TextBox _editHour = ClockDigit();
    private readonly TextBox _editMinute = ClockDigit();
    private readonly TextBox _editYear = ClockDigit(4, 40);
    private readonly TextBox _editMonth = ClockDigit(2, 24);
    private readonly TextBox _editDay = ClockDigit(2, 24);
    private readonly TextBox _editTitle = new();
    private readonly TextBlock _editDateCaption = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        Opacity = 0.62,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0)
    };
    private readonly DockPanel _editDateRow = new()
    {
        Margin = new Thickness(0, 6, 0, 0),
        LastChildFill = false
    };
    private readonly DockPanel _editClockRow = new()
    {
        Margin = new Thickness(0, 6, 0, 0),
        LastChildFill = false
    };
    private bool _editCountdown;
    private DateOnly _editDate = DateOnly.FromDateTime(DateTime.Today);
    private enum EditFocus
    {
        Title,
        Time,
        Date
    }
    private readonly TextBlock _addTimeCaption = new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        Opacity = 0.62,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0)
    };
    private readonly TextBlock _addAllDayLabel = ClockChipLabel();
    private readonly TextBlock _editAllDayLabel = ClockChipLabel();
    private readonly StackPanel _addTimeRow = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 6, 0, 0)
    };
    private readonly StackPanel _editorBody = new();
    private Border _addTimeHost = null!;
    private Border _editTimeHost = null!;
    private Border _editDateHost = null!;
    private Border _addAllDayChip = null!;
    private Border _editAllDayChip = null!;
    private PhosphorGlyph _editSave = null!;
    private string? _editingId;
    private bool _addAllDay;
    private bool _editAllDay;
    private bool _committing;

    // Category Settings Elements
    private readonly TextBox[] _categoryInputs = new TextBox[ScheduleColors.Count];
    private readonly TextBlock _categoriesHelp = new();

    private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    public event EventHandler<ScheduleSettings>? SettingsChanged;

    public event EventHandler? IslandLayoutChanged;

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public void ApplyPaper(string? paper)
    {
    }

    public ScheduleWidget()
    {
        _dateColumn.Orientation = Orientation.Vertical;
        _dateColumn.VerticalAlignment = VerticalAlignment.Center;
        _dateColumn.Margin = new Thickness(0, 0, 8, 0);

        _badgeWeekday.FontSize = DockTokens.SecondaryFontSize;
        _badgeWeekday.FontWeight = FontWeights.SemiBold;
        _badgeWeekday.Foreground = TokenBrushes.From(ScheduleColors.Argb(ScheduleColorId.Red, isLight: false));
        _badgeWeekday.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeWeekday.Margin = new Thickness(0, 0, 0, -1);

        _badgeDay.FontSize = DockTokens.PrimaryFontSize;
        _badgeDay.FontWeight = FontWeights.SemiBold;
        _badgeDay.Foreground = Brushes.White;
        _badgeDay.HorizontalAlignment = HorizontalAlignment.Center;

        _dateColumn.Children.Add(_badgeWeekday);
        _dateColumn.Children.Add(_badgeDay);

        _agendaHost.VerticalAlignment = VerticalAlignment.Center;

        _tileRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _dateColumn, _agendaHost }
        };

        _tile = WidgetChrome.Wrap(_tileRow, minWidth: 120);
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
        _tabEventsLabel.FontSize = 12;
        _tabEventsLabel.FontWeight = FontWeights.SemiBold;
        _tabEventsLabel.VerticalAlignment = VerticalAlignment.Center;
        _tabEvents = TabItem(_tabEventsLabel, () => SwitchTab(0));

        _tabCountdownsLabel.Text = UiText.ScheduleTabCountdowns;
        _tabCountdownsLabel.FontSize = 12;
        _tabCountdownsLabel.FontWeight = FontWeights.SemiBold;
        _tabCountdownsLabel.VerticalAlignment = VerticalAlignment.Center;
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
        _popup.Opened += (_, _) => PaintFlyout();
        _popup.Closed += (_, _) => CommitEdit(discardIfEmpty: true);

        _panel = BuildPanel();
        _popup.Child = _panel;

        _tickTimer.Tick += (_, _) => UpdateTile();
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

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        UpdateTile();
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
        _badgeWeekday.Foreground = TokenBrushes.From(ScheduleColors.Argb(ScheduleColorId.Red, _isLight));
        _badgeDay.Foreground = _isLight ? TokenBrushes.From(DockTokens.Light.Text) : Brushes.White;
        UpdateTile();
        RenderList();
        UpdateAddColorButton();
        PaintFlyout();
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.ScheduleTodayHeader;
        _btnCalendarsText.Text = UiText.ScheduleCalendars;
        _tabEventsLabel.Text = UiText.ScheduleTabEvents;
        _tabCountdownsLabel.Text = UiText.ScheduleTabCountdowns;
        _allDayHeaderLabel.Text = UiText.ScheduleAllDaySection;
        _upcomingHeaderLabel.Text = UiText.ScheduleLater;
        _categoriesHelp.Text = UiText.ScheduleColorDesc;
        _addTimeCaption.Text = UiText.ScheduleTime;
        _editDateCaption.Text = UiText.ScheduleDate;
        _addAllDayLabel.Text = UiText.ScheduleAllDay;
        _editAllDayLabel.Text = UiText.ScheduleAllDay;
        if (_editSave is not null)
        {
            _editSave.ToolTip = UiText.ScheduleDone;
        }

        if (_dateHost is not null)
        {
            _dateHost.ToolTip = UiText.ScheduleDate;
        }

        if (_editDateHost is not null)
        {
            _editDateHost.ToolTip = UiText.ScheduleEditDate;
        }

        if (_btnLayouts is not null)
        {
            _btnLayouts.ToolTip = UiText.Layout;
        }

        foreach (var row in _layoutRows)
        {
            row.Title.Text = ScheduleLayouts.Title(row.Layout);
        }

        _addInput.ToolTip = _selectedTab == 0 ? UiText.ScheduleAddEventPlaceholder : UiText.ScheduleAddCountdownPlaceholder;
        UpdateTile();
        RenderList();
        UpdateAddColorButton();
    }

    public void Open()
    {
        ShowAgenda();
        RenderList();
        PaintFlyout();
        _popup.IsOpen = true;
    }

    public void OpenCustomize()
    {
        ShowLayouts();
        PaintFlyout();
        _popup.IsOpen = true;
    }

    private void ShowAgenda()
    {
        _layoutsView.Visibility = Visibility.Collapsed;
        _categoriesView.Visibility = Visibility.Collapsed;
        _agendaView.Visibility = Visibility.Visible;
    }

    private void ClosePopup() => _popup.IsOpen = false;

    private Border BuildPanel()
    {
        // 1. Build Agenda View
        BuildAgendaView();

        // 2. Build Categories Management View
        BuildCategoriesView();
        BuildLayoutsView();

        var container = new Grid
        {
            Width = 320,
            Children = { _agendaView, _categoriesView, _layoutsView }
        };

        var host = WidgetChrome.Flyout(container, 320);
        _card = (Border)host.Child;
        return host;
    }

    private void BuildAgendaView()
    {
        // Header (Image 2): "Today" on left, "Calendars..." capsule button and close button on right
        _btnClose = WidgetChrome.HeaderClose(ClosePopup, opacity: 1);
        _btnLayouts = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, ShowLayouts, opacity: 1);
        _btnLayouts.ToolTip = UiText.Layout;

        _btnCalendarsText.Text = UiText.ScheduleCalendars;
        _btnCalendarsText.FontSize = 12;
        _btnCalendarsText.FontWeight = FontWeights.Medium;
        _btnCalendarsText.VerticalAlignment = VerticalAlignment.Center;

        _btnCalendars.Height = DockTokens.FlyoutControlHeight;
        _btnCalendars.CornerRadius = new CornerRadius(DockTokens.TileCornerRadius);
        _btnCalendars.Padding = new Thickness(10, 0, 10, 0);
        _btnCalendars.Margin = new Thickness(4, 0, 0, 0);
        _btnCalendars.VerticalAlignment = VerticalAlignment.Center;
        _btnCalendars.Cursor = Cursors.Hand;
        _btnCalendars.Child = _btnCalendarsText;
        _btnCalendars.MouseLeftButtonUp += (_, e) =>
        {
            OpenCategoriesView();
            e.Handled = true;
        };

        var header = HeaderRow(_flyoutTitle, _btnLayouts, _btnCalendars, _btnClose);

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

        _allDayChevron.Icon = PhosphorIcon.CaretUp;
        _allDayChevron.Opacity = 0.6;
        _allDayChevron.VerticalAlignment = VerticalAlignment.Center;
        _allDayChevron.IsHitTestVisible = false;

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
            _allDayChevron.Icon = _isAllDayExpanded ? PhosphorIcon.CaretUp : PhosphorIcon.CaretDown;
            e.Handled = true;
        };

        _allDayContainer.Children.Add(allDayHeaderRow);
        _allDayContainer.Children.Add(_allDayList);
        _allDayContainer.Visibility = Visibility.Collapsed;

        _eventsContainer.Children.Clear();
        _eventsContainer.Children.Add(_eventsList);
        _eventsContainer.Children.Add(_allDayContainer);

        _upcomingHeaderLabel.Text = UiText.ScheduleLater;
        _upcomingHeaderLabel.FontSize = 12;
        _upcomingHeaderLabel.FontWeight = FontWeights.SemiBold;
        _upcomingHeaderLabel.Opacity = 0.65;
        _upcomingHeaderLabel.Margin = new Thickness(0, 8, 0, 6);
        _upcomingContainer.Children.Add(_upcomingHeaderLabel);
        _upcomingContainer.Children.Add(_upcomingList);
        _upcomingContainer.Visibility = Visibility.Collapsed;
        _eventsContainer.Children.Add(_upcomingContainer);

        // Quick add bar
        _addColorDot.Width = 16;
        _addColorDot.Height = 16;
        _addColorDot.VerticalAlignment = VerticalAlignment.Center;
        _addColorDot.HorizontalAlignment = HorizontalAlignment.Center;

        _addColorBtn.Width = DockTokens.FlyoutControlHeight;
        _addColorBtn.Height = DockTokens.FlyoutControlHeight;
        _addColorBtn.CornerRadius = new CornerRadius(DockTokens.FlyoutControlHeight / 2);
        _addColorBtn.Margin = new Thickness(0, 0, 6, 0);
        _addColorBtn.VerticalAlignment = VerticalAlignment.Center;
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

        _addGlyph = WidgetChrome.HeaderIcon(PhosphorIcon.Plus, AddFromInput, opacity: 1);
        _addGlyph.Margin = new Thickness(6, 0, 0, 0);
        _addGlyph.ToolTip = UiText.ScheduleAdd;

        _addInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AddFromInput();
                e.Handled = true;
            }
        };

        _dateHost = DateField(_addYear, _addMonth, _addDay);
        _dateHost.Margin = new Thickness(0, 0, 6, 0);
        _dateHost.Visibility = Visibility.Collapsed;
        _dateHost.ToolTip = UiText.ScheduleDate;

        _addField = WidgetChrome.Field(_addInput);
        _chromeBorders.Add(_addField);

        BuildClocks();

        var addRow = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_addGlyph, Dock.Right);
        addRow.Children.Add(_addGlyph);
        DockPanel.SetDock(_addColorBtn, Dock.Left);
        addRow.Children.Add(_addColorBtn);
        DockPanel.SetDock(_dateHost, Dock.Left);
        addRow.Children.Add(_dateHost);
        addRow.Children.Add(_addField);

        var addHost = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        addHost.Children.Add(addRow);
        addHost.Children.Add(_addTimeRow);

        _agendaView.Children.Add(header);
        _agendaView.Children.Add(tabs);
        _agendaView.Children.Add(_scrollList);
        _agendaView.Children.Add(addHost);

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
        doneBtn.Tag = "tint";
        doneBtn.Foreground = TokenBrushes.From(0xFF0078D4);
        doneBtn.MouseLeftButtonUp += (_, e) =>
        {
            SaveCategoriesAndClose();
            e.Handled = true;
        };

        catTitle.HorizontalAlignment = HorizontalAlignment.Center;
        var catHeader = new Grid();
        catHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        catHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        catHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        catHeader.Children.Add(backBtn);
        Grid.SetColumn(catTitle, 1);
        catHeader.Children.Add(catTitle);
        Grid.SetColumn(doneBtn, 2);
        catHeader.Children.Add(doneBtn);

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
            _categoryDots[i] = dot;
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);

            var box = new TextBox
            {
                FontFamily = WidgetFonts.Ui,
                FontSize = 13,
                Padding = new Thickness(8, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _categoryInputs[i] = box;
            var field = WidgetChrome.Field(box);
            _chromeBorders.Add(field);
            row.Children.Add(field);

            listStack.Children.Add(row);
        }

        var resetLabel = HeaderLabel(UiText.ScheduleResetDefaults);
        resetLabel.FontSize = 12;
        resetLabel.FontWeight = FontWeights.Normal;
        var resetBtn = new Border
        {
            Height = DockTokens.FlyoutControlHeight,
            Padding = new Thickness(10, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Child = resetLabel
        };
        resetBtn.MouseLeftButtonUp += (_, e) =>
        {
            FillCategoryInputs(useDefaults: true);
            e.Handled = true;
        };
        WidgetChrome.MarkQuiet(resetBtn);
        _chromeBorders.Add(resetBtn);

        _categoriesView.Children.Add(catHeader);
        _categoriesView.Children.Add(_categoriesHelp);
        _categoriesView.Children.Add(listStack);
        _categoriesView.Children.Add(resetBtn);
    }

    private void OpenCategoriesView()
    {
        CommitEdit(discardIfEmpty: true);
        FillCategoryInputs(useDefaults: false);
        _agendaView.Visibility = Visibility.Collapsed;
        _layoutsView.Visibility = Visibility.Collapsed;
        _categoriesView.Visibility = Visibility.Visible;
    }

    private void FillCategoryInputs(bool useDefaults)
    {
        for (var i = 0; i < ScheduleColors.Count; i++)
        {
            var colorId = (ScheduleColorId)i;
            _categoryInputs[i].Text = useDefaults
                ? ScheduleColors.DefaultTitle(colorId)
                : _engine.Settings.GetCategoryTitle(colorId);
        }
    }

    private void SaveCategoriesAndClose()
    {
        var cats = new List<ScheduleCategory>();
        for (var i = 0; i < ScheduleColors.Count; i++)
        {
            cats.Add(new ScheduleCategory
            {
                ColorId = (ScheduleColorId)i,
                Title = ScheduleSettings.StoredCategoryTitle((ScheduleColorId)i, _categoryInputs[i].Text)
            });
        }

        var next = ScheduleSettings.Normalize(_engine.Settings with { Categories = cats });
        _engine.ApplySettings(next);
        SettingsChanged?.Invoke(this, next);

        ShowAgenda();

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
        if (_selectedTab != index)
        {
            CommitEdit(discardIfEmpty: true);
        }

        _selectedTab = index;
        _tabEvents.Opacity = index == 0 ? 1.0 : 0.55;
        _tabCountdowns.Opacity = index == 1 ? 1.0 : 0.55;

        _btnCalendars.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _addColorBtn.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _dateHost.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        _addTimeRow.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        var layout = ScheduleLayouts.Normalize(_engine.Settings.Layout);
        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        var showDate = layout != ScheduleLayouts.NextEvent;
        _dateColumn.Visibility = showDate ? Visibility.Visible : Visibility.Collapsed;
        _tileRow.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _tileRow.HorizontalAlignment = HorizontalAlignment.Center;
        _dateColumn.HorizontalAlignment = HorizontalAlignment.Center;
        _agendaHost.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeDay.FontSize = _side ? 20 : DockTokens.PrimaryFontSize;
        _dateColumn.Margin = _side
            ? new Thickness(0, 0, 0, layout == ScheduleLayouts.Date ? 0 : 2)
            : new Thickness(0, 0, layout == ScheduleLayouts.Date ? 0 : 8, 0);
        WidgetChrome.FitEdge(_tile, _side, 120);

        if (layout == ScheduleLayouts.Date)
        {
            _agendaHost.Visibility = Visibility.Collapsed;
            _agendaHost.Width = double.NaN;
            if (!_side)
            {
                _tile.MinWidth = 0;
                _tile.MaxWidth = double.PositiveInfinity;
            }
        }
        else
        {
            _agendaHost.Visibility = Visibility.Visible;
            _agendaHost.Width = _side
                ? DockTokens.SideTileWidth - 16
                : layout == ScheduleLayouts.DateAgenda ? DockTokens.ScheduleAgendaWidth : double.NaN;
            if (!_side)
            {
                _tile.MinWidth = 120;
                _tile.MaxWidth = 260;
            }

            if (_side || layout != ScheduleLayouts.DateAgenda)
            {
                PaintNextEvent(now, textBrush, layout == ScheduleLayouts.NextEvent || _side);
            }
            else
            {
                PaintAgenda(now, textBrush);
            }
        }

        if (!string.Equals(_paintedLayout, layout, StringComparison.Ordinal))
        {
            _paintedLayout = layout;
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PaintAgenda(DateTime now, Brush textBrush)
    {
        var upcoming = _engine.GetTodayUpcomingEvents(now, limit: 3);
        if (upcoming.Count == 0)
        {
            _agendaHost.Children.Add(TileStatus(UiText.ScheduleNoEvents, textBrush));
            return;
        }

        foreach (var item in upcoming)
        {
            var row = new DockPanel
            {
                Height = 12,
                Margin = new Thickness(0, 0.5, 0, 0.5),
                LastChildFill = true
            };

            var timeBlock = new TextBlock
            {
                Text = ScheduleEngine.ClockOf(item).ToString(@"hh\:mm"),
                FontSize = 9,
                Opacity = 0.65,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = textBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(timeBlock, Dock.Right);
            row.Children.Add(timeBlock);

            var dot = ColorDot(item, 4.5, new Thickness(0, 0, 4, 0));
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);

            row.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = textBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            _agendaHost.Children.Add(row);
        }
    }

    private void PaintNextEvent(DateTime now, Brush textBrush, bool hero)
    {
        var item = _engine.GetNextUpcomingEvent(now)
            ?? _engine.GetTodayUpcomingEvents(now, limit: 8).LastOrDefault();
        if (item is null)
        {
            _agendaHost.Children.Add(TileStatus(UiText.ScheduleNoEvents, textBrush));
            return;
        }

        var titleRow = new StackPanel
        {
            Orientation = _side ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(ColorDot(item, hero ? 6 : 5.5, _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 5, 0)));
        titleRow.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = _side ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left,
            HorizontalAlignment = _side ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            Width = _side ? DockTokens.SideTileWidth - 16 : double.NaN,
            MaxWidth = _side ? DockTokens.SideTileWidth - 16 : hero ? 148 : 120,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var time = new TextBlock
        {
            Text = ScheduleEngine.ClockOf(item).ToString(@"hh\:mm"),
            FontSize = _side ? 10 : hero ? DockTokens.PrimaryFontSize : DockTokens.SecondaryFontSize,
            FontWeight = hero ? FontWeights.SemiBold : FontWeights.Normal,
            Opacity = hero ? 1 : 0.7,
            Margin = _side ? new Thickness(0, 1, 0, 0) : new Thickness(hero ? 11 : 10.5, hero ? 0 : 1, 0, 0),
            HorizontalAlignment = _side ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left,
            Width = _side ? DockTokens.SideTileWidth - 16 : double.NaN,
            Foreground = textBrush
        };
        if (_side)
        {
            _agendaHost.Children.Add(time);
            _agendaHost.Children.Add(titleRow);
        }
        else
        {
            _agendaHost.Children.Add(titleRow);
            _agendaHost.Children.Add(time);
        }
    }

    private TextBlock TileStatus(string text, Brush brush) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = _side ? TextWrapping.Wrap : TextWrapping.NoWrap,
        TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left,
        HorizontalAlignment = _side ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
        Width = _side ? DockTokens.SideTileWidth - 16 : double.NaN,
        Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center
    };

    private Ellipse ColorDot(ScheduleItem item, double size, Thickness margin) => new()
    {
        Width = size,
        Height = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = margin,
        Fill = TokenBrushes.From(ScheduleColors.Argb(item.ColorId, _isLight))
    };

    private void BuildLayoutsView()
    {
        _layoutsView.Visibility = Visibility.Collapsed;

        var back = HeaderLabel(UiText.ScheduleBack);
        back.FontSize = 12;
        back.Cursor = Cursors.Hand;
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowAgenda();
            e.Handled = true;
        };

        var title = HeaderLabel(UiText.Layout);
        title.FontSize = 13;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(back);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var list = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var layout in ScheduleLayouts.All)
        {
            list.Children.Add(LayoutRow(layout));
        }

        _layoutsView.Children.Add(header);
        _layoutsView.Children.Add(list);
    }

    private Border LayoutRow(string layout)
    {
        var check = WidgetChrome.AccentCheck(DockTokens.Settings.Accent);
        var label = HeaderLabel(ScheduleLayouts.Title(layout));
        label.FontSize = 13;
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(label);
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
        _layoutRows.Add((layout, row, check, label));
        return row;
    }

    private void ShowLayouts()
    {
        CommitEdit(discardIfEmpty: true);
        _agendaView.Visibility = Visibility.Collapsed;
        _categoriesView.Visibility = Visibility.Collapsed;
        _layoutsView.Visibility = Visibility.Visible;
        PaintLayouts();
    }

    private void SetLayout(string layout)
    {
        var next = ScheduleSettings.Normalize(_engine.Settings with { Layout = layout });
        if (ScheduleSettings.Same(_engine.Settings, next))
        {
            return;
        }

        _engine.ApplySettings(next);
        SettingsChanged?.Invoke(this, next);
        PaintLayouts();
        UpdateTile();
    }

    private void PaintLayouts()
    {
        var selectedFill = TokenBrushes.From(DockTokens.ResolveQuietFill(_isLight, WidgetChrome.ThemeBackdrop));
        var current = ScheduleLayouts.Normalize(_engine.Settings.Layout);
        foreach (var (layout, row, check, _) in _layoutRows)
        {
            var selected = string.Equals(layout, current, StringComparison.Ordinal);
            row.Background = selected ? selectedFill : Brushes.Transparent;
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
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

    private void PaintFlyout()
    {
        if (_painting || _card is null)
        {
            return;
        }

        _painting = true;
        try
        {
            var ink = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
            var quiet = TokenBrushes.From(DockTokens.ResolveQuietFill(_isLight, WidgetChrome.ThemeBackdrop));
            WidgetChrome.ApplyTheme(_card, _isLight, WidgetChrome.ThemeBackdrop);
            foreach (var border in _chromeBorders)
            {
                WidgetChrome.ApplyTheme(border, _isLight, WidgetChrome.ThemeBackdrop);
            }

            _btnClose.Background = quiet;
            _btnCalendars.Background = quiet;
            _addColorBtn.Background = quiet;
            _addGlyph.Foreground = ink;
            _addGlyph.Background = quiet;
            if (_btnLayouts is not null)
            {
                _btnLayouts.Foreground = ink;
                _btnLayouts.Background = quiet;
            }

            PaintLayouts();
            PaintTab(_tabEvents, _selectedTab == 0);
            PaintTab(_tabCountdowns, _selectedTab == 1);
            for (var i = 0; i < _categoryDots.Length; i++)
            {
                if (_categoryDots[i] is not null)
                {
                    _categoryDots[i].Fill = TokenBrushes.From(ScheduleColors.Argb((ScheduleColorId)i, _isLight));
                }
            }

            PaintClocks();
            WidgetChrome.PaintScrollViewer(_scrollList, _isLight);
        }
        finally
        {
            _painting = false;
        }
    }

    private void PaintTab(Border tab, bool selected)
    {
        tab.Background = selected
            ? TokenBrushes.From(DockTokens.ResolveQuietFill(_isLight, WidgetChrome.ThemeBackdrop))
            : Brushes.Transparent;
        tab.CornerRadius = new CornerRadius(DockTokens.TileCornerRadius);
        tab.BorderThickness = new Thickness(0);
    }

    private void RenderEvents()
    {
        _eventsList.Children.Clear();
        _allDayList.Children.Clear();
        _upcomingList.Children.Clear();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdue = _engine.GetOverdueEvents(today);
        var upcoming = _engine.GetUpcomingEvents(today);
        var (timed, allDay) = _engine.GetTodayEvents(today);

        if (timed.Count == 0 && allDay.Count == 0 && overdue.Count == 0 && upcoming.Count == 0)
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
            _upcomingContainer.Visibility = Visibility.Collapsed;
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
                _eventsList.Children.Add(EventRow(item, today));
            }
        }

        var todayEvents = timed.Concat(allDay)
            .OrderBy(i => i.IsCompleted)
            .ThenBy(ScheduleEngine.ClockOf)
            .ToList();
        foreach (var item in todayEvents)
        {
            _eventsList.Children.Add(EventRow(item, today));
        }

        _allDayContainer.Visibility = Visibility.Collapsed;

        if (upcoming.Count > 0)
        {
            _upcomingContainer.Visibility = Visibility.Visible;
            foreach (var item in upcoming)
            {
                _upcomingList.Children.Add(EventRow(item, today));
            }
        }
        else
        {
            _upcomingContainer.Visibility = Visibility.Collapsed;
        }
    }

    private UIElement EventRow(ScheduleItem item, DateOnly today) =>
        _editingId == item.Id ? CreateEventEditor(item) : CreateEventRow(item, today);

    private UIElement CreateEventRow(ScheduleItem item, DateOnly today)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand };
        AddEventChrome(row, item);

        var timeText = ScheduleEngine.ClockOf(item).ToString(@"hh\:mm");
        var timeChip = new Border
        {
            MinWidth = 52,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Opacity = item.IsCompleted ? 0.45 : 1,
            Background = TokenBrushes.From(DockTokens.ResolveQuietFill(_isLight, WidgetChrome.ThemeBackdrop)),
            BorderBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke),
            BorderThickness = new Thickness(1),
            ToolTip = UiText.ScheduleEditTime,
            Child = new TextBlock
            {
                Text = timeText,
                FontFamily = WidgetFonts.Ui,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text)
            }
        };
        timeChip.MouseLeftButtonUp += (_, e) =>
        {
            BeginEdit(item, EditFocus.Time);
            e.Handled = true;
        };
        DockPanel.SetDock(timeChip, Dock.Left);
        row.Children.Add(timeChip);

        var copyStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };
        copyStack.MouseLeftButtonUp += (_, e) =>
        {
            BeginEdit(item, EditFocus.Title);
            e.Handled = true;
        };
        var title = new TextBlock
        {
            FontFamily = WidgetFonts.Ui,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Opacity = item.IsCompleted ? 0.45 : 1.0
        };
        WidgetCopy.WrapUserText(title, item.Title, DockTokens.FlyoutUserCopyMaxWidth);
        if (item.IsCompleted)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
        }
        copyStack.Children.Add(title);

        var categoryTitle = _engine.Settings.GetCategoryTitle(item.ColorId);
        var showDate = item.Date != today;
        var detail = showDate ? $"{item.Date:MM-dd} \u2022 {categoryTitle}" : categoryTitle;
        var detailBlock = new TextBlock
        {
            Text = detail,
            FontSize = 10.5,
            Opacity = 0.6,
            Margin = new Thickness(0, 1, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Cursor = Cursors.Hand
        };
        if (showDate)
        {
            detailBlock.ToolTip = UiText.ScheduleEditDate;
            detailBlock.MouseLeftButtonUp += (_, e) =>
            {
                BeginEdit(item, EditFocus.Date);
                e.Handled = true;
            };
        }

        copyStack.Children.Add(detailBlock);

        row.Children.Add(copyStack);
        return row;
    }

    private UIElement CreateEventEditor(ScheduleItem item)
    {
        Detach(_editorBody);
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        AddEventChrome(row, item, checkable: item.Kind == ScheduleKind.Event);
        row.Children.Add(_editorBody);
        return row;
    }

    private void AddEventChrome(DockPanel row, ScheduleItem item, bool checkable = true)
    {
        var id = item.Id;
            var trash = WidgetChrome.HeaderIcon(PhosphorIcon.Trash, () => DeleteItem(id), opacity: 0.7);
        trash.Opacity = 0.35;
        DockPanel.SetDock(trash, Dock.Right);
        row.Children.Add(trash);

        if (checkable)
        {
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
            DockPanel.SetDock(check, Dock.Left);
            row.Children.Add(check);
        }

        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Fill = TokenBrushes.From(ScheduleColors.Argb(item.ColorId, _isLight))
        };
        DockPanel.SetDock(dot, Dock.Left);
        row.Children.Add(dot);
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
            if (_editingId == item.Id)
            {
                _countdownsList.Children.Add(CreateEventEditor(item));
                continue;
            }

            var id = item.Id;
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4), Cursor = Cursors.Hand };

            var trash = WidgetChrome.HeaderIcon(PhosphorIcon.Trash, () =>
            {
                var next = _engine.Remove(id);
                SettingsChanged?.Invoke(this, next);
                UpdateTile();
                RenderList();
            }, opacity: 0.7);
            trash.Opacity = 0.35;
            DockPanel.SetDock(trash, Dock.Right);
            row.Children.Add(trash);

            var diffText = ScheduleFormat.DaysRemaining(item.Date, today);
            var pill = new Border
            {
                Background = TokenBrushes.From(0x280078D4u),
                Height = DockTokens.FlyoutControlHeight,
                CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = diffText, FontSize = 11, FontWeight = FontWeights.SemiBold }
            };
            DockPanel.SetDock(pill, Dock.Right);
            row.Children.Add(pill);

            var titleBlock = new TextBlock
            {
                FontFamily = WidgetFonts.Ui,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            WidgetCopy.WrapUserText(titleBlock, item.Title, DockTokens.FlyoutUserCopyMaxWidth);
            var dateBlock = new TextBlock
            {
                Text = item.Date.ToString("yyyy-MM-dd"),
                FontSize = 10,
                Opacity = 0.6,
                Cursor = Cursors.Hand,
                ToolTip = UiText.ScheduleEditDate
            };
            dateBlock.MouseLeftButtonUp += (_, e) =>
            {
                BeginEdit(item, EditFocus.Date);
                e.Handled = true;
            };
            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            titleStack.MouseLeftButtonUp += (_, e) =>
            {
                BeginEdit(item, EditFocus.Title);
                e.Handled = true;
            };
            titleStack.Children.Add(titleBlock);
            titleStack.Children.Add(dateBlock);

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
            TimeSpan time;
            var title = raw;

            var space = raw.IndexOf(' ');
            if (space > 0 && ScheduleFormat.TryParseClock(raw[..space], out var parsed))
            {
                time = new TimeSpan(parsed.Hours, parsed.Minutes, 0);
                title = raw[(space + 1)..].Trim();
                _addHour.Text = time.Hours.ToString("00", CultureInfo.InvariantCulture);
                _addMinute.Text = time.Minutes.ToString("00", CultureInfo.InvariantCulture);
                PaintClocks();
            }
            else
            {
                time = ReadClock(_addHour, _addMinute);
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
                IsAllDay = false,
                ColorId = _selectedAddColor
            };

            if (!_engine.TryAdd(item, DateOnly.FromDateTime(DateTime.Today), out var next))
            {
                UserAlerts.Show(ProductInfo.Name, UiText.ScheduleFull);
                return;
            }

            _addInput.Text = "";
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        }
        else
        {
            var date = ReadDate(_addYear, _addMonth, _addDay, DateOnly.FromDateTime(DateTime.Today.AddDays(7)));

            var item = new ScheduleItem
            {
                Title = raw,
                Kind = ScheduleKind.Countdown,
                Date = date,
                ColorId = _selectedAddColor
            };

            if (!_engine.TryAdd(item, DateOnly.FromDateTime(DateTime.Today), out var next))
            {
                UserAlerts.Show(ProductInfo.Name, UiText.ScheduleFull);
                return;
            }

            _addInput.Text = "";
            SettingsChanged?.Invoke(this, next);
            UpdateTile();
            RenderList();
        }
    }

    private void BuildClocks()
    {
        _addHour.Text = ScheduleEngine.DefaultClock.Hours.ToString("00", CultureInfo.InvariantCulture);
        _addMinute.Text = ScheduleEngine.DefaultClock.Minutes.ToString("00", CultureInfo.InvariantCulture);
        BindDigits(_addHour, 23, editsItem: false, advanceTo: _addMinute);
        BindDigits(_addMinute, 59, editsItem: false, advanceTo: null);
        BindDigits(_editHour, 23, editsItem: true, advanceTo: _editMinute);
        BindDigits(_editMinute, 59, editsItem: true, advanceTo: null);
        WriteDate(_addYear, _addMonth, _addDay, DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        BindDatePart(_addYear, _addMonth, editsItem: false);
        BindDatePart(_addMonth, _addDay, editsItem: false);
        BindDatePart(_addDay, null, editsItem: false);
        BindDatePart(_editYear, _editMonth, editsItem: true);
        BindDatePart(_editMonth, _editDay, editsItem: true);
        BindDatePart(_editDay, null, editsItem: true);

        _addTimeHost = ClockField(_addHour, _addMinute);
        _editTimeHost = ClockField(_editHour, _editMinute);
        BindClockWheel(_addTimeHost, _addHour, _addMinute);
        BindClockWheel(_editTimeHost, _editHour, _editMinute);
        BindDateWheel(_dateHost, _addYear, _addMonth, _addDay);

        _addAllDayLabel.Text = UiText.ScheduleAllDay;
        _editAllDayLabel.Text = UiText.ScheduleAllDay;
        _addAllDayChip = AllDayChip(_addAllDayLabel, ToggleAddAllDay);
        _editAllDayChip = AllDayChip(_editAllDayLabel, ToggleEditAllDay);
        _addAllDayChip.Visibility = Visibility.Collapsed;
        _editAllDayChip.Visibility = Visibility.Collapsed;

        _addTimeCaption.Text = UiText.ScheduleTime;
        _addTimeRow.Children.Add(_addTimeCaption);
        _addTimeRow.Children.Add(_addTimeHost);
        _addTimeRow.Children.Add(_addAllDayChip);

        _editTitle.FontFamily = WidgetFonts.Ui;
        _editTitle.FontSize = 13;
        _editTitle.Padding = new Thickness(8, 0, 8, 0);
        _editTitle.BorderThickness = new Thickness(0);
        _editTitle.Background = Brushes.Transparent;
        _editTitle.VerticalContentAlignment = VerticalAlignment.Center;
        _editTitle.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        };

        var titleField = WidgetChrome.Field(_editTitle);
        _chromeBorders.Add(titleField);

        _editSave = WidgetChrome.HeaderIcon(PhosphorIcon.Check, () => CommitEdit(), opacity: 1);
        _editSave.Margin = new Thickness(6, 0, 0, 0);
        _editSave.ToolTip = UiText.ScheduleDone;

        _editDateHost = DateField(_editYear, _editMonth, _editDay);
        _editDateHost.ToolTip = UiText.ScheduleEditDate;
        BindDateWheel(_editDateHost, _editYear, _editMonth, _editDay);
        _editDateCaption.Text = UiText.ScheduleDate;
        DockPanel.SetDock(_editDateCaption, Dock.Left);
        _editDateRow.Children.Add(_editDateCaption);
        DockPanel.SetDock(_editDateHost, Dock.Left);
        _editDateRow.Children.Add(_editDateHost);

        _editClockRow.Children.Add(_editTimeHost);
        _editClockRow.Children.Add(_editAllDayChip);
        ShowEditClock(false);

        _editorBody.Children.Add(titleField);
        _editorBody.Children.Add(_editDateRow);
        _editorBody.Children.Add(_editClockRow);
        PaintClocks();
    }

    private void ToggleAddAllDay()
    {
        _addAllDay = !_addAllDay;
        PaintClock(_addAllDay, _addTimeHost, _addHour, _addMinute, _addAllDayChip);
    }

    private void ToggleEditAllDay()
    {
        _editAllDay = !_editAllDay;
        PaintClock(_editAllDay, _editTimeHost, _editHour, _editMinute, _editAllDayChip);
        if (!_editAllDay)
        {
            _editHour.Focus();
            _editHour.SelectAll();
        }
    }

    private void PaintClocks()
    {
        if (_addTimeHost is null || _editTimeHost is null)
        {
            return;
        }

        PaintClock(_addAllDay, _addTimeHost, _addHour, _addMinute, _addAllDayChip);
        PaintClock(_editAllDay, _editTimeHost, _editHour, _editMinute, _editAllDayChip);
    }

    private static void PaintClock(bool allDay, Border host, TextBox hour, TextBox minute, Border chip)
    {
        host.Opacity = allDay ? 0.45 : 1;
        host.IsHitTestVisible = !allDay;
        hour.IsReadOnly = allDay;
        minute.IsReadOnly = allDay;
        if (allDay)
        {
            WidgetChrome.MarkQuietOverlay(chip);
        }
        else
        {
            WidgetChrome.MarkQuiet(chip);
        }
    }

    private Border AllDayChip(TextBlock label, Action toggle)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Padding = new Thickness(8, 0, 8, 0),
            Height = DockTokens.FlyoutControlHeight,
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };
        chip.MouseLeftButtonUp += (_, e) =>
        {
            toggle();
            e.Handled = true;
        };
        _chromeBorders.Add(chip);
        return chip;
    }

    private Border ClockField(TextBox hour, TextBox minute)
    {
        var colon = new TextBlock
        {
            Text = ":",
            FontFamily = WidgetFonts.Ui,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 1)
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { hour, colon, minute }
        };
        var host = WidgetChrome.Field(row);
        host.Width = 84;
        host.Margin = new Thickness(0, 0, 6, 0);
        _chromeBorders.Add(host);
        return host;
    }

    private static TextBox ClockDigit(int length = 2, double width = 24)
    {
        var box = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = WidgetFonts.Ui,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            MaxLength = length,
            Width = width,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        InputMethod.SetIsInputMethodEnabled(box, false);
        return box;
    }

    private static TextBlock ClockChipLabel() => new()
    {
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void BindDigits(TextBox box, int max, bool editsItem, TextBox? advanceTo)
    {
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsDigit))
            {
                e.CancelCommand();
            }
        });
        box.PreviewTextInput += (_, e) =>
        {
            e.Handled = e.Text.Length == 0 || !e.Text.All(char.IsDigit);
            if (e.Handled || advanceTo is null || box.IsReadOnly)
            {
                return;
            }

            var proposed = box.Text.Length - box.SelectionLength + e.Text.Length;
            if (proposed < box.MaxLength)
            {
                return;
            }

            var next = advanceTo;
            box.Dispatcher.BeginInvoke(() =>
            {
                next.Focus();
                next.SelectAll();
            }, DispatcherPriority.Background);
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                Nudge(box, 1, max);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                Nudge(box, -1, max);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (editsItem)
                {
                    CommitEdit();
                }
                else
                {
                    AddFromInput();
                }

                e.Handled = true;
            }
            else if (editsItem && e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        };
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
    }

    private void BindClockWheel(UIElement element, TextBox hour, TextBox minute)
    {
        MouseWheelEventHandler wheel = (_, e) =>
        {
            var box = ReferenceEquals(Keyboard.FocusedElement, minute) ? minute : hour;
            var max = ReferenceEquals(box, minute) ? 59 : 23;
            if (box.IsReadOnly)
            {
                return;
            }

            Nudge(box, e.Delta > 0 ? 1 : -1, max);
            e.Handled = true;
        };
        element.PreviewMouseWheel += wheel;
        element.MouseWheel += wheel;
        hour.PreviewMouseWheel += wheel;
        minute.PreviewMouseWheel += wheel;
    }

    private static void Nudge(TextBox box, int delta, int max)
    {
        if (box.IsReadOnly)
        {
            return;
        }

        var value = ReadPart(box) + delta;
        if (value > max)
        {
            value = 0;
        }
        else if (value < 0)
        {
            value = max;
        }

        box.Text = value.ToString("00", CultureInfo.InvariantCulture);
        box.CaretIndex = box.Text.Length;
    }

    private static int ReadPart(TextBox box) =>
        int.TryParse(box.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : 0;

    private static TimeSpan ReadClock(TextBox hour, TextBox minute) =>
        new(Math.Clamp(ReadPart(hour), 0, 23), Math.Clamp(ReadPart(minute), 0, 59), 0);

    private void ShowEditClock(bool countdown)
    {
        _editCountdown = countdown;
        Detach(_editSave);
        DockPanel.SetDock(_editSave, Dock.Right);
        if (countdown)
        {
            _editClockRow.Visibility = Visibility.Collapsed;
            _editDateRow.Children.Add(_editSave);
        }
        else
        {
            _editClockRow.Visibility = Visibility.Visible;
            _editClockRow.Children.Add(_editSave);
        }
    }

    private Border DateField(TextBox year, TextBox month, TextBox day)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { year, DateSep(), month, DateSep(), day }
        };
        var host = WidgetChrome.Field(row);
        host.Width = 120;
        _chromeBorders.Add(host);
        return host;
    }

    private static TextBlock DateSep() => new()
    {
        Text = "-",
        Width = 8,
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static void WriteDate(TextBox year, TextBox month, TextBox day, DateOnly date)
    {
        year.Text = date.Year.ToString("0000", CultureInfo.InvariantCulture);
        month.Text = date.Month.ToString("00", CultureInfo.InvariantCulture);
        day.Text = date.Day.ToString("00", CultureInfo.InvariantCulture);
    }

    private static DateOnly ReadDate(TextBox year, TextBox month, TextBox day, DateOnly fallback)
    {
        if (year.Text.Length != 4
            || !int.TryParse(year.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            || y is < 1 or > 9999
            || !int.TryParse(month.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || m is < 1 or > 12
            || !int.TryParse(day.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            || d < 1)
        {
            return fallback;
        }

        d = Math.Min(d, DateTime.DaysInMonth(y, m));
        return new DateOnly(y, m, d);
    }

    private void BindDatePart(TextBox box, TextBox? advanceTo, bool editsItem)
    {
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsDigit))
            {
                e.CancelCommand();
            }
        });
        box.PreviewTextInput += (_, e) =>
        {
            e.Handled = e.Text.Length == 0 || !e.Text.All(char.IsDigit);
            if (e.Handled || advanceTo is null || box.IsReadOnly)
            {
                return;
            }

            var proposed = box.Text.Length - box.SelectionLength + e.Text.Length;
            if (proposed < box.MaxLength)
            {
                return;
            }

            var next = advanceTo;
            box.Dispatcher.BeginInvoke(() =>
            {
                next.Focus();
                next.SelectAll();
            }, DispatcherPriority.Background);
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                NudgeDate(box, 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                NudgeDate(box, -1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (editsItem)
                {
                    CommitEdit();
                }
                else
                {
                    AddFromInput();
                }

                e.Handled = true;
            }
            else if (editsItem && e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        };
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
    }

    private void BindDateWheel(UIElement element, TextBox year, TextBox month, TextBox day)
    {
        MouseWheelEventHandler wheel = (_, e) =>
        {
            var focused = Keyboard.FocusedElement as TextBox;
            var box = ReferenceEquals(focused, year) || ReferenceEquals(focused, month) || ReferenceEquals(focused, day)
                ? focused!
                : month;
            NudgeDate(box, e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        };
        element.PreviewMouseWheel += wheel;
        year.PreviewMouseWheel += wheel;
        month.PreviewMouseWheel += wheel;
        day.PreviewMouseWheel += wheel;
    }

    private void NudgeDate(TextBox box, int delta)
    {
        DateParts(box, out var year, out var month, out var day);
        if (ReferenceEquals(box, year))
        {
            var value = Math.Clamp(ReadPart(box) + delta, 1, 9999);
            box.Text = value.ToString("0000", CultureInfo.InvariantCulture);
        }
        else if (ReferenceEquals(box, month))
        {
            var value = ReadPart(box) + delta;
            if (value > 12)
            {
                value = 1;
            }
            else if (value < 1)
            {
                value = 12;
            }

            box.Text = value.ToString("00", CultureInfo.InvariantCulture);
        }
        else
        {
            var y = ReadPart(year);
            var m = ReadPart(month);
            var max = y is >= 1 and <= 9999 && m is >= 1 and <= 12 ? DateTime.DaysInMonth(y, m) : 31;
            var value = ReadPart(box) + delta;
            if (value > max)
            {
                value = 1;
            }
            else if (value < 1)
            {
                value = max;
            }

            box.Text = value.ToString("00", CultureInfo.InvariantCulture);
        }

        box.CaretIndex = box.Text.Length;
    }

    private void DateParts(TextBox box, out TextBox year, out TextBox month, out TextBox day)
    {
        if (ReferenceEquals(box, _addYear) || ReferenceEquals(box, _addMonth) || ReferenceEquals(box, _addDay))
        {
            year = _addYear;
            month = _addMonth;
            day = _addDay;
            return;
        }

        year = _editYear;
        month = _editMonth;
        day = _editDay;
    }

    private void BeginEdit(ScheduleItem item, EditFocus focus)
    {
        if (_committing)
        {
            return;
        }

        if (_editingId == item.Id)
        {
            FocusEditor(focus);
            return;
        }

        if (_editingId is not null)
        {
            CommitEdit(discardIfEmpty: true);
        }

        _editingId = item.Id;
        _editTitle.Text = item.Title;
        _editAllDay = false;
        var clock = ScheduleEngine.ClockOf(item);
        _editHour.Text = clock.Hours.ToString("00", CultureInfo.InvariantCulture);
        _editMinute.Text = clock.Minutes.ToString("00", CultureInfo.InvariantCulture);
        WriteDate(_editYear, _editMonth, _editDay, item.Date);
        _editDate = item.Date;
        ShowEditClock(item.Kind == ScheduleKind.Countdown);
        PaintClock(_editAllDay, _editTimeHost, _editHour, _editMinute, _editAllDayChip);
        RenderList();
        FocusEditor(focus);
    }

    private void FocusEditor(EditFocus focus)
    {
        var box = focus switch
        {
            EditFocus.Date => _editYear,
            EditFocus.Time when !_editCountdown && !_editAllDay => _editHour,
            _ => _editTitle
        };
        box.Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void CommitEdit(bool discardIfEmpty = false)
    {
        if (_committing || _editingId is null)
        {
            return;
        }

        var title = _editTitle.Text.Trim();
        if (title.Length == 0)
        {
            if (discardIfEmpty)
            {
                _editingId = null;
                RenderList();
            }

            return;
        }

        var id = _editingId;
        var allDay = false;
        var date = ReadDate(_editYear, _editMonth, _editDay, _editDate);
        TimeSpan? time = ReadClock(_editHour, _editMinute);
        _editingId = null;
        _committing = true;
        try
        {
            var current = _engine.Settings.Items.FirstOrDefault(item => item.Id == id);
            var currentTime = current?.Time is { } existing
                ? new TimeSpan(existing.Hours, existing.Minutes, 0)
                : (TimeSpan?)null;
            var countdown = current?.Kind == ScheduleKind.Countdown;
            var unchanged = current is not null
                && current.Title == title
                && current.Date == date
                && (countdown
                    || ((current.IsAllDay || current.Time is null) == allDay
                        && Nullable.Equals(allDay ? null : currentTime, time)));
            if (!unchanged && _engine.TryUpdate(id, title, date, time, allDay, out var next))
            {
                SettingsChanged?.Invoke(this, next);
            }

            UpdateTile();
            RenderList();
        }
        finally
        {
            _committing = false;
        }
    }

    private void CancelEdit()
    {
        if (_editingId is null)
        {
            return;
        }

        _editingId = null;
        RenderList();
    }

    private void DeleteItem(string id)
    {
        if (_editingId == id)
        {
            _editingId = null;
        }

        var next = _engine.Remove(id);
        SettingsChanged?.Invoke(this, next);
        UpdateTile();
        RenderList();
    }

    private static void Detach(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl control:
                control.Content = null;
                break;
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
            Height = DockTokens.FlyoutControlHeight,
            Background = Brushes.Transparent,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
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

}
