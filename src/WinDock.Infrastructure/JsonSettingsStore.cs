using System.Text.Json;
using System.Text.Json.Serialization;
using WinDock.Core;

namespace WinDock.Infrastructure;

public static class AppPaths
{
    public static string RoamingDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinDock");

    public static string LocalDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDock");

    public static string SettingsFile => Path.Combine(RoamingDirectory, "settings.json");

    public static string SettingsBackupFile => Path.Combine(RoamingDirectory, "settings.bak");

    public static string NotesDirectory => Path.Combine(RoamingDirectory, "notes");

    public static string LegacyNotesFile => Path.Combine(RoamingDirectory, "notes.txt");

    public static string LogsDirectory => Path.Combine(LocalDirectory, "logs");

    public static string IconCacheDirectory => Path.Combine(LocalDirectory, "icon-cache");

    public static string BingWallpaperDirectory => Path.Combine(LocalDirectory, "bing-wallpaper");

    public static string MediaThumbDirectory => Path.Combine(LocalDirectory, "media-thumbs");
}

public static class AppLog
{
    private static readonly object Gate = new();

    public static void Write(string message, Exception? error = null)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
            if (error is not null)
            {
                line += Environment.NewLine + error;
            }

            lock (Gate)
            {
                File.AppendAllText(Path.Combine(AppPaths.LogsDirectory, $"windock-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Logging must never take down the dock.
        }
    }
}

public static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
            }

            MoveWithRetry(temp, path, maxRetries: 5, initialDelayMs: 15);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static void MoveWithRetry(string source, string destination, int maxRetries, int initialDelayMs)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < maxRetries)
            {
                Thread.Sleep(initialDelayMs * attempt);
            }
        }
    }
}

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _path;
    private readonly string _backupPath;

    public JsonSettingsStore() : this(AppPaths.SettingsFile, AppPaths.SettingsBackupFile)
    {
    }

    public JsonSettingsStore(string path, string backupPath)
    {
        _path = path;
        _backupPath = backupPath;
    }

    public string? LastLoadMessage { get; private set; }

    public bool IsImmutable { get; private set; }

    public bool SkipNotesSweep { get; private set; }

    public AppSettings Load()
    {
        LastLoadMessage = null;
        IsImmutable = false;
        SkipNotesSweep = false;

        var mainExists = File.Exists(_path);
        var backupExists = File.Exists(_backupPath);
        if (!mainExists && !backupExists)
        {
            var firstRun = DefaultProfiles.CreateFactorySettings();
            try
            {
                Save(firstRun);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to write factory settings.", ex);
            }

            return firstRun;
        }

        if (TryAdopt(_path, overwriteOnPersist: true, out var fromMain, out var mainMessage))
        {
            LastLoadMessage = mainMessage;
            SkipNotesSweep = IsImmutable;
            return fromMain;
        }

        if (TryAdopt(_backupPath, overwriteOnPersist: true, out var fromBackup, out var backupMessage))
        {
            LastLoadMessage = backupMessage ?? UiText.RestoredFromBackup;
            try
            {
                Save(fromBackup);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to restore settings.json from backup.", ex);
            }

            return fromBackup;
        }

        if (mainExists)
        {
            Quarantine(_path);
        }

        var factory = DefaultProfiles.CreateFactorySettings();
        try
        {
            Save(factory);
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to write factory settings.", ex);
        }

        SkipNotesSweep = true;
        LastLoadMessage = UiText.ResetToFactory;
        AppLog.Write(LastLoadMessage);
        return factory;
    }

    public void Save(AppSettings settings)
    {
        if (IsImmutable)
        {
            return;
        }

        var normalized = SettingsValidator.EnsureInvariants(settings);
        if (normalized.SchemaVersion > SettingsSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Settings schema {normalized.SchemaVersion} is newer than this build ({SettingsSchema.CurrentVersion}).");
        }

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        AtomicFile.WriteAllText(_path, json);
        try
        {
            File.Copy(_path, _backupPath, overwrite: true);
        }
        catch (IOException)
        {
            // bak is best-effort after a successful write.
        }
    }

    private bool TryAdopt(string path, bool overwriteOnPersist, out AppSettings settings, out string? message)
    {
        settings = null!;
        message = null;
        if (!TryRead(path, out var parsed))
        {
            return false;
        }

        if (parsed.SchemaVersion > SettingsSchema.CurrentVersion)
        {
            message = UiText.NewerSchema(parsed.SchemaVersion);
            AppLog.Write(message);
            IsImmutable = true;
            settings = DefaultProfiles.CreateFactorySettings();
            return true;
        }

        try
        {
            var before = SnapshotIds(parsed);
            var normalized = SettingsValidator.EnsureInvariants(Migrate(parsed));
            var assigned = SnapshotIds(normalized) != before;
            if (assigned && overwriteOnPersist && normalized.SchemaVersion <= SettingsSchema.CurrentVersion)
            {
                try
                {
                    Save(normalized);
                }
                catch (Exception ex)
                {
                    AppLog.Write("Failed to persist assigned dock item ids.", ex);
                }
            }

            settings = normalized;
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Rejected settings at {path}.", ex);
            return false;
        }
    }

    private static AppSettings Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion == 0)
        {
            return new AppSettings
            {
                SchemaVersion = SettingsSchema.CurrentVersion,
                Ui = settings.Ui,
                Privacy = settings.Privacy,
                Hotkeys = settings.Hotkeys,
                ManualOverride = settings.ManualOverride,
                ActiveProfileId = settings.ActiveProfileId,
                LastProfileId = settings.LastProfileId,
                ManualOverrideProfileId = settings.ManualOverrideProfileId,
                ManualOverrideFocusActive = settings.ManualOverrideFocusActive,
                ManualOverrideTopologyKey = settings.ManualOverrideTopologyKey,
                Profiles = settings.Profiles,
                SwitchHistory = settings.SwitchHistory
            };
        }

        if (settings.SchemaVersion > SettingsSchema.CurrentVersion)
        {
            throw new InvalidOperationException("Refusing to load a newer settings schema.");
        }

        return settings;
    }

    private static string SnapshotIds(AppSettings settings) =>
        string.Join(";", settings.Profiles.SelectMany(profile =>
            profile.Dock.Items.Select(item => profile.Id + ":" + item.Id)
                .Concat((profile.HiddenWidgets ?? []).Select(item => profile.Id + ":h:" + item.Id))));

    private static bool TryRead(string path, out AppSettings settings)
    {
        settings = null!;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            settings = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void Quarantine(string path)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var dest = path + $".corrupt.{stamp}";
        try
        {
            File.Move(path, dest, overwrite: true);
        }
        catch (IOException)
        {
            // Best-effort; factory settings still apply.
        }
    }
}

