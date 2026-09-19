using System.Globalization;

namespace WinDock.Core;

public static class ByteRate
{
    public static string Format(double bytesPerSecond)
    {
        var parts = Split(bytesPerSecond);
        return $"{parts.Value} {parts.Unit}";
    }

    public static (string Value, string Unit) Split(double bytesPerSecond)
    {
        var magnitude = Math.Abs(bytesPerSecond);
        if (magnitude < 1024)
        {
            return (string.Create(CultureInfo.InvariantCulture, $"{bytesPerSecond:0}"), "B/s");
        }

        if (magnitude < 1024 * 1024)
        {
            return (string.Create(CultureInfo.InvariantCulture, $"{bytesPerSecond / 1024:0.0}"), "KB/s");
        }

        return (string.Create(CultureInfo.InvariantCulture, $"{bytesPerSecond / (1024 * 1024):0.00}"), "MB/s");
    }
}
