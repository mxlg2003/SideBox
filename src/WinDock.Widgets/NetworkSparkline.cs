using System.Windows;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class NetworkSparkline : FrameworkElement
{
    public static readonly DependencyProperty ReceiveStrokeProperty = DependencyProperty.Register(
        nameof(ReceiveStroke),
        typeof(Brush),
        typeof(NetworkSparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SendStrokeProperty = DependencyProperty.Register(
        nameof(SendStroke),
        typeof(Brush),
        typeof(NetworkSparkline),
        new FrameworkPropertyMetadata(Brushes.Magenta, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(NetworkSparkline),
        new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender));

    private IReadOnlyList<double> _receive = [];
    private IReadOnlyList<double> _send = [];
    private bool _showReceive = true;
    private bool _showSend = true;

    public NetworkSparkline()
    {
        SnapsToDevicePixels = false;
        IsHitTestVisible = false;
    }

    public Brush ReceiveStroke
    {
        get => (Brush)GetValue(ReceiveStrokeProperty);
        set => SetValue(ReceiveStrokeProperty, value);
    }

    public Brush SendStroke
    {
        get => (Brush)GetValue(SendStrokeProperty);
        set => SetValue(SendStrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public void Render(IReadOnlyList<double> receive, IReadOnlyList<double> send, bool showReceive, bool showSend)
    {
        _receive = receive;
        _send = send;
        _showReceive = showReceive;
        _showSend = showSend;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 2 || h <= 2)
        {
            return;
        }

        var max = 1.0;
        if (_showReceive)
        {
            max = Math.Max(max, Max(_receive));
        }

        if (_showSend)
        {
            max = Math.Max(max, Max(_send));
        }

        if (_showReceive)
        {
            DrawSeries(dc, _receive, ReceiveStroke, StrokeThickness, false, max);
        }

        if (_showSend)
        {
            DrawSeries(dc, _send, SendStroke, StrokeThickness, true, max);
        }
    }

    private void DrawSeries(DrawingContext dc, IReadOnlyList<double> values, Brush? stroke, double thickness, bool dashed, double max)
    {
        if (values.Count == 0)
        {
            return;
        }

        var w = ActualWidth;
        var h = ActualHeight;
        var pad = 1.5;
        var plotW = Math.Max(1, w - (pad * 2));
        var plotH = Math.Max(1, h - (pad * 2));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var x = pad + (values.Count == 1 ? plotW / 2 : i * plotW / (values.Count - 1));
                var y = pad + plotH - (Math.Clamp(values[i], 0, max) / max * plotH);
                if (i == 0)
                {
                    ctx.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    ctx.LineTo(new Point(x, y), true, true);
                }
            }
        }

        geo.Freeze();
        var pen = new Pen(stroke ?? Brushes.Gray, Math.Max(1, thickness))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
            DashStyle = dashed ? DashStyles.Dot : DashStyles.Solid
        };
        dc.DrawGeometry(null, pen, geo);
    }

    private static double Max(IReadOnlyList<double> values)
    {
        var max = 0.0;
        foreach (var value in values)
        {
            max = Math.Max(max, value);
        }

        return max;
    }
}
