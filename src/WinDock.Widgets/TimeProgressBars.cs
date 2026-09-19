using System.Windows;
using System.Windows.Media;

namespace WinDock.Widgets;

public sealed class TimeProgressBars : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(TimeProgressBars),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(TimeProgressBars),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Brush),
        typeof(TimeProgressBars),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
        nameof(BarCount),
        typeof(int),
        typeof(TimeProgressBars),
        new FrameworkPropertyMetadata(36, FrameworkPropertyMetadataOptions.AffectsRender));

    public TimeProgressBars()
    {
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public int BarCount
    {
        get => (int)GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        var count = Math.Max(4, BarCount);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var slot = w / count;
        var barW = Math.Clamp(slot * 0.48, 1.2, 4.0);
        var radius = Math.Min(barW, h) / 2;
        var progress = Math.Clamp(Progress, 0, 1);
        var fill = Fill ?? Brushes.Black;
        var track = Track ?? Brushes.LightGray;
        for (var i = 0; i < count; i++)
        {
            var x = i * slot + (slot - barW) / 2;
            var brush = (i + 0.5) / count <= progress ? fill : track;
            dc.DrawRoundedRectangle(brush, null, new Rect(x, 0, barW, h), radius, radius);
        }
    }
}
