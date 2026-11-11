using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WinDock.Core;

#if SHELL
namespace WinDock.Shell;
#else
namespace WinDock.Widgets;
#endif

/// <summary>Phosphor icon drawn from <see cref="PhosphorIcon"/> path data. 256 design grid, nonzero fill.</summary>
#if SHELL
internal sealed class PhosphorGlyph : FrameworkElement
#else
public sealed class PhosphorGlyph : FrameworkElement
#endif
{
    private const double Design = 256;

    private static readonly Dictionary<string, Geometry> Cache = new(StringComparer.Ordinal);

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(string),
        typeof(PhosphorGlyph),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(PhosphorGlyph),
        new FrameworkPropertyMetadata(
            Brushes.Black,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background),
        typeof(Brush),
        typeof(PhosphorGlyph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding),
        typeof(Thickness),
        typeof(PhosphorGlyph),
        new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(PhosphorGlyph),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public PhosphorGlyph()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    public PhosphorGlyph(string icon, double size)
        : this()
    {
        Icon = icon;
        IconSize = size;
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    protected override Size MeasureOverride(Size available)
    {
        var icon = !double.IsNaN(IconSize) && IconSize > 0 ? IconSize : 16;
        return new Size(Padding.Left + icon + Padding.Right, Padding.Top + icon + Padding.Bottom);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (Background is not null)
        {
            var radius = Math.Min(DockTokens.TileCornerRadius, Math.Min(width, height) / 2);
            dc.DrawRoundedRectangle(Background, null, new Rect(0, 0, width, height), radius, radius);
        }
        var geometry = GeometryFor(Icon);
        if (geometry is null)
        {
            return;
        }

        var content = new Rect(
            Padding.Left,
            Padding.Top,
            Math.Max(0, width - Padding.Left - Padding.Right),
            Math.Max(0, height - Padding.Top - Padding.Bottom));
        if (content.Width <= 0 || content.Height <= 0)
        {
            return;
        }

        var side = !double.IsNaN(IconSize) && IconSize > 0 ? IconSize : Math.Min(content.Width, content.Height);
        side = Math.Min(side, Math.Min(content.Width, content.Height));
        var dest = new Rect(
            content.X + (content.Width - side) / 2,
            content.Y + (content.Height - side) / 2,
            side,
            side);
        var scale = side / Design;
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, dest.X, dest.Y));
        dc.DrawGeometry(Foreground ?? Brushes.Black, null, geometry);
        dc.Pop();
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var point = hitTestParameters.HitPoint;
        if (point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight)
        {
            return new PointHitTestResult(this, point);
        }

        return null!;
    }

    private static Geometry? GeometryFor(string? icon)
    {
        if (string.IsNullOrEmpty(icon))
        {
            return null;
        }

        if (Cache.TryGetValue(icon, out var cached))
        {
            return cached;
        }

        if (!PhosphorIcon.TryGet(icon, out var data) || data.Length == 0)
        {
            return null;
        }

        var geometry = Geometry.Parse("F1 " + data);
        geometry.Freeze();
        Cache[icon] = geometry;
        return geometry;
    }
}
