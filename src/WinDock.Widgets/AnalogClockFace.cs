using System.Windows;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class AnalogClockFace : FrameworkElement
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time),
        typeof(DateTime),
        typeof(AnalogClockFace),
        new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(AnalogClockFace),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public AnalogClockFace(double size)
    {
        Width = size;
        Height = size;
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
        Stroke = TokenBrushes.From(DockTokens.Dark.Text);
    }

    public DateTime Time
    {
        get => (DateTime)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var stroke = Stroke ?? Brushes.Black;
        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(cx, cy) - Math.Max(1.1, w * 0.07);
        var facePen = new Pen(stroke, Math.Max(1.25, w * 0.055))
        {
            EndLineCap = PenLineCap.Round
        };
        dc.DrawEllipse(null, facePen, new Point(cx, cy), radius, radius);

        var time = Time;
        var hourAngle = (time.Hour % 12 + time.Minute / 60.0 + time.Second / 3600.0) * 30.0;
        var minuteAngle = time.Minute * 6.0 + time.Second * 0.1;
        DrawHand(dc, stroke, cx, cy, hourAngle, radius * 0.52, Math.Max(1.7, w * 0.085));
        DrawHand(dc, stroke, cx, cy, minuteAngle, radius * 0.76, Math.Max(1.2, w * 0.055));

        var cap = Math.Max(1.8, w * 0.09);
        dc.DrawEllipse(stroke, null, new Point(cx, cy), cap / 2, cap / 2);
    }

    private static void DrawHand(DrawingContext dc, Brush stroke, double cx, double cy, double angle, double length, double thickness)
    {
        var rad = (angle - 90) * Math.PI / 180.0;
        var pen = new Pen(stroke, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new Point(cx, cy), new Point(cx + length * Math.Cos(rad), cy + length * Math.Sin(rad)));
    }
}
