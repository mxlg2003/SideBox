using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class StopwatchWidget : UserControl, IWidgetOpenable, IKeepAliveWhileRunning, ILocalizable, IWidgetDetachable
{
    private const uint Accent = 0xFF0078D4;
    private const uint QuietFill = 0x14000000;

    private readonly TextBlock _tileTime = WidgetCopy.Primary();
    private readonly TextBlock _tileStatus = WidgetCopy.Secondary();
    private readonly TextBlock _panelTime = WidgetCopy.Primary();
    private readonly TextBlock _panelStatus = WidgetCopy.Secondary();
    private readonly TextBlock _primaryLabel = WidgetCopy.Primary();
    private readonly TextBlock _resetLabel = WidgetCopy.Primary();
    private TextBlock _flyoutTitle = null!;
    private readonly Border _tile;
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private TimeSpan _elapsed;
    private DateTimeOffset? _startedAt;
    private bool _running;

    public StopwatchWidget()
    {
        _tileTime.FontSize = 17;
        _tileTime.TextAlignment = TextAlignment.Center;
        _tileTime.HorizontalAlignment = HorizontalAlignment.Stretch;
        _tileStatus.Margin = new Thickness(0);
        _tileStatus.TextAlignment = TextAlignment.Center;
        _tileStatus.HorizontalAlignment = HorizontalAlignment.Stretch;
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
        _resetLabel.Text = StopwatchFormat.Reset;
        _resetLabel.FontSize = 14;
        _resetLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _resetLabel.VerticalAlignment = VerticalAlignment.Center;

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _tileTime, _tileStatus }
        };
        _tile = WidgetChrome.Wrap(copy, 72);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        _flyoutTitle = WidgetCopy.Secondary();
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Stopwatch);
        _flyoutTitle.FontSize = 12;
        _flyoutTitle.VerticalAlignment = VerticalAlignment.Center;
        var close = new TextBlock
        {
            Text = "\u00D7",
            FontSize = 18,
            Opacity = 0.45,
            Padding = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_flyoutTitle);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        var primary = ActionButton(_primaryLabel, Accent, Toggle);
        var reset = ActionButton(_resetLabel, QuietFill, ResetElapsed);
        WidgetChrome.MarkQuiet(reset);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { primary, reset }
        };

        var panel = WidgetChrome.Flyout(new StackPanel
        {
            Children =
            {
                header,
                _panelTime,
                _panelStatus,
                actions
            }
        }, 260);
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
        close.MouseLeftButtonUp += (_, e) =>
        {
            _popup.IsOpen = false;
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
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => Render();
        Loaded += (_, _) =>
        {
            Render();
            if (_running)
            {
                _timer.Start();
            }
        };
        Unloaded += (_, _) =>
        {
            if (!_running)
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

    public bool IsAlive => _running;

    public void Detach()
    {
        _timer.Stop();
        _popup.IsOpen = false;
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Stopwatch);
        _resetLabel.Text = StopwatchFormat.Reset;
        Render();
    }

    public void Open()
    {
        Render();
        _popup.IsOpen = true;
    }

    private void Toggle()
    {
        if (_running)
        {
            _elapsed = Displayed();
            _startedAt = null;
            _running = false;
            _timer.Stop();
        }
        else
        {
            _startedAt = DateTimeOffset.UtcNow;
            _running = true;
            _timer.Start();
        }

        Render();
    }

    private void ResetElapsed()
    {
        _elapsed = TimeSpan.Zero;
        _startedAt = null;
        _running = false;
        _timer.Stop();
        Render();
    }

    private TimeSpan Displayed()
    {
        if (!_running || _startedAt is not DateTimeOffset start)
        {
            return _elapsed;
        }

        return _elapsed + (DateTimeOffset.UtcNow - start);
    }

    private void Render()
    {
        var elapsed = Displayed();
        var time = StopwatchFormat.Time(elapsed);
        var status = StopwatchFormat.Status(_running, elapsed);
        _tileTime.Text = time;
        _panelTime.Text = time;
        _tileStatus.Text = status;
        _panelStatus.Text = status;
        _primaryLabel.Text = StopwatchFormat.PrimaryAction(_running, elapsed);
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
}
