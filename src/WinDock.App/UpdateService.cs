using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using WinDock.Core;

namespace WinDock.App;

public static class UpdateService
{
    private static readonly HttpClient Http = UpdateChecker.CreateDefaultClient();

    public static Task<UpdateCheckResult> CheckUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default) =>
        UpdateChecker.CheckUpdateAsync(currentVersion, cancellationToken: cancellationToken);

    public static async Task DownloadAndInstallAsync(
        AppVersionInfo info,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(info.DownloadUrl))
        {
            throw new InvalidOperationException("Download URL is empty.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"SideBox-setup-{info.Version}.exe");

        // Download with progress reporting (try direct first, fallback to mirror if GitHub direct fails)
        var succeeded = false;
        Exception? directException = null;

        try
        {
            await DownloadToFileAsync(info.DownloadUrl, tempPath, progress, cancellationToken).ConfigureAwait(false);
            succeeded = true;
        }
        catch (Exception ex)
        {
            directException = ex;
        }

        // If direct GitHub download failed, attempt with accelerated mirror
        if (!succeeded && info.DownloadUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var mirrorUrl = $"{ProductInfo.DownloadMirrorPrefix.TrimEnd('/')}/{info.DownloadUrl}";
            try
            {
                await DownloadToFileAsync(mirrorUrl, tempPath, progress, cancellationToken).ConfigureAwait(false);
                succeeded = true;
            }
            catch
            {
                // Rethrow original or combined exception
                throw directException ?? new InvalidOperationException(UiText.UpdateDownloadFailed);
            }
        }

        if (!succeeded)
        {
            throw directException ?? new InvalidOperationException(UiText.UpdateDownloadFailed);
        }

        progress?.Report(100);

        // Verify SHA256 if provided
        if (!string.IsNullOrWhiteSpace(info.Sha256))
        {
            await using var checkStream = File.OpenRead(tempPath);
            var hashBytes = await SHA256.HashDataAsync(checkStream, cancellationToken).ConfigureAwait(false);
            var actualHash = Convert.ToHexString(hashBytes);

            if (!string.Equals(actualHash, info.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }

                throw new InvalidOperationException(UiText.UpdateHashMismatch);
            }
        }

        // Launch installer with silent and close-applications switches
        var psi = new ProcessStartInfo
        {
            FileName = tempPath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS",
            UseShellExecute = true
        };
        Process.Start(psi);

        // Gracefully shut down and exit current application so files are completely unlocked
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                Application.Current.Shutdown();
            }
            catch
            {
            }

            Environment.Exit(0);
        });
    }

    private static async Task DownloadToFileAsync(
        string url,
        string destinationPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                var percentage = (int)Math.Clamp((double)totalRead / totalBytes.Value * 100, 0, 100);
                progress?.Report(percentage);
            }
        }
    }
}
