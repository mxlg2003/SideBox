using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace WinDock.Providers;

internal static class ProcessIconCache
{
    public static string? TrySave(string? imagePath, string cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imagePath.ToLowerInvariant())))[..16];
            var cache = Path.Combine(cacheDirectory, key + "-proc.png");
            if (File.Exists(cache)
                && File.GetLastWriteTimeUtc(cache) >= File.GetLastWriteTimeUtc(imagePath))
            {
                return cache;
            }

            using var icon = Icon.ExtractAssociatedIcon(imagePath);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(cache, ImageFormat.Png);
            return cache;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
