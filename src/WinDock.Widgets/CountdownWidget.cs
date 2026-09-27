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

public sealed class CountdownWidget : UserControl, IWidgetOpenable, ICountdownConfigurable, IKeepAliveWhileRunning, ILocalizable, IWidgetDetachable
{
    private const uint Accent = 0xFF0078D4;
    private const string GlyphTimer = "\uE916";

    private readonly CountdownEngine _engine = new();
    private readonly TextBlock _tileTime = WidgetCopy.Primary();
    private readonly TextBlock _tileStatus = WidgetCopy.Secondary();
    private readonly TextBlock _panelTime = WidgetCopy.Primary();
    private readonly TextBlock _primaryLabel = WidgetCopy.Primary();
    private readonly TextBlock _saved = WidgetCopy.Secondary();
    private readonly TextBlock _glyph = WidgetChrome.Glyph(GlyphTimer);
    private readonly TextBox _name = FieldBox();
    private readonly TextBox _hours = DigitBox();
    private readonly TextBox _minutes = DigitBox();
    private readonly TextBox _seconds = DigitBox();
    private readonly StackPanel _presetList = new();
    private readonly StackPanel _presetSection;
    private readonly Border _tile;
    private readonly Border _reset;
    private readonly Border _primary;
    private readonly List<UIElement> _spinners = [];
    private readonly List<(UIElement Element, string Part, int Max)> _wheelFields = [];
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _savedTimer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private readonly DispatcherTimer _persistTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _repeatTimer = new();
    private readonly Dictionary<string, TextBlock> _partCaptions = new(StringComparer.Ordinal);
    private TextBlock _flyoutTitle = null!;
    private TextBlock _presetsCaption = null!;
    private TextBlock _nameLabel = null!;
    private TextBlock _resetLabel = null!;
    private Action? _repeatClick;
    private bool _applying;
    private bool _suppressFields;

