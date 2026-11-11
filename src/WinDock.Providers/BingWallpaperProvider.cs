using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class BingWallpaperProvider : IBingWallpaperProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly string _backupFile;
    private readonly string _autoFile;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly object _gate = new();
    private readonly System.Threading.Timer _autoTimer;
    private BingWallpaperSnapshot _current = new();
    private DateTimeOffset _catalogFetchedAt;
    private bool _autoApplyDaily;
    private string? _lastAppliedKey;
    private bool _disposed;

    public BingWallpaperProvider(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        var parent = Path.GetDirectoryName(cacheDirectory) ?? cacheDirectory;
        _backupFile = Path.Combine(parent, "wallpaper-backup.json");
        _autoFile = Path.Combine(parent, "wallpaper-auto.json");
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{ProductInfo.Name}/1.0 (Windows; Bing wallpaper)");
        LoadAutoState();
        _autoTimer = new System.Threading.Timer(
            _ => _ = CheckAndAutoApplyAsync(CancellationToken.None),
            null,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(30));
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

    public Task SelectIndexAsync(int index, CancellationToken cancellationToken = default) =>
        WithLock(() => SelectIndexCore(index, cancellationToken), cancellationToken);

    public string? GetLocalPath(BingWallpaperImage? image)
    {
        if (image is null)
        {
            return null;
        }

        var uhdPath = Path.Combine(_cacheDirectory, BingWallpaperArchive.FileStem(image) + "-uhd.jpg");
        if (File.Exists(uhdPath))
        {
            return uhdPath;
        }

        var stdPath = Path.Combine(_cacheDirectory, BingWallpaperArchive.FileStem(image) + ".jpg");
        if (File.Exists(stdPath))
        {
            return stdPath;
        }

        return null;
    }

    public bool AutoApplyDaily
    {
        get
        {
            lock (_gate)
            {
                return _autoApplyDaily;
            }
        }
        set
        {
            var changed = false;
            lock (_gate)
            {
                if (_autoApplyDaily != value)
                {
                    _autoApplyDaily = value;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveAutoState();
                if (value)
                {
                    _ = CheckAndAutoApplyAsync(CancellationToken.None);
                }
            }
        }
    }

    public string? LastAppliedKey
    {
        get
        {
            lock (_gate)
            {
                return _lastAppliedKey;
            }
        }
        set
        {
            lock (_gate)
            {
                _lastAppliedKey = value;
            }
            SaveAutoState();
        }
    }

    public Task<bool> CheckAndAutoApplyAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => CheckAndAutoApplyCore(cancellationToken), cancellationToken);

    private async Task<bool> CheckAndAutoApplyCore(CancellationToken cancellationToken)
    {
        if (!AutoApplyDaily)
        {
            return false;
        }

        if (Current.Images.Count == 0)
        {
            await RefreshCore(force: false, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = Current;
        if (snapshot.Images.Count == 0 || snapshot.Images[0] is not { } todayImage)
        {
            return false;
        }

        if (string.Equals(LastAppliedKey, todayImage.Key, StringComparison.Ordinal)
            || string.Equals(snapshot.AppliedKey, todayImage.Key, StringComparison.Ordinal))
        {
            return false;
        }

        var todayStem = BingWallpaperArchive.FileStem(todayImage);
        if (Win32DesktopWallpaper.TryCapture(out var currentBackup)
            && !string.IsNullOrWhiteSpace(currentBackup.Wallpaper)
            && currentBackup.Wallpaper.Contains(todayStem, StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
            {
                _lastAppliedKey = todayImage.Key;
            }
            SaveAutoState();
            return true;
        }

        var ok = await ApplyCore(today: true, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            lock (_gate)
            {
                _lastAppliedKey = todayImage.Key;
            }
            SaveAutoState();
        }

        return ok;
    }

    public Task<bool> TryApplyCurrentAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => ApplyCore(today: false, cancellationToken), cancellationToken);

    public Task<bool> TryApplyTodayAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => ApplyCore(today: true, cancellationToken), cancellationToken);

    public bool CanRestorePreviousWallpaper => TryReadBackup(out var backup) && File.Exists(backup.Wallpaper);

    public Task<bool> TryRestorePreviousAsync(CancellationToken cancellationToken = default) =>
        WithLock(RestoreCore, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _autoTimer.Dispose();
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
                Publish(new BingWallpaperSnapshot { Error = UiText.NoBingImage, AppliedKey = snapshot.AppliedKey });
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
            PrefetchCatalog(images);

            if (AutoApplyDaily)
            {
                _ = CheckAndAutoApplyCore(cancellationToken);
            }
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
                Error = UiText.BingLoadFailed
            });
        }
    }

    private void PrefetchCatalog(IReadOnlyList<BingWallpaperImage> images)
    {
        _ = Task.Run(async () =>
        {
            foreach (var img in images)
            {
                try
                {
                    var stdPath = Path.Combine(_cacheDirectory, BingWallpaperArchive.FileStem(img) + ".jpg");
                    if (!File.Exists(stdPath))
                    {
                        await EnsureFileAsync(img.ImageUrl, stdPath, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                }
            }

            Publish(Current);
        });
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

    private async Task SelectIndexCore(int index, CancellationToken cancellationToken)
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

        var clamped = Math.Clamp(index, 0, snapshot.Images.Count - 1);
        if (clamped == snapshot.SelectedIndex && !string.IsNullOrWhiteSpace(snapshot.LocalPath))
        {
            return;
        }

        var next = new BingWallpaperSnapshot
        {
            Images = snapshot.Images,
            SelectedIndex = clamped,
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
                Publish(Failed(snapshot, UiText.DownloadFailed));
                return false;
            }

            Win32DesktopWallpaper.TryCapture(out var previous);
            if (!Win32DesktopWallpaper.TrySet(local))
            {
                Publish(Failed(snapshot with { LocalPath = local }, UiText.ApplyFailed));
                return false;
            }

            TryWriteBackup(previous);

            if (snapshot.Selected is not null)
            {
                lock (_gate)
                {
                    _lastAppliedKey = snapshot.Selected.Key;
                }
                SaveAutoState();
            }

            Publish(new BingWallpaperSnapshot
            {
                Images = snapshot.Images,
                SelectedIndex = snapshot.SelectedIndex,
                LocalPath = local,
                AppliedKey = snapshot.Selected?.Key
            });
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            Publish(Failed(Current, UiText.ApplyFailed));
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
                Error = UiText.DownloadFailed
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
            TrimCache(path);
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

    private Task<bool> RestoreCore()
    {
        if (!TryReadBackup(out var backup) || !Win32DesktopWallpaper.TryRestore(backup))
        {
            return Task.FromResult(false);
        }

        TryDelete(_backupFile);
        return Task.FromResult(true);
    }

    private bool TryReadBackup(out WallpaperBackup backup)
    {
        backup = new WallpaperBackup("", "10", "0");
        try
        {
            if (!File.Exists(_backupFile))
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<WallpaperBackup>(File.ReadAllText(_backupFile));
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Wallpaper))
            {
                return false;
            }

            backup = parsed;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool IsCachedBingWallpaper(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var cache = Path.GetFullPath(_cacheDirectory);
            return full.StartsWith(cache, StringComparison.OrdinalIgnoreCase)
                   || full.Contains("bing-wallpaper", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void TryWriteBackup(WallpaperBackup backup)
    {
        if (string.IsNullOrWhiteSpace(backup.Wallpaper))
        {
            return;
        }

        if (IsCachedBingWallpaper(backup.Wallpaper) && File.Exists(_backupFile))
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_backupFile);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_backupFile, JsonSerializer.Serialize(backup));
        }
        catch (Exception)
        {
        }
    }

    private void LoadAutoState()
    {
        try
        {
            if (!File.Exists(_autoFile))
            {
                return;
            }

            var json = File.ReadAllText(_autoFile);
            var state = JsonSerializer.Deserialize<AutoWallpaperState>(json);
            if (state is not null)
            {
                _autoApplyDaily = state.AutoApplyDaily;
                _lastAppliedKey = state.LastAppliedKey;
            }
        }
        catch (Exception)
        {
        }
    }

    private void SaveAutoState()
    {
        try
        {
            var dir = Path.GetDirectoryName(_autoFile);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AutoWallpaperState state;
            lock (_gate)
            {
                state = new AutoWallpaperState(_autoApplyDaily, _lastAppliedKey);
            }

            File.WriteAllText(_autoFile, JsonSerializer.Serialize(state));
        }
        catch (Exception)
        {
        }
    }

    private void TrimCache(string keepPath)
    {
        try
        {
            var files = new DirectoryInfo(_cacheDirectory)
                .GetFiles("*.jpg")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            foreach (var extra in files.Skip(24))
            {
                if (!string.Equals(extra.FullName, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(extra.FullName);
                }
            }
        }
        catch (Exception)
        {
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

internal sealed record WallpaperBackup(string Wallpaper, string Style, string Tile);

internal sealed record AutoWallpaperState(bool AutoApplyDaily, string? LastAppliedKey);

internal static class Win32DesktopWallpaper
{
    private const int SpiSetDeskWallpaper = 20;
    private const int SpiGetDeskWallpaper = 0x0073;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendChange = 0x02;

    public static bool TryCapture(out WallpaperBackup backup)
    {
        backup = new WallpaperBackup("", "10", "0");
        try
        {
            var buffer = new StringBuilder(520);
            SystemParametersInfo(SpiGetDeskWallpaper, buffer.Capacity, buffer, 0);
            var path = buffer.ToString();
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var style = key?.GetValue("WallpaperStyle")?.ToString() ?? "10";
            var tile = key?.GetValue("TileWallpaper")?.ToString() ?? "0";
            backup = new WallpaperBackup(path, style, tile);
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryRestore(WallpaperBackup backup)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backup.Wallpaper) || !File.Exists(backup.Wallpaper))
            {
                return false;
            }

            var full = Path.GetFullPath(backup.Wallpaper);
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
            {
                key?.SetValue("WallpaperStyle", string.IsNullOrWhiteSpace(backup.Style) ? "10" : backup.Style);
                key?.SetValue("TileWallpaper", string.IsNullOrWhiteSpace(backup.Tile) ? "0" : backup.Tile);
            }

            return SystemParametersInfo(SpiSetDeskWallpaper, 0, full, SpifUpdateIniFile | SpifSendChange);
        }
        catch (Exception)
        {
            return false;
        }
    }

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);
}
