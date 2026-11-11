using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class AlarmWidget : UserControl, IWidgetOpenable, IAlarmConfigurable, IKeepAliveWhileRunning, IWidgetThemeAware, IIslandLayoutAware, ILocalizable, IWidgetDetachable, IDockEdgeAware
{
    private const uint Accent = 0xFF0078D4;
    private const double TileGlyph = 16;

    private readonly AlarmEngine _engine = new();
    private readonly TextBlock _tileTitle = WidgetCopy.Primary();
    private readonly TextBlock _tileDay = WidgetCopy.Secondary();
    private readonly TextBlock _tileStatus = WidgetCopy.Secondary();
    private readonly TextBlock _tileRemain = WidgetCopy.Secondary();
    private readonly PhosphorGlyph _glyph = new(PhosphorIcon.Alarm, TileGlyph) { Tag = "tint" };
    private readonly StackPanel _headline;
    private readonly StackPanel _trailing;
    private readonly StackPanel _dockCopy;
    private readonly Grid _dock;
    private bool _side;
    private readonly TextBlock _panelTime = WidgetCopy.Primary();
    private readonly TextBlock _panelStatus = WidgetCopy.Secondary();
    private readonly TextBlock _panelLabel = WidgetCopy.Primary();
    private readonly TextBlock _hint = WidgetCopy.Secondary();
    private readonly TextBlock _primaryLabel = WidgetCopy.Primary();
    private readonly TextBox _hours = DigitBox();
    private readonly TextBox _minutes = DigitBox();
    private readonly TextBox _label = FieldBox();
    private readonly StackPanel _setupPage;
    private readonly StackPanel _armedPage;
    private readonly Border _tile;
    private readonly Border _primary;
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _persistTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly List<(UIElement Element, string Part, int Max)> _wheelFields = [];
    private bool _applying;
    private bool _suppressFields;
    private bool _isLight;
    private bool _laidOutArmed;
    private string _laidOutLabel = "";
    private TextBlock _flyoutTitle = null!;
    private TextBlock _timeCaption = null!;
    private TextBlock _labelCaption = null!;

    public AlarmWidget()
    {
        _tileTitle.FontSize = DockTokens.PrimaryFontSize;
        _tileDay.Margin = new Thickness(8, 1, 0, 0);
        _tileDay.VerticalAlignment = VerticalAlignment.Center;
        _tileStatus.Margin = new Thickness(0);
        _tileRemain.FontSize = 10;
        _tileRemain.Margin = new Thickness(0, 1, 0, 0);
        _tileRemain.TextAlignment = TextAlignment.Center;
        _tileRemain.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.Margin = new Thickness(0);
        _panelTime.FontSize = 38;
        _panelTime.FontWeight = FontWeights.SemiBold;
        _panelTime.HorizontalAlignment = HorizontalAlignment.Center;
        _panelTime.Margin = new Thickness(0, 8, 0, 4);
        _panelStatus.FontSize = 13;
        _panelStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _panelLabel.FontSize = 14;
        _panelLabel.FontWeight = FontWeights.SemiBold;
        _panelLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _panelLabel.Margin = new Thickness(0, 6, 0, 0);
        _panelLabel.TextAlignment = TextAlignment.Center;
        WidgetCopy.WrapUserText(_panelLabel, string.Empty, DockTokens.FlyoutUserCopyMaxWidth);
        _hint.Margin = new Thickness(0);
        _primaryLabel.Tag = "tint";
        _primaryLabel.FontSize = 14;
        _primaryLabel.Foreground = Brushes.White;
        _primaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _primaryLabel.VerticalAlignment = VerticalAlignment.Center;

        _trailing = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 0, 0),
            Children = { _glyph, _tileRemain }
        };
        _headline = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tileTitle, _tileDay }
        };
        _tileStatus.HorizontalAlignment = HorizontalAlignment.Left;
        _tileTitle.SizeChanged += (_, _) => SyncTileLabelWidth();
        _tileDay.SizeChanged += (_, _) => SyncTileLabelWidth();
        _dockCopy = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _headline, _tileStatus }
        };
        _dock = new Grid { VerticalAlignment = VerticalAlignment.Center };
        _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dock.Children.Add(_dockCopy);
        Grid.SetColumn(_trailing, 1);
        _dock.Children.Add(_trailing);
        _tile = WidgetChrome.Wrap(_dock, 0);

        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Alarm));
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, close);

        var timeRow = FormRow(AlarmFormat.Time, TimeChip(), out _timeCaption);
        timeRow.Margin = new Thickness(0, 8, 0, 10);
        var labelRow = FormRow(AlarmFormat.Label, WidgetChrome.Field(_label), out _labelCaption);
        labelRow.Margin = new Thickness(0, 0, 0, 12);

        _primary = ActionButton(_primaryLabel, Accent, Toggle);
        _primary.HorizontalAlignment = HorizontalAlignment.Center;
        _primary.MinWidth = 120;
        _primary.Margin = new Thickness(0, 16, 0, 0);

        _setupPage = new StackPanel
        {
            Children = { timeRow, labelRow, _hint }
        };
        _armedPage = new StackPanel
        {
            Children = { _panelTime, _panelStatus, _panelLabel }
        };

        var body = new Grid();
        body.Children.Add(_setupPage);
        body.Children.Add(_armedPage);

        var panel = WidgetChrome.Flyout(new StackPanel
        {
            Children = { header, body, _primary }
        }, 300);
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
        _popup.Closed += (_, _) => OnPopupClosed();
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
        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            PaintFields();
        };

        BindField(_label, commitLabel: true);
        BindField(_hours, part: "hours", max: 23);
        BindField(_minutes, part: "minutes", max: 59);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Pulse();
        _persistTimer.Tick += (_, _) =>
        {
            _persistTimer.Stop();
            Persist();
        };
        Loaded += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            WidgetFlyout.Wheel += OnFlyoutWheel;
            Render();
            if (_engine.Armed)
            {
                _timer.Start();
                Pulse();
            }
        };
        Unloaded += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            if (!_engine.Armed)
            {
                _timer.Stop();
            }

            FlushPersist();
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        PaintFields();
        Render();
    }

    public event EventHandler<AlarmSettings>? SettingsChanged;

    public event EventHandler? IslandLayoutChanged;

    public bool IsAlive => _engine.Armed;

    public void Detach()
    {
        _timer.Stop();
        _persistTimer.Stop();
        _popup.IsOpen = false;
        WidgetFlyout.Wheel -= OnFlyoutWheel;
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Alarm);
        _timeCaption.Text = AlarmFormat.Time;
        _labelCaption.Text = AlarmFormat.Label;
        Render();
    }

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        _dock.ColumnDefinitions.Clear();
        _dock.RowDefinitions.Clear();
        if (_side)
        {
            _dock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _dock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(_dockCopy, 0);
            Grid.SetColumn(_trailing, 0);
            Grid.SetRow(_trailing, 0);
            Grid.SetRow(_dockCopy, 1);
            _trailing.Margin = new Thickness(0, 0, 0, 2);
            _trailing.HorizontalAlignment = HorizontalAlignment.Center;
            _dockCopy.HorizontalAlignment = HorizontalAlignment.Center;
            _headline.HorizontalAlignment = HorizontalAlignment.Center;
            _tileTitle.HorizontalAlignment = HorizontalAlignment.Center;
            _tileTitle.TextAlignment = TextAlignment.Center;
            _tileDay.HorizontalAlignment = HorizontalAlignment.Center;
            _tileStatus.HorizontalAlignment = HorizontalAlignment.Center;
            _tileStatus.TextAlignment = TextAlignment.Center;
            _tileRemain.HorizontalAlignment = HorizontalAlignment.Center;
            _tileRemain.TextAlignment = TextAlignment.Center;
        }
        else
        {
            _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(_dockCopy, 0);
            Grid.SetRow(_trailing, 0);
            Grid.SetColumn(_dockCopy, 0);
            Grid.SetColumn(_trailing, 1);
            _trailing.Margin = new Thickness(10, 0, 0, 0);
            _trailing.HorizontalAlignment = HorizontalAlignment.Right;
            _dockCopy.HorizontalAlignment = HorizontalAlignment.Left;
            _headline.HorizontalAlignment = HorizontalAlignment.Left;
            _tileTitle.HorizontalAlignment = HorizontalAlignment.Left;
            _tileTitle.TextAlignment = TextAlignment.Left;
            _tileDay.HorizontalAlignment = HorizontalAlignment.Left;
            _tileStatus.HorizontalAlignment = HorizontalAlignment.Left;
            _tileStatus.TextAlignment = TextAlignment.Left;
            _tileRemain.HorizontalAlignment = HorizontalAlignment.Right;
            _tileRemain.TextAlignment = TextAlignment.Left;
        }

        SyncTileLabelWidth();
        WidgetChrome.FitEdge(_tile, _side, 0);
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        PaintAccent();
    }

    public void ApplySettings(AlarmSettings settings)
    {
        _applying = true;
        _engine.ApplySettings(settings, Now);
        PaintFields();
        Render();
        _applying = false;
    }

    public void Open()
    {
        Render();
        PaintFields();
        _popup.IsOpen = true;
    }

    private void Pulse()
    {
        var finished = _engine.Tick(Now);
        Render();
        if (!finished)
        {
            return;
        }

        Persist();
        SystemSounds.Asterisk.Play();
        UserAlerts.Show(
            BuiltInWidgets.DisplayName(BuiltInWidgets.Alarm),
            AlarmFormat.FinishedMessage(_engine.Settings.Label));
        if (IsVisible && IsLoaded)
        {
            Open();
        }
    }

    private void Toggle()
    {
        if (_engine.Armed)
        {
            _engine.Cancel();
            _timer.Stop();
            Persist();
            PaintFields();
            Render();
            return;
        }

        CommitFields();
        _engine.Arm(Now);
        _timer.Start();
        Persist();
        PaintFields();
        Render();
    }

    private void CommitFields()
    {
        if (_suppressFields)
        {
            return;
        }

        _engine.SetLabel(_label.Text);
        _engine.SetTime(ReadPart(_hours), ReadPart(_minutes));
    }

    private void OnFlyoutWheel(object? sender, FlyoutWheelEventArgs e)
    {
        if (!_popup.IsOpen || _engine.Armed)
        {
            return;
        }

        foreach (var (element, part, max) in _wheelFields)
        {
            if (ContainsScreen(element, e.Screen))
            {
                NudgePart(part, e.Delta > 0 ? 1 : -1, max);
                return;
            }
        }
    }

    private static bool ContainsScreen(UIElement element, Point screen)
    {
        try
        {
            var local = element.PointFromScreen(screen);
            var size = element.RenderSize;
            return size.Width > 0 && size.Height > 0
                && local.X >= -8 && local.Y >= -8
                && local.X <= size.Width + 8 && local.Y <= size.Height + 8;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void NudgePart(string part, int delta, int max)
    {
        if (_engine.Armed || _suppressFields)
        {
            return;
        }

        CommitFields();
        var hour = _engine.Settings.Hour;
        var minute = _engine.Settings.Minute;
        if (part == "hours")
        {
            hour = AlarmSettings.Wrap(hour + delta, max);
        }
        else
        {
            minute = AlarmSettings.Wrap(minute + delta, max);
        }

        _engine.SetTime(hour, minute);
        SchedulePersist();
        PaintFields();
        Render();
    }

    private void SchedulePersist()
    {
        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void FlushPersist()
    {
        if (!_persistTimer.IsEnabled)
        {
            return;
        }

        _persistTimer.Stop();
        Persist();
    }

    private void Persist()
    {
        if (_applying)
        {
            return;
        }

        SettingsChanged?.Invoke(this, _engine.Settings);
    }

    private void Render()
    {
        var now = Now;
        if (_engine.Armed)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        var remaining = _engine.Remaining(now);
        var day = _engine.Day(now);
        var clock = AlarmFormat.Clock(_engine.Settings.Hour, _engine.Settings.Minute);
        var label = _engine.Settings.Label;
        if (_engine.Armed)
        {
            _tileTitle.Text = clock;
            _tileDay.Text = AlarmFormat.Day(day);
            _tileDay.Visibility = Visibility.Visible;
            _tileStatus.Text = label;
            _tileStatus.Visibility = string.IsNullOrEmpty(label) ? Visibility.Collapsed : Visibility.Visible;
            SyncTileLabelWidth();
            _tileRemain.Text = AlarmFormat.CompactRemaining(remaining);
            _tileRemain.Visibility = Visibility.Visible;
            _panelTime.Text = clock;
            _panelStatus.Text = AlarmFormat.ArmedStatus(day, remaining);
            WidgetCopy.WrapUserText(_panelLabel, label, DockTokens.FlyoutUserCopyMaxWidth);
            _panelLabel.Visibility = string.IsNullOrEmpty(label) ? Visibility.Collapsed : Visibility.Visible;
            _primaryLabel.Text = AlarmFormat.Cancel;
            WidgetChrome.MarkQuiet(_primary);
            _setupPage.Visibility = Visibility.Collapsed;
            _armedPage.Visibility = Visibility.Visible;
        }
        else
        {
            _tileTitle.Text = AlarmFormat.IdleTitle;
            _tileDay.Visibility = Visibility.Collapsed;
            _tileStatus.Text = string.Empty;
            _tileStatus.Visibility = Visibility.Collapsed;
            SyncTileLabelWidth();
            _tileRemain.Visibility = Visibility.Collapsed;
            _hint.Text = AlarmFormat.When(day, _engine.Settings.Hour, _engine.Settings.Minute);
            _primaryLabel.Text = AlarmFormat.Set;
            WidgetChrome.ClearQuiet(_primary, Accent);
            _setupPage.Visibility = Visibility.Visible;
            _armedPage.Visibility = Visibility.Collapsed;
        }

        PaintAccent();
        var laidOut = _engine.Armed;
        var labelKey = laidOut ? _engine.Settings.Label : "";
        if (_laidOutArmed != laidOut || _laidOutLabel != labelKey)
        {
            _laidOutArmed = laidOut;
            _laidOutLabel = labelKey;
            IslandLayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SyncTileLabelWidth()
    {
        if (!_engine.Armed || string.IsNullOrEmpty(_engine.Settings.Label))
        {
            _tileStatus.ClearValue(MaxWidthProperty);
            _tileStatus.TextWrapping = TextWrapping.NoWrap;
            _tileStatus.TextTrimming = TextTrimming.None;
            return;
        }

        if (_side)
        {
            WidgetCopy.TrimIslandText(_tileStatus, _engine.Settings.Label, DockTokens.SideTileWidth - 16);
            _tileStatus.TextAlignment = TextAlignment.Center;
            return;
        }

        var width = HeadlineContentWidth();
        if (width <= 0)
        {
            return;
        }

        WidgetCopy.TrimIslandText(_tileStatus, _engine.Settings.Label, width);
    }

    private double HeadlineContentWidth()
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _tileTitle.Measure(infinite);
        var width = _tileTitle.DesiredSize.Width;
        if (_tileDay.Visibility != Visibility.Visible)
        {
            return width;
        }

        _tileDay.Measure(infinite);
        return width + _tileDay.Margin.Left + _tileDay.Margin.Right + _tileDay.DesiredSize.Width;
    }

    private void PaintAccent()
    {
        var ink = _isLight ? DockTokens.Light.Text : DockTokens.Dark.Text;
        _glyph.Foreground = TokenBrushes.From(_engine.Armed ? DockTokens.Alarm : ink);
        _glyph.Icon = _engine.Armed ? PhosphorIcon.AlarmFill : PhosphorIcon.Alarm;
        _glyph.Opacity = _engine.Armed ? 1 : 0.72;
        _primaryLabel.Tag = "tint";
        _primaryLabel.Foreground = TokenBrushes.From(_engine.Armed ? ink : DockTokens.Settings.OnAccent);
    }

    private void PaintFields()
    {
        _suppressFields = true;
        _label.Text = _engine.Settings.Label;
        _hours.Text = _engine.Settings.Hour.ToString("00", CultureInfo.InvariantCulture);
        _minutes.Text = _engine.Settings.Minute.ToString("00", CultureInfo.InvariantCulture);
        _suppressFields = false;
    }

    private void ClosePopup()
    {
        _popup.IsOpen = false;
    }

    private void OnPopupClosed()
    {
        var before = _engine.Settings;
        CommitFields();
        if (!AlarmSettings.Same(before, _engine.Settings))
        {
            FlushPersist();
            Persist();
        }
        else
        {
            FlushPersist();
        }
    }

    private void BindField(TextBox box, bool commitLabel = false, string? part = null, int max = 0)
    {
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (commitLabel)
            {
                return;
            }

            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsDigit))
            {
                e.CancelCommand();
            }
        });
        if (!commitLabel)
        {
            box.PreviewTextInput += (_, e) =>
            {
                e.Handled = e.Text.Length == 0 || !e.Text.All(char.IsDigit);
            };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Space)
                {
                    e.Handled = true;
                    return;
                }

                if (part is null)
                {
                    return;
                }

                if (e.Key == Key.Up)
                {
                    NudgePart(part, 1, max);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    NudgePart(part, -1, max);
                    e.Handled = true;
                }
            };
        }

        box.LostKeyboardFocus += (_, _) =>
        {
            if (_suppressFields || _engine.Armed)
            {
                return;
            }

            var before = _engine.Settings;
            CommitFields();
            if (!AlarmSettings.Same(before, _engine.Settings))
            {
                Persist();
            }

            PaintFields();
            Render();
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitFields();
            if (!commitLabel)
            {
                Toggle();
            }
            else
            {
                Persist();
            }

            e.Handled = true;
        };
    }

    private Border TimeChip()
    {
        var colon = WidgetCopy.Primary();
        colon.Text = ":";
        colon.FontSize = 14;
        colon.Margin = new Thickness(0, 0, 0, 1);
        colon.VerticalAlignment = VerticalAlignment.Center;
        var clock = WidgetChrome.Glyph(PhosphorIcon.Clock);
        clock.IconSize = 12;
        clock.Opacity = 0.55;
        clock.Margin = new Thickness(8, 0, 2, 0);
        var hours = TimePart(_hours, "hours", 23);
        var minutes = TimePart(_minutes, "minutes", 59);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { hours, colon, minutes, clock }
        };
        var chip = WidgetChrome.Field(row);
        chip.Padding = new Thickness(10, 0, 8, 0);
        chip.HorizontalAlignment = HorizontalAlignment.Right;
        chip.MinWidth = 108;
        return chip;
    }

    private Border TimePart(TextBox box, string part, int max)
    {
        var host = new Border
        {
            Child = box,
            Background = Brushes.Transparent,
            MinWidth = 22
        };
        MouseWheelEventHandler wheel = (_, e) =>
        {
            if (_engine.Armed)
            {
                return;
            }

            NudgePart(part, e.Delta > 0 ? 1 : -1, max);
            e.Handled = true;
        };
        host.PreviewMouseWheel += wheel;
        host.MouseWheel += wheel;
        box.PreviewMouseWheel += wheel;
        box.MouseWheel += wheel;
        host.MouseEnter += (_, _) =>
        {
            if (!_engine.Armed)
            {
                box.Focus();
            }
        };
        _wheelFields.Add((host, part, max));
        return host;
    }

    private static int ReadPart(TextBox box) =>
        int.TryParse(box.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static TextBox FieldBox() => new()
    {
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        Padding = new Thickness(8, 0, 8, 0),
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        MaxLength = AlarmSettings.MaxLabelLength,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBox DigitBox()
    {
        var box = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = WidgetFonts.Ui,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            MaxLength = 2,
            Width = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        InputMethod.SetIsInputMethodEnabled(box, false);
        return box;
    }

    private static Grid FormRow(string caption, UIElement field, out TextBlock label)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        label = WidgetCopy.Secondary();
        label.Text = caption;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(field, 1);
        row.Children.Add(label);
        row.Children.Add(field);
        return row;
    }

    private static Border ActionButton(TextBlock label, uint fill, Action click)
    {
        var border = new Border
        {
            MinWidth = 88,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 0, 16, 0),
            Background = TokenBrushes.From(fill),
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


    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
}