    public CountdownWidget()
    {
        _tileTime.FontSize = 17;
        _tileStatus.Margin = new Thickness(0);
        _panelTime.FontSize = 38;
        _panelTime.FontWeight = FontWeights.SemiBold;
        _panelTime.HorizontalAlignment = HorizontalAlignment.Center;
        _panelTime.Margin = new Thickness(0, 4, 0, 12);
        _primaryLabel.Tag = "tint";
        _primaryLabel.FontSize = 14;
        _primaryLabel.Foreground = Brushes.White;
        _primaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _primaryLabel.VerticalAlignment = VerticalAlignment.Center;
        _saved.Text = CountdownFormat.PresetSaved;
        _saved.HorizontalAlignment = HorizontalAlignment.Center;
        _saved.Margin = new Thickness(0, 10, 0, 0);
        _saved.Visibility = Visibility.Collapsed;
        _glyph.FontSize = 16;
        _glyph.Margin = new Thickness(0, 0, 8, 0);

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tileTime, _tileStatus }
        };
        _tile = WidgetChrome.Wrap(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _glyph, copy }
        }, 0);

        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Countdown));
        var close = HeaderGlyph("\u00D7", ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, close);

        _presetsCaption = Caption(CountdownFormat.Presets);
        _presetSection = new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 8),
            Children =
            {
                _presetsCaption,
                _presetList
            }
        };

        var nameRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _nameLabel = Caption(CountdownFormat.TimerName);
        _nameLabel.VerticalAlignment = VerticalAlignment.Center;
        _nameLabel.Margin = new Thickness(0, 0, 12, 0);
        var nameHost = WidgetChrome.Field(_name, 34);
        Grid.SetColumn(nameHost, 1);
        nameRow.Children.Add(_nameLabel);
        nameRow.Children.Add(nameHost);

        var parts = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        parts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        parts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        parts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var hours = DigitCard(_hours, CountdownFormat.Hours, "hours", CountdownSettings.MaxHours);
        var minutes = DigitCard(_minutes, CountdownFormat.Minutes, "minutes", 59);
        var seconds = DigitCard(_seconds, CountdownFormat.Seconds, "seconds", 59);
        minutes.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetColumn(minutes, 1);
        Grid.SetColumn(seconds, 2);
        parts.Children.Add(hours);
        parts.Children.Add(minutes);
        parts.Children.Add(seconds);

        _primary = ActionButton(_primaryLabel, Accent, Toggle);
        _resetLabel = ResetLabel();
        _reset = ActionButton(_resetLabel, DockTokens.QuietFill, Reset);
        WidgetChrome.MarkQuiet(_reset);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _primary, _reset }
        };

        var panel = WidgetChrome.Flyout(new StackPanel
        {
            Children =
            {
                header,
                _panelTime,
                _presetSection,
                nameRow,
                parts,
                actions,
                _saved
            }
        }, 320);
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
            PaintPresets();
        };

        BindField(_name, commitName: true);
        BindField(_hours, part: "hours", max: CountdownSettings.MaxHours);
        BindField(_minutes, part: "minutes", max: 59);
        BindField(_seconds, part: "seconds", max: 59);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Pulse();
        _savedTimer.Tick += (_, _) =>
        {
            _savedTimer.Stop();
            _saved.Visibility = Visibility.Collapsed;
        };
        _persistTimer.Tick += (_, _) =>
        {
            _persistTimer.Stop();
            Persist();
        };
        _repeatTimer.Tick += (_, _) =>
        {
            _repeatClick?.Invoke();
            _repeatTimer.Interval = TimeSpan.FromMilliseconds(80);
        };
        Loaded += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            WidgetFlyout.Wheel += OnFlyoutWheel;
            Render();
            if (_engine.Running)
            {
                _timer.Start();
            }
        };
        Unloaded += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            if (!_engine.Running)
            {
                _timer.Stop();
            }

            FlushPersist();
            StopRepeat();
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        PaintFields();
        PaintPresets();
        Render();
    }

    public event EventHandler<CountdownSettings>? SettingsChanged;

    public bool IsAlive => _engine.Running;

    public void Detach()
    {
        _timer.Stop();
        _popup.IsOpen = false;
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Countdown);
        _presetsCaption.Text = CountdownFormat.Presets;
        _nameLabel.Text = CountdownFormat.TimerName;
        if (_partCaptions.TryGetValue("hours", out var hours)) hours.Text = CountdownFormat.Hours;
        if (_partCaptions.TryGetValue("minutes", out var minutes)) minutes.Text = CountdownFormat.Minutes;
        if (_partCaptions.TryGetValue("seconds", out var seconds)) seconds.Text = CountdownFormat.Seconds;
        _resetLabel.Text = StopwatchFormat.Reset;
        _saved.Text = CountdownFormat.PresetSaved;
        Render();
    }

    public void ApplySettings(CountdownSettings settings)
    {
        _applying = true;
        _engine.ApplySettings(settings, Now);
        PaintFields();
        PaintPresets();
        Render();
        _applying = false;
    }

    public void Open()
    {
        Render();
        PaintFields();
        PaintPresets();
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

        SystemSounds.Asterisk.Play();
        UserAlerts.Show(
            BuiltInWidgets.DisplayName(BuiltInWidgets.Countdown),
            CountdownFormat.FinishedMessage(_engine.Settings.Name));
        if (IsVisible && IsLoaded)
        {
            Open();
        }
    }

    private void Toggle()
    {
        if (_engine.Running)
        {
            _engine.Pause(Now);
            _timer.Stop();
            Render();
            return;
        }

        CommitFields();
        if (!_engine.CanStart(Now))
        {
            Render();
            return;
        }

        var saved = _engine.UpsertPreset();
        _engine.Start(Now);
        _timer.Start();
        if (saved)
        {
            ShowSaved();
            PaintPresets();
        }

        FlushPersist();
        Persist();
        Render();
    }

    private void Reset()
    {
        _engine.Reset();
        _timer.Stop();
        Render();
        PaintFields();
    }

    private void ChoosePreset(string id)
    {
        if (_engine.ApplyPreset(id, Now))
        {
            _timer.Stop();
            Persist();
            PaintFields();
            PaintPresets();
            Render();
        }
    }

    private void DeletePreset(string id)
    {
        if (_engine.RemovePreset(id))
        {
            Persist();
            PaintPresets();
        }
    }

    private void CommitFields()
    {
        if (_suppressFields)
        {
            return;
        }

        _engine.SetName(_name.Text);
        _engine.SetDuration(ReadPart(_hours), ReadPart(_minutes), ReadPart(_seconds), Now);
    }

    private void OnFlyoutWheel(object? sender, FlyoutWheelEventArgs e)
    {
        if (!_popup.IsOpen || _engine.Running)
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

        if (_popup.Child is UIElement root && ContainsScreen(root, e.Screen))
        {
            NudgeFocused(e.Delta);
        }
    }

    private void NudgeFocused(int delta)
    {
        var step = delta > 0 ? 1 : -1;
        if (ReferenceEquals(Keyboard.FocusedElement, _hours))
        {
            NudgePart("hours", step, CountdownSettings.MaxHours);
        }
        else if (ReferenceEquals(Keyboard.FocusedElement, _minutes))
        {
            NudgePart("minutes", step, 59);
        }
        else if (ReferenceEquals(Keyboard.FocusedElement, _seconds))
        {
            NudgePart("seconds", step, 59);
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
        if (_engine.Running || _suppressFields)
        {
            return;
        }

        CommitFields();
        var hours = _engine.Settings.Hours;
        var minutes = _engine.Settings.Minutes;
        var seconds = _engine.Settings.Seconds;
        switch (part)
        {
            case "hours":
                hours = CountdownSettings.Wrap(hours + delta, max);
                break;
            case "minutes":
                minutes = CountdownSettings.Wrap(minutes + delta, max);
                break;
            default:
                seconds = CountdownSettings.Wrap(seconds + delta, max);
                break;
        }

        _engine.SetDuration(hours, minutes, seconds, Now);
        SchedulePersist();
        PaintFields();
        Render();
    }

    private void SchedulePersist()
    {
        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void BeginRepeat(Action click)
    {
        StopRepeat();
        _repeatClick = click;
        _repeatTimer.Interval = TimeSpan.FromMilliseconds(400);
        _repeatTimer.Start();
    }

    private void StopRepeat()
    {
        _repeatTimer.Stop();
        _repeatClick = null;
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
        var time = CountdownFormat.Time(_engine.Running || paused ? remaining : _engine.Duration);
        _tileTime.Text = time;
        _panelTime.Text = CountdownFormat.Time(remaining);
        _panelTime.Visibility = _engine.Running || paused ? Visibility.Visible : Visibility.Collapsed;
        _tileStatus.Text = CountdownFormat.TileStatus(_engine.Running, paused);
        _primaryLabel.Text = StopwatchFormat.PrimaryAction(_engine.Running, paused ? TimeSpan.FromSeconds(1) : TimeSpan.Zero);
        _reset.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        var canStart = _engine.CanStart(now);
        _primary.Opacity = _engine.Running || paused || canStart ? 1 : 0.45;
        _primary.IsHitTestVisible = _engine.Running || paused || canStart;
        _hours.IsReadOnly = _engine.Running;
        _minutes.IsReadOnly = _engine.Running;
        _seconds.IsReadOnly = _engine.Running;
        _name.IsReadOnly = _engine.Running;
        foreach (var spinner in _spinners)
        {
            spinner.IsHitTestVisible = !_engine.Running;
            spinner.Opacity = _engine.Running ? 0.28 : 1;
        }
    }

    private void PaintFields()
    {
        _suppressFields = true;
        _name.Text = _engine.Settings.Name;
        _hours.Text = _engine.Settings.Hours.ToString(CultureInfo.InvariantCulture);
        _minutes.Text = _engine.Settings.Minutes.ToString(CultureInfo.InvariantCulture);
        _seconds.Text = _engine.Settings.Seconds.ToString(CultureInfo.InvariantCulture);
        _suppressFields = false;
    }

    private void PaintPresets()
    {
        _presetList.Children.Clear();
        foreach (var preset in _engine.Settings.Presets)
        {
            _presetList.Children.Add(PresetRow(preset));
        }

        _presetSection.Visibility = _engine.Settings.Presets.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ShowSaved()
    {
        _saved.Visibility = Visibility.Visible;
        _savedTimer.Stop();
        _savedTimer.Start();
    }

    private void ClosePopup()
    {
        _popup.IsOpen = false;
    }

    private void OnPopupClosed()
    {
        var before = _engine.Settings;
        CommitFields();
        if (!CountdownSettings.Same(before, _engine.Settings))
        {
            FlushPersist();
            Persist();
        }
        else
        {
            FlushPersist();
        }

        StopRepeat();
    }

    private Border PresetRow(CountdownPreset preset)
    {
        var name = WidgetCopy.Primary();
        name.FontSize = 13;
        name.FontWeight = FontWeights.Normal;
        name.VerticalAlignment = VerticalAlignment.Center;
        WidgetCopy.WrapUserText(name, preset.Name, DockTokens.FlyoutUserCopyMaxWidth);
        var time = WidgetCopy.Secondary();
        time.Text = CountdownFormat.Time(CountdownSettings.ToDuration(preset.Hours, preset.Minutes, preset.Seconds));
        time.Margin = new Thickness(8, 0, 4, 0);
        time.VerticalAlignment = VerticalAlignment.Center;
        var remove = HeaderGlyph("\u00D7", () => DeletePreset(preset.Id), 16);
        remove.Padding = new Thickness(8, 4, 4, 4);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(name);
        Grid.SetColumn(time, 1);
        row.Children.Add(time);
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);
        var border = new Border
        {
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Background = TokenBrushes.From(DockTokens.QuietFill),
            Padding = new Thickness(10, 6, 6, 6),
            Margin = new Thickness(0, 6, 0, 0),
            Cursor = Cursors.Hand,
            Child = row
        };
        WidgetChrome.MarkQuiet(border);
        border.MouseLeftButtonUp += (_, e) =>
        {
            if (e.Handled)
            {
                return;
            }

            ChoosePreset(preset.Id);
            e.Handled = true;
        };
        return border;
    }

    private void BindField(TextBox box, bool commitName = false, string? part = null, int max = 0)
    {
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (commitName)
            {
                return;
            }

            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsDigit))
            {
                e.CancelCommand();
            }
        });
        if (!commitName)
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
            if (_suppressFields || _engine.Running)
            {
                return;
            }

            var before = _engine.Settings;
            CommitFields();
            if (!CountdownSettings.Same(before, _engine.Settings))
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
            if (!commitName)
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

    private static int ReadPart(TextBox box) =>
        int.TryParse(box.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static TextBox FieldBox() => new()
    {
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        Padding = new Thickness(8, 0, 8, 0),
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        MaxLength = CountdownSettings.MaxNameLength,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBox DigitBox()
    {
        var box = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = WidgetFonts.Ui,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            MaxLength = 2,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 8, 0, 4)
        };
        InputMethod.SetIsInputMethodEnabled(box, false);
        return box;
    }

    private Border DigitCard(TextBox box, string caption, string part, int max)
    {
        var spinner = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 6, 0)
        };
        spinner.Children.Add(SpinnerButton("\uE70E", () => NudgePart(part, 1, max)));
        spinner.Children.Add(SpinnerButton("\uE70D", () => NudgePart(part, -1, max)));
        Panel.SetZIndex(spinner, 1);
        _spinners.Add(spinner);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(box);
        Grid.SetColumn(spinner, 1);
        row.Children.Add(spinner);

        var label = WidgetCopy.Secondary();
        label.Text = caption;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Margin = new Thickness(0, 0, 0, 6);
        _partCaptions[part] = label;
        var card = new Border
        {
            Tag = WidgetChrome.FieldTag,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Background = TokenBrushes.From(DockTokens.QuietFill),
            MinHeight = 56,
            Child = new StackPanel
            {
                Children = { row, label }
            }
        };
        WidgetChrome.ApplyTheme(card, false);
        MouseWheelEventHandler wheel = (_, e) =>
        {
            if (_engine.Running)
            {
                return;
            }

            NudgePart(part, e.Delta > 0 ? 1 : -1, max);
            e.Handled = true;
        };
        card.MouseEnter += (_, _) =>
        {
            if (!_engine.Running)
            {
                box.Focus();
            }
        };
        card.PreviewMouseWheel += wheel;
        card.MouseWheel += wheel;
        box.PreviewMouseWheel += wheel;
        box.MouseWheel += wheel;
        _wheelFields.Add((card, part, max));
        return card;
    }

    private Border SpinnerButton(string glyph, Action click)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 8,
            Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        var border = new Border
        {
            Width = 18,
            Height = 14,
            Background = TokenBrushes.From(DockTokens.QuietFill),
            Cursor = Cursors.Hand,
            Child = icon
        };
        WidgetChrome.MarkQuietOverlay(border);
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (_engine.Running)
            {
                return;
            }

            click();
            BeginRepeat(click);
            e.Handled = true;
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            StopRepeat();
            e.Handled = true;
        };
        border.MouseLeave += (_, _) =>
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                StopRepeat();
            }
        };
        return border;
    }

    private static TextBlock Caption(string text)
    {
        var label = WidgetCopy.Secondary();
        label.Text = text;
        label.Margin = new Thickness(0);
        return label;
    }

    private static Border ActionButton(TextBlock label, uint fill, Action click)
    {
        var border = new Border
        {
            MinWidth = 88,
            Height = 34,
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

    private static TextBlock HeaderGlyph(string glyph, Action click, double size = 13)
    {
        var block = new TextBlock
        {
            Text = glyph,
            FontFamily = glyph == "\u00D7" ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = size,
            Opacity = 0.45,
            Padding = new Thickness(8, 4, 0, 4),
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

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
}
