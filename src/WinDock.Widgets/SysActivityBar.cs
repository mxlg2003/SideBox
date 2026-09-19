using System.Windows;
using System.Windows.Media;

namespace WinDock.Widgets;

public sealed class SysActivityBar : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(SysActivityBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(SysActivityBar),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Brush),
        typeof(SysActivityBar),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public SysActivityBar()
    {
        Height = 6;
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
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

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var radius = h / 2;
        var bounds = new Rect(0, 0, w, h);
        dc.DrawRoundedRectangle(Track ?? Brushes.Gray, null, bounds, radius, radius);
        var fillW = w * Math.Clamp(Progress, 0, 1);
        if (fillW <= 0.5)
        {
            return;
        }

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, fillW, h)));
        dc.DrawRoundedRectangle(Fill ?? Brushes.Black, null, bounds, radius, radius);
        dc.Pop();
    }
}
