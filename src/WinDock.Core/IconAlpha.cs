namespace WinDock.Core;

public static class IconAlpha
{
    public const byte MinOpaque = 16;

    public static (int X, int Y, int Width, int Height) OpaqueBounds(
        ReadOnlySpan<byte> bgra,
        int width,
        int height,
        int stride,
        byte minOpaque = MinOpaque)
    {
        if (width <= 0 || height <= 0 || stride < width * 4)
        {
            return (0, 0, 0, 0);
        }

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                if (bgra[row + (x * 4) + 3] <= minOpaque)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        return maxX < minX ? (0, 0, 0, 0) : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
