using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class FocusTimerWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IFocusTimerConfigurable, IKeepAliveWhileRunning, IWidgetThemeAware, ILocalizable, IWidgetDetachable, IDockEdgeAware
{
    private const uint Accent = 0xFF0078D4;
    private const uint QuietFill = 0x14000000;
    private const double RingSize = 26;

    private readonly FocusTimerEngine _engine = new();
    private readonly TextBlock _tileTime = WidgetCopy.Primary();
    private readonly TextBlock _tileStatus = WidgetCopy.Secondary();
    private readonly TextBlock _panelTime = WidgetCopy.Primary();
    private readonly TextBlock _panelStatus = WidgetCopy.Secondary();
    private readonly TextBlock _primaryLabel = WidgetCopy.Primary();
    private readonly Rectangle _pauseLeft = PauseBar();
    private readonly Rectangle _pauseRight = PauseBar();
    private readonly StackPanel _pauseMark;
    private readonly ProgressRing _ring = new(RingSize);
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private readonly Border _resetMenu;
    private readonly StackPanel _presets = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 12, 0, 0)
    };
    private readonly StackPanel _swatches = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 10, 0, 8)
    };
    private readonly List<(int Minutes, Border Chip, TextBlock Label)> _presetChips = [];
    private readonly List<(string Id, PhosphorGlyph Check)> _marks = [];
    private readonly Dictionary<string, TextBlock> _stepValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _stepNames = new(StringComparer.Ordinal);
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _colorCaption = null!;
    private TextBlock _resetPeriodLabel = null!;
    private TextBlock _resetSessionLabel = null!;
    private TextBlock _resetLabel = null!;
    private readonly CheckBox _notify = new()
    {
        Content = UiText.NotifyWhenDone,
        IsChecked = true,
        Margin = new Thickness(0, 16, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
    private bool _applying;
    private bool _side;
    private readonly Grid _ringHost;
    private readonly StackPanel _dockCopy;
    private readonly StackPanel _dock;

    public FocusTimerWidget()
    {
        _tileTime.FontSize = 16;
        _tileStatus.Margin = new Thickness(0);
        _panelTime.FontSize = 38;
        _panelTime.FontWeight = FontWeights.SemiBold;
        _panelTime.HorizontalAlignment = HorizontalAlignment.Center;
        _panelStatus.FontSize = 13;
        _panelStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _panelStatus.Margin = new Thickness(0, 2, 0, 16);
        _primaryLabel.Tag = "tint";
        _primaryLabel.FontSize = 14;
        _primaryLabel.Foreground = Brushes.White;
        _primaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _primaryLabel.VerticalAlignment = VerticalAlignment.Center;
        _pauseMark = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Children = { _pauseLeft, _pauseRight }
        };
        _ring.VerticalAlignment = VerticalAlignment.Center;
        ApplyTheme(false);

        _ringHost = new Grid
        {
            Width = RingSize,
            Height = RingSize,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _ring, _pauseMark }
        };
        _dockCopy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tileTime, _tileStatus }
        };
        _dock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _ringHost, _dockCopy }
        };
        _tile = WidgetChrome.Wrap(_dock, 0);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        _resetMenu = BuildResetMenu();
        _editorPage = BuildEditor();
        _customizePage = BuildCustomize();
        _customizePage.Visibility = Visibility.Collapsed;
        var body = new Grid();
        body.Children.Add(_editorPage);
        body.Children.Add(_customizePage);
        _panel = WidgetChrome.Flyout(body, 300);
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = _panel
        };
        WidgetFlyout.Attach(_popup);
        _popup.Closed += (_, _) => ShowEditor();
        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)_panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        _notify.Checked += (_, _) => SetNotify(true);
        _notify.Unchecked += (_, _) => SetNotify(false);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Pulse();
        Loaded += (_, _) =>
        {
            Render();
            if (_engine.Running)
            {
                _timer.Start();
            }
        };
        Unloaded += (_, _) =>
        {
            if (!_engine.Running)
            {
                _timer.Stop();
            }

            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        Render();
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<FocusTimerSettings>? SettingsChanged;

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        _dock.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _dock.HorizontalAlignment = HorizontalAlignment.Center;
        _ringHost.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _ringHost.HorizontalAlignment = HorizontalAlignment.Center;
        _dockCopy.HorizontalAlignment = HorizontalAlignment.Center;
        _tileTime.HorizontalAlignment = HorizontalAlignment.Center;
        _tileTime.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _tileStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _tileStatus.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        WidgetChrome.FitEdge(_tile, _side, 0);
    }

    public void ApplyTheme(bool isLight)
    {
        _ring.Track = TokenBrushes.From(isLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke);
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.FocusTimer);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _colorCaption.Text = UiText.FocusColor;
        _notify.Content = UiText.NotifyWhenDone;
        _resetPeriodLabel.Text = UiText.ResetTimer;
        _resetSessionLabel.Text = UiText.BackToSession1;
        _resetLabel.Text = StopwatchFormat.Reset;
        if (_stepNames.TryGetValue("focus", out var focus)) focus.Text = UiText.FocusLabel;
        if (_stepNames.TryGetValue("short", out var shortBreak)) shortBreak.Text = UiText.ShortBreak;
        if (_stepNames.TryGetValue("long", out var longBreak)) longBreak.Text = UiText.LongBreak;
        if (_stepNames.TryGetValue("every", out var every)) every.Text = UiText.LongBreakEvery;
        foreach (var (minutes, _, label) in _presetChips)
        {
            label.Text = FocusTimerFormat.Preset(minutes);
        }

        PaintCustomize();
        Render();
    }

    public void ApplySettings(FocusTimerSettings settings)
    {
        _applying = true;
        _engine.ApplySettings(settings, Now);
        PaintCustomize();
        Render();
        _applying = false;
    }

    public bool IsAlive => _engine.Running;

    public void Detach()
    {
        _timer.Stop();
        _popup.IsOpen = false;
    }

    public void Open()
    {
        ShowEditor();
        Render();
        _popup.IsOpen = true;
    }

    public void OpenCustomize()
    {
        ShowCustomize();
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

        FocusTimerEvents.Notify(_engine.Running, _engine.Phase);

        if (_engine.Settings.NotifyWhenFinished)
        {
            SystemSounds.Asterisk.Play();
            UserAlerts.Show(UiText.WidgetFocusTimer, _engine.Phase == FocusTimerPhase.Focus ? UiText.FocusStarted : UiText.PeriodEnded);
            if (IsVisible && IsLoaded)
            {
                Open();
            }
        }
    }

    private void Toggle()
    {
        HideResetMenu();
        _engine.Toggle(Now);
        if (_engine.Running)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        FocusTimerEvents.Notify(_engine.Running, _engine.Phase);
        Render();
    }

    private void ChoosePreset(int minutes)
    {
        HideResetMenu();
        _engine.SetFocusMinutes(minutes, Now);
        _timer.Stop();
        FocusTimerEvents.Notify(_engine.Running, _engine.Phase);
        Persist();
        Render();
    }

    private void ResetPeriod()
    {
        HideResetMenu();
        _engine.ResetPeriod();
        _timer.Stop();
        FocusTimerEvents.Notify(false, _engine.Phase);
        Render();
    }

    private void ResetToSession1()
    {
        HideResetMenu();
        _engine.ResetToSession1();
        _timer.Stop();
        FocusTimerEvents.Notify(false, _engine.Phase);
        Render();
    }

    private void SetNotify(bool notify)
    {
        if (_applying)
        {
            return;
        }

        _engine.ApplySettings(_engine.Settings with { NotifyWhenFinished = notify }, Now);
        Persist();
    }

    private void ChooseColor(string color)
    {
        _engine.ApplySettings(_engine.Settings with { Color = color }, Now);
        Persist();
        PaintCustomize();
        Render();
    }

    private void Nudge(string key, int delta)
    {
        var settings = _engine.Settings;
        settings = key switch
        {
            "focus" => settings with { FocusMinutes = settings.FocusMinutes + delta },
            "short" => settings with { ShortBreakMinutes = settings.ShortBreakMinutes + delta },
            "long" => settings with { LongBreakMinutes = settings.LongBreakMinutes + delta },
            _ => settings with { LongBreakEvery = settings.LongBreakEvery + delta }
        };
        _engine.ApplySettings(settings, Now);
        Persist();
        PaintCustomize();
        Render();
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
        if (_engine.Running)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        var remaining = _engine.Remaining(now);
        var paused = _engine.IsPaused(now);
        var time = FocusTimerFormat.Time(remaining);
        var color = TokenBrushes.From(FocusTimerColors.Fill(_engine.Settings.Color));
        _tileTime.Text = time;
        _panelTime.Text = time;
        _tileStatus.Text = FocusTimerFormat.TileStatus(_engine.Running, paused, _engine.Phase);
        _panelStatus.Text = FocusTimerFormat.SessionLine(_engine.Phase, _engine.Session, _engine.Settings.LongBreakEvery);
        _primaryLabel.Text = _engine.Running
            ? StopwatchFormat.Pause
            : paused ? StopwatchFormat.Resume : StopwatchFormat.Start;
        _ring.Fill = color;
        _ring.Progress = _engine.Running || paused ? _engine.RemainingFraction(now) : 0;
        _pauseLeft.Fill = color;
        _pauseRight.Fill = color;
        _pauseMark.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (minutes, chip, label) in _presetChips)
        {
            var selected = minutes == _engine.Settings.FocusMinutes;
            label.Tag = selected ? "tint" : null;
            if (selected)
            {
                WidgetChrome.ClearQuiet(chip, Accent);
                label.Foreground = Brushes.White;
            }
            else
            {
                WidgetChrome.MarkQuiet(chip);
            }
        }
    }

    private void ShowEditor()
    {
        HideResetMenu();
        _popup.StaysOpen = false;
        _editorPage.Visibility = Visibility.Visible;
        _customizePage.Visibility = Visibility.Collapsed;
    }

    private void ShowCustomize()
    {
        HideResetMenu();
        _popup.StaysOpen = true;
        PaintCustomize();
        _editorPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Visible;
    }

    private void HideResetMenu() => _resetMenu.Visibility = Visibility.Collapsed;

    private void ClosePopup()
    {
        _popup.StaysOpen = false;
        _popup.IsOpen = false;
    }

    private void PaintCustomize()
    {
        _applying = true;
        _notify.IsChecked = _engine.Settings.NotifyWhenFinished;
        SetStep("focus", FocusTimerFormat.StepValue(_engine.Settings.FocusMinutes, UiText.MinutesUnit));
        SetStep("short", FocusTimerFormat.StepValue(_engine.Settings.ShortBreakMinutes, UiText.MinutesUnit));
        SetStep("long", FocusTimerFormat.StepValue(_engine.Settings.LongBreakMinutes, UiText.MinutesUnit));
        SetStep("every", FocusTimerFormat.StepValue(_engine.Settings.LongBreakEvery, UiText.SessionsUnit));
        foreach (var (id, check) in _marks)
        {
            check.Visibility = string.Equals(id, _engine.Settings.Color, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _applying = false;
    }

    private void SetStep(string key, string text)
    {
        if (_stepValues.TryGetValue(key, out var label))
        {
            label.Text = text;
        }
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.FocusTimer));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        var primary = ActionButton(_primaryLabel, Accent, Toggle);
        _resetLabel = ResetLabel();
        var reset = ActionButton(_resetLabel, QuietFill, () =>
        {
            _resetMenu.Visibility = _resetMenu.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        });
        WidgetChrome.MarkQuiet(reset);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { primary, reset }
        };
        foreach (var minutes in FocusTimerFormat.FocusPresets)
        {
            _presets.Children.Add(PresetChip(minutes));
        }

        var page = new StackPanel { Children = { header, _panelTime, _panelStatus, actions, _resetMenu, _presets } };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Grid BuildCustomize()
    {
        _backLabel = HeaderLabel(UiText.Back);
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, ShowEditor, 12),
                _backLabel
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        _customizeTitle = HeaderLabel(UiText.Customize);
        _customizeTitle.Margin = new Thickness(8, 0, 0, 0);
        _customizeTitle.FontWeight = FontWeights.SemiBold;
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, _customizeTitle }
        }, close);
        _colorCaption = HeaderLabel(UiText.FocusColor);
        _colorCaption.Opacity = 0.62;
        _colorCaption.Margin = new Thickness(0, 12, 0, 0);
        foreach (var id in FocusTimerColors.All)
        {
            var (dot, check) = Swatch(id);
            _swatches.Children.Add(dot);
            _marks.Add((id, check));
        }

        var page = new StackPanel
        {
            Children =
            {
                header,
                _colorCaption,
                _swatches,
                Stepper("focus", UiText.FocusLabel, () => Nudge("focus", -1), () => Nudge("focus", 1)),
                Stepper("short", UiText.ShortBreak, () => Nudge("short", -1), () => Nudge("short", 1)),
                Stepper("long", UiText.LongBreak, () => Nudge("long", -1), () => Nudge("long", 1)),
                Stepper("every", UiText.LongBreakEvery, () => Nudge("every", -1), () => Nudge("every", 1)),
                _notify
            }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Border BuildResetMenu()
    {
        var reset = MenuRow(UiText.ResetTimer, ResetPeriod, out _resetPeriodLabel);
        var session = MenuRow(UiText.BackToSession1, ResetToSession1, out _resetSessionLabel);
        var menu = new Border
        {
            Visibility = Visibility.Collapsed,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Background = TokenBrushes.From(0x0F000000),
            Margin = new Thickness(24, 8, 24, 0),
            Padding = new Thickness(4),
            Child = new StackPanel { Children = { reset, session } }
        };
        WidgetChrome.MarkQuiet(menu);
        return menu;
    }

    private static Border MenuRow(string text, Action click, out TextBlock label)
    {
        label = WidgetCopy.Primary();
        label.Text = text;
        label.FontSize = 13;
        label.FontWeight = FontWeights.Normal;
        label.Margin = new Thickness(10, 8, 10, 8);
        var border = new Border { Cursor = Cursors.Hand, Child = label };
        border.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return border;
    }

    private Border PresetChip(int minutes)
    {
        var label = WidgetCopy.Primary();
        label.Text = FocusTimerFormat.Preset(minutes);
        label.FontSize = 12;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border
        {
            MinWidth = 58,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Margin = new Thickness(4, 0, 4, 0),
            Background = TokenBrushes.From(QuietFill),
            Cursor = Cursors.Hand,
            Child = label
        };
        WidgetChrome.MarkQuiet(chip);
        chip.MouseLeftButtonUp += (_, e) =>
        {
            ChoosePreset(minutes);
            e.Handled = true;
        };
        _presetChips.Add((minutes, chip, label));
        return chip;
    }

    private Grid Stepper(string key, string title, Action minus, Action plus)
    {
        var name = HeaderLabel(title);
        _stepNames[key] = name;
        var value = WidgetCopy.Primary();
        value.FontSize = 13;
        value.MinWidth = 64;
        value.TextAlignment = TextAlignment.Center;
        value.VerticalAlignment = VerticalAlignment.Center;
        _stepValues[key] = value;
        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(name);
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                RoundButton(PhosphorIcon.Minus, minus),
                value,
                RoundButton(PhosphorIcon.Plus, plus)
            }
        };
        Grid.SetColumn(controls, 1);
        row.Children.Add(controls);
        return row;
    }

    private static Border RoundButton(string glyph, Action click)
    {
        var icon = new PhosphorGlyph(glyph, 11)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var border = new Border
        {
            Width = DockTokens.FlyoutControlHeight,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.FlyoutControlHeight / 2),
            Background = TokenBrushes.From(QuietFill),
            Cursor = Cursors.Hand,
            Child = icon
        };
        WidgetChrome.MarkQuiet(border);
        border.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return border;
    }

    private (Border Dot, PhosphorGlyph Check) Swatch(string id)
    {
        var check = new PhosphorGlyph(PhosphorIcon.Check, 11)
        {
            Tag = "tint",
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        var host = new Grid
        {
            Width = 28,
            Height = 28,
            Children =
            {
                new Ellipse
                {
                    Fill = TokenBrushes.From(FocusTimerColors.Fill(id)),
                    Stroke = TokenBrushes.From(0x33000000),
                    StrokeThickness = 0.6
                },
                check
            }
        };
        var border = new Border
        {
            Width = DockTokens.FlyoutControlHeight,
            Height = DockTokens.FlyoutControlHeight,
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            Child = host
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            ChooseColor(id);
            e.Handled = true;
        };
        return (border, check);
    }

    private static Border ActionButton(TextBlock label, uint fill, Action click)
    {
        var border = new Border
        {
            MinWidth = 88,
            Height = DockTokens.FlyoutControlHeight,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(4, 0, 4, 0),
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

    private static TextBlock ResetLabel()
    {
        var label = WidgetCopy.Primary();
        label.Text = StopwatchFormat.Reset;
        label.FontSize = 14;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
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

    private static Rectangle PauseBar() => new()
    {
        Width = 2.4,
        Height = 8,
        RadiusX = 1,
        RadiusY = 1,
        Margin = new Thickness(0.9, 0, 0.9, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
}
