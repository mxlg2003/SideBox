using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class StockChart : FrameworkElement
{
    private IReadOnlyList<StockPoint> _points = [];
    private int _hover = -1;
    private int _anchor = -1;
    private bool _dragging;
    private bool _pressing;
    private Point _press;

    public StockChart()
    {
        SnapsToDevicePixels = false;
        Cursor = Cursors.Cross;
    }

    private bool _spark;
    private bool _isLight;

    public bool Spark
    {
        get => _spark;
        set
        {
            _spark = value;
            IsHitTestVisible = !value;
            Cursor = value ? Cursors.Arrow : Cursors.Cross;
            InvalidateVisual();
        }
    }

    public bool IsLight
    {
        get => _isLight;
        set
        {
            _isLight = value;
            InvalidateVisual();
        }
    }

    public bool Rising { get; set; } = true;

    public bool ShowPreviousClose { get; set; }

    public double? PreviousClose { get; set; }

    public bool Intraday { get; set; } = true;

    public IReadOnlyList<StockPoint> Points
    {
        get => _points;
        set
        {
            _points = value ?? [];
            _hover = -1;
            _anchor = -1;
            _dragging = false;
            _pressing = false;
            InvalidateVisual();
        }
    }

    public void InvalidateChart() => InvalidateVisual();

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (Spark)
        {
            return null;
        }

        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Spark || _points.Count == 0)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_pressing && !_dragging && Math.Abs(position.X - _press.X) >= 4)
        {
            _dragging = true;
        }

        var index = IndexAt(position.X);
        if (index == _hover && !_dragging)
        {
            return;
        }

        _hover = index;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Spark || _points.Count == 0)
        {
            return;
        }

        _press = e.GetPosition(this);
        _anchor = IndexAt(_press.X);
        _hover = _anchor;
        _pressing = _anchor >= 0;
        _dragging = false;
        if (_pressing)
        {
            CaptureMouse();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_pressing && !_dragging)
        {
            return;
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _pressing = false;
        _dragging = false;
        _anchor = -1;
        _hover = IndexAt(e.GetPosition(this).X);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_dragging || _pressing || _hover < 0)
        {
            return;
        }

        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 4 || height < 4 || _points.Count == 0)
        {
            return;
        }

        var plot = Spark
            ? new Rect(1.5, 2, Math.Max(1, width - 3), Math.Max(1, height - 4))
            : new Rect(6, 10, Math.Max(1, width - 54), Math.Max(1, height - 20));
        var (low, high) = Bounds();
        if (!Spark)
        {
            DrawDots(dc, plot);
            DrawGrid(dc, plot, low, high);
        }

        if (ShowPreviousClose && PreviousClose is double previous && previous >= low && previous <= high)
        {
            var y = Y(previous, plot, low, high);
            var dash = Pen(Brush(DockTokens.StockFlat, 160), 1, DashStyles.Dash);
            dc.DrawLine(dash, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        var tone = Rising ? DockTokens.StockUp : DockTokens.StockDown;
        var line = Pen(Brush(tone, 255), Spark ? 1.5 : 1.8);
        var geo = new StreamGeometry();
        var fill = new StreamGeometry();
        using (var stroke = geo.Open())
        using (var area = fill.Open())
        {
            for (var i = 0; i < _points.Count; i++)
            {
                var x = X(i, plot);
                var y = Y(_points[i].Price, plot, low, high);
                if (i == 0)
                {
                    stroke.BeginFigure(new Point(x, y), false, false);
                    area.BeginFigure(new Point(x, y), true, true);
                }
                else
                {
                    stroke.LineTo(new Point(x, y), true, true);
                    area.LineTo(new Point(x, y), true, true);
                }
            }

            if (_points.Count >= 2)
            {
                area.LineTo(new Point(X(_points.Count - 1, plot), plot.Bottom), true, false);
                area.LineTo(new Point(X(0, plot), plot.Bottom), true, false);
            }
        }

        geo.Freeze();
        fill.Freeze();
        if (!Spark && _points.Count >= 2)
        {
            dc.DrawGeometry(Brush(tone, 36), null, fill);
        }

        dc.DrawGeometry(null, line, geo);

        if (!Spark)
        {
            DrawAxis(dc, plot, low, high);
            DrawHover(dc, plot, low, high, tone);
        }
    }

    private void DrawDots(DrawingContext dc, Rect plot)
    {
        var brush = Faint(IsLight ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke, 180);
        for (var y = plot.Top; y <= plot.Bottom; y += 12)
        {
            for (var x = plot.Left; x <= plot.Right; x += 12)
            {
                dc.DrawEllipse(brush, null, new Point(x, y), 0.7, 0.7);
            }
        }
    }

    private void DrawGrid(DrawingContext dc, Rect plot, double low, double high)
    {
        var pen = Pen(Faint(IsLight ? DockTokens.Light.Text : DockTokens.Dark.Text, 70), 1, DashStyles.Dot);
        foreach (var tick in new[] { high, (high + low) / 2, low })
        {
            var y = Y(tick, plot, low, high);
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
    }

    private void DrawAxis(DrawingContext dc, Rect plot, double low, double high)
    {
        var brush = Faint(IsLight ? DockTokens.Light.Text : DockTokens.Dark.Text, 150);
        DrawLabel(dc, StockFormat.Price(high), plot.Right + 6, Y(high, plot, low, high) - 6, brush);
        DrawLabel(dc, StockFormat.Price(low), plot.Right + 6, Y(low, plot, low, high) - 6, brush);
    }

    private void DrawHover(DrawingContext dc, Rect plot, double low, double high, uint tone)
    {
        if (_hover < 0 || _hover >= _points.Count)
        {
            return;
        }

        var point = _points[_hover];
        var x = X(_hover, plot);
        var y = Y(point.Price, plot, low, high);
        var hair = Pen(Faint(tone, 180), 1);
        dc.DrawLine(hair, new Point(x, plot.Top), new Point(x, plot.Bottom));
        dc.DrawEllipse(Brush(tone, 255), Pen(Brush(IsLight ? DockTokens.Light.Text : DockTokens.Dark.Text, 255), 1), new Point(x, y), 3.2, 3.2);

        if (_dragging && _anchor >= 0 && _anchor < _points.Count)
        {
            var from = _points[_anchor].Price;
            var percent = Math.Abs(from) < 0.0000001 ? 0 : (point.Price - from) / from * 100;
            DrawChip(dc, StockFormat.Price(point.Price) + "  " + StockFormat.Percent(percent), x, y, plot);
            var ax = X(_anchor, plot);
            var ay = Y(from, plot, low, high);
            dc.DrawEllipse(Brush(DockTokens.StockFlat, 255), null, new Point(ax, ay), 2.6, 2.6);
            return;
        }

        var when = Intraday
            ? point.Time.ToString("HH:mm", CultureInfo.CurrentCulture)
            : point.Time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        DrawChip(dc, StockFormat.Price(point.Price) + "  " + when, x, y, plot);
    }

    private void DrawChip(DrawingContext dc, string text, double x, double y, Rect plot)
    {
        var fg = Brush(IsLight ? DockTokens.Dark.Text : DockTokens.Dark.Text, 255);
        var bg = Brush(IsLight ? DockTokens.Light.Text : DockTokens.Dark.TileFill, 230);
        var formatted = Text(text, fg, 11, FontWeights.SemiBold);
        var width = formatted.Width + 12;
        var height = formatted.Height + 6;
        var left = Math.Clamp(x - width / 2, plot.Left, Math.Max(plot.Left, plot.Right - width));
        var top = y - height - 8;
        if (top < plot.Top)
        {
            top = y + 8;
        }

        var rect = new Rect(left, top, width, height);
        dc.DrawRoundedRectangle(bg, null, rect, 6, 6);
        dc.DrawText(formatted, new Point(rect.X + 6, rect.Y + 3));
    }

    private void DrawLabel(DrawingContext dc, string text, double x, double y, Brush brush)
    {
        var formatted = Text(text, brush, 10, FontWeights.Normal);
        var top = Math.Clamp(y, 0, Math.Max(0, ActualHeight - formatted.Height));
        dc.DrawText(formatted, new Point(x, top));
    }

    private (double Low, double High) Bounds()
    {
        var low = _points[0].Price;
        var high = low;
        foreach (var point in _points)
        {
            low = Math.Min(low, point.Price);
            high = Math.Max(high, point.Price);
        }

        if (ShowPreviousClose && PreviousClose is double previous && previous > 0)
        {
            if (previous > high && previous < high * 1.15)
            {
                high = previous;
            }

            if (previous < low && previous > low * 0.85)
            {
                low = previous;
            }
        }

        if (high <= low)
        {
            high = low + Math.Max(Math.Abs(low) * 0.01, 0.01);
        }

        var pad = (high - low) * 0.08;
        return (low - pad, high + pad);
    }

    private double X(int index, Rect plot)
    {
        if (_points.Count <= 1)
        {
            return plot.Left + plot.Width / 2;
        }

        return plot.Left + index * plot.Width / (_points.Count - 1);
    }

    private static double Y(double price, Rect plot, double low, double high)
    {
        var span = Math.Max(high - low, 0.0001);
        return plot.Bottom - (price - low) / span * plot.Height;
    }

    private int IndexAt(double x)
    {
        if (_points.Count == 0)
        {
            return -1;
        }

        var plot = new Rect(6, 10, Math.Max(1, ActualWidth - 54), Math.Max(1, ActualHeight - 20));
        var t = (x - plot.Left) / plot.Width;
        var index = (int)Math.Round(Math.Clamp(t, 0, 1) * (_points.Count - 1));
        return Math.Clamp(index, 0, _points.Count - 1);
    }

    private FormattedText Text(string value, Brush brush, double size, FontWeight weight) => new(
        value,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(WidgetFonts.Ui, FontStyles.Normal, weight, FontStretches.Normal),
        size,
        brush,
        Dip());

    private double Dip()
    {
        try
        {
            return VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static Brush Brush(uint argb, byte alpha)
    {
        var (_, r, g, b) = DockTokens.Split(argb);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush Faint(uint argb, byte alpha) => Brush(argb, alpha);

    private static Pen Pen(Brush brush, double thickness, DashStyle? dash = null)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
            DashStyle = dash ?? DashStyles.Solid
        };
        pen.Freeze();
        return pen;
    }
}