public sealed class FileNotesStore : INotesStore
{
    private readonly string _directory;
    private readonly string _legacyFile;
    private int _legacyAdopted;

    public FileNotesStore()
        : this(AppPaths.NotesDirectory, AppPaths.LegacyNotesFile)
    {
    }

    public FileNotesStore(string directory, string? legacyFile = null)
    {
        _directory = directory;
        _legacyFile = legacyFile ?? Path.Combine(AppPaths.RoamingDirectory, "notes.txt");
    }

    public string Load(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public void Save(string key, string text)
    {
        if (NotesText.IsBlank(text))
        {
            Delete(key);
            return;
        }

        Directory.CreateDirectory(_directory);
        AtomicFile.WriteAllText(PathFor(key), text);
    }

    public void Delete(string key)
    {
        try
        {
            var path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best-effort; Sweep or the next Save can retry.
        }
    }

    public void Sweep(IEnumerable<string> liveKeys)
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        var live = (liveKeys ?? []).Select(PathFor).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(_directory, "*.txt"))
        {
            if (live.Contains(file))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception)
            {
                // Leave the file; a later Sweep can retry.
            }
        }
    }

    public void TryAdoptLegacy(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !File.Exists(_legacyFile))
        {
            return;
        }

        var dest = PathFor(key);
        if (!File.Exists(dest))
        {
            if (Interlocked.Exchange(ref _legacyAdopted, 1) != 0)
            {
                RetireLegacy();
                return;
            }

            try
            {
                Directory.CreateDirectory(_directory);
                File.Copy(_legacyFile, dest, overwrite: false);
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _legacyAdopted, 0);
                return;
            }
        }

        RetireLegacy();
    }

    private void RetireLegacy()
    {
        if (!File.Exists(_legacyFile))
        {
            return;
        }

        try
        {
            File.Move(_legacyFile, _legacyFile + ".bak", overwrite: true);
        }
        catch (Exception)
        {
            try
            {
                File.Delete(_legacyFile);
            }
            catch (Exception)
            {
                // Leave it; TryAdoptLegacy will no-op once dest files exist.
            }
        }
    }

    private string PathFor(string key)
    {
        var safe = string.Concat((key ?? string.Empty).Where(ch => char.IsLetterOrDigit(ch) || ch == '-'));
        if (safe.Length == 0)
        {
            safe = "note";
        }

        return Path.Combine(_directory, safe + ".txt");
    }
}

public sealed class SystemClockProvider : IClockProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTime LocalNow => DateTime.Now;
}
