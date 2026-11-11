using System.IO;
using System.Net.Http;
using System.Text.Json;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class WallpaperService : IWallpaperProvider, IDisposable
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly string _settingsFile;
    private readonly string _backupFile;
    private readonly IBingWallpaperProvider? _bing;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly object _gate = new();
    private readonly Timer _rotationTimer;

    private WallpaperSnapshot _current = new();
    private WallpaperSettings _settings = WallpaperSettings.Default;
    private readonly List<WallpaperItem> _recent = [];
    private int _selectedIndex;
    private WallpaperBackup? _backup;
    private bool _disposed;

    public WallpaperService(string cacheDirectory, IBingWallpaperProvider? bing = null)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
        _settingsFile = Path.Combine(_cacheDirectory, "wallpaper-settings.json");
        _backupFile = Path.Combine(_cacheDirectory, "wallpaper-backup.json");
        _bing = bing;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{ProductInfo.Name}/1.0 (Windows; Wallpaper hub)");

        LoadSettingsAndBackup();

        _rotationTimer = new Timer(
            _ => _ = OnRotationTimerTickAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2));
    }

    public WallpaperSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool CanRestoreOriginalWallpaper
    {
        get
        {
            lock (_gate)
            {
                return _backup is not null && !string.IsNullOrWhiteSpace(_backup.Wallpaper) && File.Exists(_backup.Wallpaper);
            }
        }
    }

    public event EventHandler? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => RefreshCoreAsync(cancellationToken), cancellationToken);

    public Task SelectNextAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => SelectNextCoreAsync(forceNew: true, cancellationToken), cancellationToken);

    public Task SelectPreviousAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => SelectPreviousCoreAsync(cancellationToken), cancellationToken);

    public Task SelectIndexAsync(int index, CancellationToken cancellationToken = default) =>
        WithLock(() => SelectIndexCoreAsync(index, cancellationToken), cancellationToken);

    public Task ApplySettingsAsync(WallpaperSettings settings, CancellationToken cancellationToken = default) =>
        WithLock(async () =>
        {
            var oldSource = _settings.Source;
            var oldFolder = _settings.LocalFolder;
            _settings = settings;
            SaveSettings();

            if (oldSource != settings.Source || !string.Equals(oldFolder, settings.LocalFolder, StringComparison.OrdinalIgnoreCase))
            {
                await SelectNextCoreAsync(forceNew: true, cancellationToken);
            }
            else
            {
                PublishCurrent();
            }
        }, cancellationToken);

    public Task<bool> TryApplyCurrentDesktopAsync(CancellationToken cancellationToken = default) =>
        WithLock(() => TryApplyCurrentDesktopCoreAsync(cancellationToken), cancellationToken);

    public Task<bool> TryRestoreOriginalDesktopAsync(CancellationToken cancellationToken = default) =>
        WithLock(async () =>
        {
            if (_backup is null)
            {
                return false;
            }

            var ok = Win32DesktopWallpaper.TryRestore(_backup);
            if (ok)
            {
                _settings = _settings with { LastAppliedKey = null };
                SaveSettings();
                PublishCurrent();
            }
            return ok;
        }, cancellationToken);

    public string? GetLocalPath(WallpaperItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.LocalPath) && File.Exists(item.LocalPath))
        {
            return item.LocalPath;
        }

        var cached = CachedFilePath(item.Id);
        return File.Exists(cached) ? cached : null;
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        if (_recent.Count == 0)
        {
            await SelectNextCoreAsync(forceNew: false, cancellationToken);
        }
        else
        {
            PublishCurrent();
        }
    }

    private async Task SelectNextCoreAsync(bool forceNew, CancellationToken cancellationToken)
    {
        if (!forceNew && _selectedIndex < _recent.Count - 1)
        {
            _selectedIndex++;
            PublishCurrent();
            return;
        }

        PublishBusy(true);

        try
        {
            WallpaperItem? item = null;
            string? error = null;

            switch (_settings.Source)
            {
                case WallpaperSource.Local:
                    item = PickNextLocal();
                    if (item is null)
                    {
                        error = UiText.WallpaperEmptyFolder;
                        item = UnsplashCuratedCatalog.Next(_settings.CurrentId);
                    }
                    break;

                case WallpaperSource.Unsplash:
                    if (!string.IsNullOrWhiteSpace(_settings.UnsplashAccessKey))
                    {
                        try
                        {
                            item = await FetchRandomUnsplashAsync(_settings.UnsplashAccessKey, _settings.UnsplashQuery, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                            item = UnsplashCuratedCatalog.Next(_settings.CurrentId);
                        }
                    }
                    else
                    {
                        error = UiText.WallpaperNoKey;
                        item = UnsplashCuratedCatalog.Next(_settings.CurrentId);
                    }
                    break;

                case WallpaperSource.Bing:
                    item = await PickBingAsync(cancellationToken);
                    item ??= UnsplashCuratedCatalog.Next(_settings.CurrentId);
                    break;

                case WallpaperSource.Curated:
                default:
                    item = UnsplashCuratedCatalog.Next(_settings.CurrentId);
                    break;
            }

            if (item is not null)
            {
                var localFile = await EnsureLocalFileAsync(item, cancellationToken);
                item = item with { LocalPath = localFile };

                _recent.Add(item);
                if (_recent.Count > 16)
                {
                    _recent.RemoveAt(0);
                }
                _selectedIndex = _recent.Count - 1;

                _settings = _settings with
                {
                    CurrentId = item.Id,
                    ChosenOn = DateTime.Now.ToString("yyyy-MM-dd")
                };
                SaveSettings();

                PublishCurrent(error);

                if (_settings.AutoApplyDesktop)
                {
                    await TryApplyCurrentDesktopCoreAsync(cancellationToken);
                }
            }
            else
            {
                PublishCurrent(error ?? "No wallpaper available.");
            }
        }
        catch (Exception ex)
        {
            PublishCurrent(ex.Message);
        }
        finally
        {
            PublishBusy(false);
        }
    }

    private async Task SelectPreviousCoreAsync(CancellationToken cancellationToken)
    {
        if (_selectedIndex > 0)
        {
            _selectedIndex--;
            PublishCurrent();

            var current = Current.Current;
            if (current is not null && _settings.AutoApplyDesktop)
            {
                await TryApplyCurrentDesktopCoreAsync(cancellationToken);
            }
        }
    }

    private async Task SelectIndexCoreAsync(int index, CancellationToken cancellationToken)
    {
        if (index >= 0 && index < _recent.Count)
        {
            _selectedIndex = index;
            PublishCurrent();

            var current = Current.Current;
            if (current is not null && _settings.AutoApplyDesktop)
            {
                await TryApplyCurrentDesktopCoreAsync(cancellationToken);
            }
        }
    }

    private WallpaperItem? PickNextLocal()
    {
        if (string.IsNullOrWhiteSpace(_settings.LocalFolder) || !Directory.Exists(_settings.LocalFolder))
        {
            return null;
        }

        var option = _settings.LocalRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(_settings.LocalFolder, "*.*", option)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0)
        {
            return null;
        }

        var (next, seen) = LocalShufflePicker.PickNext(files, _settings.LocalCurrent, _settings.LocalSeen);
        _settings = _settings with
        {
            LocalCurrent = next,
            LocalSeen = seen,
            LocalChosenOn = DateTime.Now.ToString("yyyy-MM-dd")
        };
        SaveSettings();

        var name = Path.GetFileNameWithoutExtension(next);
        return new WallpaperItem
        {
            Id = $"local:{next}",
            Source = WallpaperSource.Local,
            Url = new Uri(next).AbsoluteUri,
            LocalPath = next,
            Title = name,
            Author = Path.GetFileName(_settings.LocalFolder),
            PageUrl = next,
            DominantColor = "#334155"
        };
    }

    private async Task<WallpaperItem?> FetchRandomUnsplashAsync(string key, string query, CancellationToken cancellationToken)
    {
        var url = new UriBuilder("https://api.unsplash.com/photos/random")
        {
            Query = $"orientation=landscape&content_filter=high{(string.IsNullOrWhiteSpace(query) ? "" : $"&query={Uri.EscapeDataString(query.Trim())}")}"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);
        request.Headers.Add("Authorization", $"Client-ID {key}");
        request.Headers.Add("Accept-Version", "v1");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Unsplash error: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
        var rawUrl = root.GetProperty("urls").GetProperty("raw").GetString() ?? "";
        var sizedUrl = $"{rawUrl}&w=2560&q=80&fm=jpg&fit=crop";

        var user = root.GetProperty("user");
        var authorName = user.GetProperty("name").GetString() ?? "";
        var userHtml = user.GetProperty("links").GetProperty("html").GetString() ?? "https://unsplash.com";

        var photoHtml = root.GetProperty("links").GetProperty("html").GetString() ?? "https://unsplash.com";
        var downloadLoc = root.TryGetProperty("links", out var links) && links.TryGetProperty("download_location", out var dl)
            ? dl.GetString()
            : null;

        var color = root.TryGetProperty("color", out var colorEl) ? colorEl.GetString() ?? "#1e293b" : "#1e293b";

        var item = new WallpaperItem
        {
            Id = $"unsplash:{id}",
            Source = WallpaperSource.Unsplash,
            Url = sizedUrl,
            Title = authorName,
            Author = authorName,
            AuthorUrl = $"{userHtml}?utm_source=windock&utm_medium=referral",
            PageUrl = $"{photoHtml}?utm_source=windock&utm_medium=referral",
            DominantColor = color,
            DownloadLocation = downloadLoc
        };

        if (!string.IsNullOrWhiteSpace(downloadLoc))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var ping = new HttpRequestMessage(HttpMethod.Get, downloadLoc);
                    ping.Headers.Add("Authorization", $"Client-ID {key}");
                    await _http.SendAsync(ping, CancellationToken.None);
                }
                catch { }
            }, CancellationToken.None);
        }

        return item;
    }

    private async Task<WallpaperItem?> PickBingAsync(CancellationToken cancellationToken)
    {
        if (_bing is not null)
        {
            await _bing.RefreshAsync(cancellationToken);
            var snapshot = _bing.Current;
            var selected = snapshot.Selected;
            if (selected is not null)
            {
                return new WallpaperItem
                {
                    Id = $"bing:{selected.Key}",
                    Source = WallpaperSource.Bing,
                    Url = selected.WallpaperUrl,
                    LocalPath = snapshot.LocalPath,
                    Title = selected.Title,
                    Author = "Microsoft Bing",
                    AuthorUrl = "https://www.bing.com",
                    PageUrl = "https://www.bing.com",
                    DominantColor = "#0284c7"
                };
            }
        }
        return null;
    }

    private async Task<string?> EnsureLocalFileAsync(WallpaperItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.LocalPath) && File.Exists(item.LocalPath))
        {
            return item.LocalPath;
        }

        var destination = CachedFilePath(item.Id);
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        try
        {
            using var response = await _http.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var tempPath = destination + ".tmp";
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            File.Move(tempPath, destination);

            TrimCache(destination);
            return destination;
        }
        catch
        {
            return null;
        }
    }

    private string CachedFilePath(string id)
    {
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheDirectory, $"{safeId}.jpg");
    }

    private void TrimCache(string keepPath)
    {
        try
        {
            var files = new DirectoryInfo(_cacheDirectory)
                .GetFiles("*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            foreach (var extra in files.Skip(12))
            {
                if (!string.Equals(extra.FullName, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(extra.FullName); } catch { }
                }
            }
        }
        catch { }
    }

    private async Task<bool> TryApplyCurrentDesktopCoreAsync(CancellationToken cancellationToken)
    {
        var current = _selectedIndex >= 0 && _selectedIndex < _recent.Count ? _recent[_selectedIndex] : null;
        if (current is null)
        {
            return false;
        }

        var localPath = await EnsureLocalFileAsync(current, cancellationToken);
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            return false;
        }

        if (_backup is null)
        {
            if (Win32DesktopWallpaper.TryCapture(out var backup))
            {
                _backup = backup;
                try
                {
                    File.WriteAllText(_backupFile, JsonSerializer.Serialize(_backup));
                }
                catch { }
            }
        }

        var ok = Win32DesktopWallpaper.TrySet(localPath);
        if (ok)
        {
            _settings = _settings with { LastAppliedKey = current.Id };
            SaveSettings();
            PublishCurrent();
        }
        return ok;
    }

    private async Task OnRotationTimerTickAsync()
    {
        if (_disposed) return;

        var due = false;
        var now = DateTime.Now;
        var today = now.ToString("yyyy-MM-dd");

        switch (_settings.Rotation)
        {
            case WallpaperRotation.Daily:
                due = !string.Equals(_settings.ChosenOn, today, StringComparison.Ordinal);
                break;
            case WallpaperRotation.Interval15Min:
                due = true;
                break;
            case WallpaperRotation.Interval30Min:
                due = now.Minute % 30 < 2;
                break;
            case WallpaperRotation.Interval1Hour:
                due = now.Minute < 2;
                break;
            case WallpaperRotation.Interval4Hours:
                due = now.Hour % 4 == 0 && now.Minute < 2;
                break;
            case WallpaperRotation.Startup:
            case WallpaperRotation.Manual:
            default:
                due = false;
                break;
        }

        if (due)
        {
            await WithLock(() => SelectNextCoreAsync(forceNew: true, CancellationToken.None), CancellationToken.None);
        }
    }

    private void PublishCurrent(string? error = null)
    {
        var currentItem = _selectedIndex >= 0 && _selectedIndex < _recent.Count ? _recent[_selectedIndex] : null;
        lock (_gate)
        {
            _current = new WallpaperSnapshot
            {
                Current = currentItem,
                Recent = _recent.ToList(),
                SelectedIndex = _selectedIndex,
                IsBusy = false,
                Error = error,
                AppliedKey = _settings.LastAppliedKey,
                AutoApplyDesktop = _settings.AutoApplyDesktop,
                Settings = _settings
            };
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PublishBusy(bool busy)
    {
        lock (_gate)
        {
            _current = new WallpaperSnapshot
            {
                Current = _current.Current,
                Recent = _current.Recent,
                SelectedIndex = _current.SelectedIndex,
                IsBusy = busy,
                Error = null,
                AppliedKey = _current.AppliedKey,
                AutoApplyDesktop = _current.AutoApplyDesktop,
                Settings = _settings
            };
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSettingsAndBackup()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var loaded = JsonSerializer.Deserialize<WallpaperSettings>(json);
                if (loaded is not null)
                {
                    _settings = loaded;
                }
            }

            if (File.Exists(_backupFile))
            {
                var json = File.ReadAllText(_backupFile);
                _backup = JsonSerializer.Deserialize<WallpaperBackup>(json);
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings);
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }

    private async Task WithLock(Func<Task> action, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await action();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<T> WithLock<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rotationTimer.Dispose();
        _http.Dispose();
        _mutex.Dispose();
    }
}
