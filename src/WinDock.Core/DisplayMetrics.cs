namespace WinDock.Core;

public static class DisplayMetrics
{
    public const double DefaultDpi = 96;

    public static double ScaleFromDpi(double dpi) => Math.Max(1, dpi) / DefaultDpi;

    public static double PixelsToDip(double pixels, double dpi) =>
        pixels * DefaultDpi / Math.Max(1, dpi);

    public static RectLogical PixelRectToDip(int x, int y, int width, int height, double dpi) =>
        new(PixelsToDip(x, dpi), PixelsToDip(y, dpi), PixelsToDip(width, dpi), PixelsToDip(height, dpi));
}
