using System.Windows;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Brush),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClockwiseProperty = DependencyProperty.Register(
        nameof(Clockwise),
        typeof(bool),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(ProgressRing),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public ProgressRing(double size)
    {
        Width = size;
        Height = size;
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public bool Clockwise
    {
        get => (bool)GetValue(ClockwiseProperty);
        set => SetValue(ClockwiseProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var cx = w / 2;
        var cy = h / 2;
        var thickness = StrokeThickness > 0 ? StrokeThickness : Math.Max(2.4, w * 0.12);
        var radius = Math.Min(cx, cy) - thickness / 2;
        var center = new Point(cx, cy);
        var track = new Pen(Track ?? Brushes.Gray, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawEllipse(null, track, center, radius, radius);

        var sweep = Math.Clamp(Progress, 0, 1) * 360.0;
        if (sweep <= 0.4)
        {
            return;
        }

        var fill = new Pen(Fill ?? Brushes.LimeGreen, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (sweep >= 359.5)
        {
            dc.DrawEllipse(null, fill, center, radius, radius);
            return;
        }

        var start = -90.0 * Math.PI / 180.0;
        var end = (Clockwise ? -90.0 + sweep : -90.0 - sweep) * Math.PI / 180.0;
        var p1 = new Point(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
        var p2 = new Point(cx + radius * Math.Cos(end), cy + radius * Math.Sin(end));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1, false, false);
            ctx.ArcTo(
                p2,
                new Size(radius, radius),
                0,
                sweep > 180,
                Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                true,
                false);
        }

        geo.Freeze();
        dc.DrawGeometry(null, fill, geo);
    }
}
