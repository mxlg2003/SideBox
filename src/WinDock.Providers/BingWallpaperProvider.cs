using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class BingWallpaperProvider : IBingWallpaperProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly object _gate = new();
    private BingWallpaperSnapshot _current = new();
    private DateTimeOffset _catalogFetchedAt;
    private bool _disposed;

    public BingWallpaperProvider(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WinDock/1.0 (Windows; Bing wallpaper)");
    }

    public BingWallpaperSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => RefreshCore(force: false, cancellationToken), cancellationToken);

    public Task SelectRelativeAsync(int delta, CancellationToken cancellationToken = default) =>
        WithLock(() => SelectCore(delta, cancellationToken), cancellationToken);

    public Task<bool> TryApplyCurrentAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => ApplyCore(today: false, cancellationToken), cancellationToken);

    public Task<bool> TryApplyTodayAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => ApplyCore(today: true, cancellationToken), cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
        _mutex.Dispose();
    }

    private async Task WithLock(Func<Task> action, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<bool> WithLock(Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task RefreshCore(bool force, CancellationToken cancellationToken)
    {
        var snapshot = Current;
        if (!force && snapshot.Images.Count > 0 && DateTimeOffset.UtcNow - _catalogFetchedAt < TimeSpan.FromHours(1))
        {
            await EnsureLocalAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return;
        }

        Publish(Busy(snapshot));
        try
        {
            var market = BingWallpaperArchive.ResolveMarket(CultureInfo.CurrentUICulture.Name);
            var json = await _http.GetStringAsync(BingWallpaperArchive.CatalogUrl(market), cancellationToken).ConfigureAwait(false);
            var images = BingWallpaperArchive.Parse(json);
            if (images.Count == 0)
            {
                Publish(new BingWallpaperSnapshot { Error = "暂无图片", AppliedKey = snapshot.AppliedKey });
                return;
            }

            _catalogFetchedAt = DateTimeOffset.UtcNow;
            var next = new BingWallpaperSnapshot
            {
                Images = images,
                SelectedIndex = 0,
                AppliedKey = snapshot.AppliedKey,
                IsBusy = true
            };
            Publish(next);
            await EnsureLocalAsync(next, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            Publish(new BingWallpaperSnapshot
            {
                Images = snapshot.Images,
                SelectedIndex = snapshot.SelectedIndex,
                LocalPath = snapshot.LocalPath,
                AppliedKey = snapshot.AppliedKey,
                Error = "无法加载"
            });
        }
    }

    private async Task SelectCore(int delta, CancellationToken cancellationToken)
    {
        if (Current.Images.Count == 0)
        {
            await RefreshCore(force: false, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = Current;
        if (snapshot.Images.Count == 0)
        {
            return;
        }

        var next = new BingWallpaperSnapshot
        {
            Images = snapshot.Images,
            SelectedIndex = BingWallpaperArchive.WrapIndex(snapshot.SelectedIndex + delta, snapshot.Images.Count),
            AppliedKey = snapshot.AppliedKey,
            IsBusy = true
        };
        Publish(next);
        await EnsureLocalAsync(next, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ApplyCore(bool today, CancellationToken cancellationToken)
    {
        if (Current.Images.Count == 0 || today)
        {
            await RefreshCore(force: false, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = Current;
        if (snapshot.Images.Count == 0)
        {
            return false;
        }

        if (today && snapshot.SelectedIndex != 0)
        {
            snapshot = new BingWallpaperSnapshot
            {
                Images = snapshot.Images,
                SelectedIndex = 0,
                AppliedKey = snapshot.AppliedKey,
                IsBusy = true
            };
            Publish(snapshot);
        }

        try
        {
            var local = await EnsureLocalAsync(snapshot, cancellationToken).ConfigureAwait(false);
            snapshot = Current;
            if (string.IsNullOrWhiteSpace(local) || snapshot.Selected is null)
            {
                Publish(Failed(snapshot, "下载失败"));
                return false;
            }

            if (!Win32DesktopWallpaper.TrySet(local))
            {
                Publish(Failed(snapshot with { LocalPath = local }, "未能应用"));
                return false;
            }

            Publish(new BingWallpaperSnapshot
            {
                Images = snapshot.Images,
                SelectedIndex = snapshot.SelectedIndex,
                LocalPath = local,
                AppliedKey = snapshot.Selected.Key
            });
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            Publish(Failed(Current, "未能应用"));
            return false;
        }
    }

    private async Task<string?> EnsureLocalAsync(BingWallpaperSnapshot snapshot, CancellationToken cancellationToken)
    {
        var image = snapshot.Selected;
        if (image is null)
        {
            Publish(Busy(snapshot, busy: false));
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        var uhdPath = Path.Combine(_cacheDirectory, BingWallpaperArchive.FileStem(image) + "-uhd.jpg");
        var stdPath = Path.Combine(_cacheDirectory, BingWallpaperArchive.FileStem(image) + ".jpg");
        var local = await EnsureFileAsync(image.WallpaperUrl, uhdPath, cancellationToken).ConfigureAwait(false)
                    ?? await EnsureFileAsync(image.ImageUrl, stdPath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(local))
        {
            Publish(new BingWallpaperSnapshot
            {
                Images = snapshot.Images,
                SelectedIndex = snapshot.SelectedIndex,
                AppliedKey = snapshot.AppliedKey,
                Error = "下载失败"
            });
            return null;
        }

        Publish(new BingWallpaperSnapshot
        {
            Images = snapshot.Images,
            SelectedIndex = snapshot.SelectedIndex,
            LocalPath = local,
            AppliedKey = snapshot.AppliedKey
        });
        return local;
    }

    private async Task<string?> EnsureFileAsync(string url, string path, CancellationToken cancellationToken)
    {
        if (IsUsableImage(path))
        {
            return path;
        }

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var temp = path + ".tmp";
            await using (var output = File.Create(temp))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            if (!IsUsableImage(temp))
            {
                TryDelete(temp);
                return null;
            }

            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            TryDelete(path + ".tmp");
            return null;
        }
    }

    private static bool IsUsableImage(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length >= 80_000;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
        }
    }

    private static BingWallpaperSnapshot Busy(BingWallpaperSnapshot source, bool busy = true) => new()
    {
        Images = source.Images,
        SelectedIndex = source.SelectedIndex,
        LocalPath = source.LocalPath,
        AppliedKey = source.AppliedKey,
        IsBusy = busy
    };

    private static BingWallpaperSnapshot Failed(BingWallpaperSnapshot source, string error) => new()
    {
        Images = source.Images,
        SelectedIndex = source.SelectedIndex,
        LocalPath = source.LocalPath,
        AppliedKey = source.AppliedKey,
        Error = error
    };

    private void Publish(BingWallpaperSnapshot next)
    {
        lock (_gate)
        {
            _current = next;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal static class Win32DesktopWallpaper
{
    private const int SpiSetDeskWallpaper = 20;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendChange = 0x02;

    public static bool TrySet(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                return false;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
            {
                key?.SetValue("WallpaperStyle", "10");
                key?.SetValue("TileWallpaper", "0");
            }

            return SystemParametersInfo(SpiSetDeskWallpaper, 0, full, SpifUpdateIniFile | SpifSendChange);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);
}
