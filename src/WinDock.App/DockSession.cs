using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WinDock.Core;
using WinDock.Core.Permissions;
using WinDock.Infrastructure;
using WinDock.Providers;
using WinDock.Shell;
using WinDock.Widgets;

namespace WinDock.App;

public sealed class DockSession : IDisposable
{
    private readonly IStartupOptions _options;
    private readonly ISettingsStore _store;
    private readonly IDisplayService _display;
    private readonly IDockLayoutService _layout = new CoreDockLayoutService();
    private readonly IZorderPolicy _zorder = new DefaultZorderPolicy();
    private readonly IAppBarService _appBar = new Win32AppBarService();
    private readonly ProfileEngine _engine = new();
    private readonly WidgetViewRegistry _views = new();
    private readonly DockWindow _dock = new();
    private readonly PinStackFlyout _stackFlyout = new();
    private readonly System.Threading.Timer _runningPins;
    private readonly ShellHost _shell;
    private readonly DispatcherTimer _hostPulse = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _schedulePulse = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _alarmPulse = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _transparencyDebounce = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Dictionary<string, UIElement> _widgetViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageSource> _pinIcons = new(StringComparer.OrdinalIgnoreCase);
    private readonly PermissionGate _gate;
    private SettingsWindow? _settings;
    private bool _disposing;
    private IReadOnlySet<string> _trackedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlySet<string> _runningIds = new HashSet<string>(StringComparer.Ordinal);
    private HashSet<string> _lastImages = new(StringComparer.OrdinalIgnoreCase);
    private int _runningBusy;
    private string _itemKey = string.Empty;
    private DockZorderState _zorderState;
    private bool _samplingQuiet;
    private bool _samplingQuietSet;
    private DateTimeOffset? _leftHostAt;
    private DateTimeOffset? _wakeUntil;
    private string? _appliedProfileId;
    private int? _pendingTransparency;
    private string? _loadMessage;
    private readonly HashSet<string> _launchConfirmed = new(StringComparer.OrdinalIgnoreCase);

    public DockSession(IStartupOptions options)
    {
        _options = options;
        _store = new JsonSettingsStore();
        var loaded = _store.Load();
        Settings = SettingsEditor.ClearSessionOverride(loaded);
        UiLocale.Apply(Settings.Ui.Language);
        _loadMessage = _store.LastLoadMessage;
        if (loaded.ManualOverride.Until == ManualOverrideUntil.Session
            && !string.IsNullOrWhiteSpace(loaded.ManualOverrideProfileId))
        {
            try
            {
                _store.Save(Settings);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to clear session profile override.", ex);
            }
        }
        _gate = new PermissionGate(Settings.Privacy);
        Hub = new ProviderHub(_gate, new FileNotesStore(), AppPaths.BingWallpaperDirectory, AppPaths.IconCacheDirectory, AppPaths.WallpaperDirectory);
        Hub.Media.CaptureMetadata = _gate.Allows(PermissionIds.MediaRead);
        _display = new Win32DisplayService();
        _shell = new ShellHost();
        _runningPins = new System.Threading.Timer(_ => SampleRunning(), null, Timeout.Infinite, Timeout.Infinite);
        _transparencyDebounce.Tick += (_, _) => FlushTransparency();
    }

    public AppSettings Settings { get; private set; }

    public bool IsSafeMode => _options.SafeMode;

    public ProfileDiff CurrentDiff { get; private set; } = null!;

    public string? RevealHotkeyBindError { get; private set; }

    public IProviderHub Hub { get; }

    public event EventHandler? Changed;

    public event EventHandler? TaskbarCreated;

    public void RequestExit() => Application.Current.Shutdown();

    private bool _notifiedBackground;
    public void NotifyBackgroundRunning()
    {
        if (_notifiedBackground) return;
        _notifiedBackground = true;
        if (Application.Current is App app)
        {
            app.ShowTrayBalloon(ProductInfo.Name, UiText.BackgroundRunningNotice);
        }
    }

    public void Start()
    {
        _dock.WindowEnumerator = item => Hub.Shell.GetWindows(item);
        _dock.WindowActivateRequested += (_, hwnd) => Hub.Shell.ActivateWindow(hwnd);
        _dock.WindowCloseRequested += (_, hwnd) => Hub.Shell.CloseWindow(hwnd);
        _dock.SettingsRequested += (_, _) => ShowSettings();
        _dock.ExitRequested += (_, _) => RequestExit();
        _dock.ProfileSwitchRequested += (_, profileId) => SwitchProfile(profileId);
        _dock.IslandLengthChanged += (_, length) =>
        {
            if (_options.SafeMode || CurrentDiff is null)
            {
                return;
            }

            Commit(SettingsEditor.WithIslandLength(Settings, CurrentDiff.ProfileId, length));
        };
        _dock.ItemsReordered += (_, reorder) =>
        {
            if (_options.SafeMode || CurrentDiff is null)
            {
                return;
            }

            var dock = CurrentDiff.Dock;
            var next = DockPinCollapse.Reorder(
                dock.Items,
                dock.CollapseApps,
                dock.CollapseFolders,
                reorder.From,
                reorder.To);
            var stored = Settings.Profiles.FirstOrDefault(profile => profile.Id == CurrentDiff.ProfileId)?.Dock.Items
                ?? dock.Items;
            Commit(SettingsEditor.ReplaceDockItems(
                Settings,
                CurrentDiff.ProfileId,
                SettingsEditor.RestoreShielded(stored, next)));
        };
        _dock.WidgetOpenRequested += (_, itemId) =>
        {
            if (_widgetViews.TryGetValue(itemId, out var view) && view is IWidgetOpenable openable)
            {
                openable.Open();
            }
        };
        _dock.WidgetCustomizeRequested += (_, itemId) =>
        {
            if (_widgetViews.TryGetValue(itemId, out var view) && view is IWidgetCustomizable custom)
            {
                custom.OpenCustomize();
            }
        };
        _dock.WidgetRemoveRequested += (_, itemId) => OnWidgetRemove(itemId);
        _dock.WidgetDensityRequested += (_, request) => ApplyWidgetDensity(request);
        _dock.PinActivateRequested += (_, itemId) => ActivatePin(itemId);
        _dock.PinStackToggleRequested += (_, request) => TogglePinStack(request);
        _dock.PinLaunchRequested += (_, itemId) => LaunchPin(itemId);
        _dock.PinAddRequested += (_, kind) => PickPin(kind);
        _dock.PinsDropped += (_, drop) => AddDroppedPins(drop);
        _dock.IslandMonitorRequested += (_, monitor) => SetDockMonitor(monitor);
        _dock.StripActivated += (_, _) => Dispatch(RevealDock);
        _dock.IslandMouseWheel += OnIslandMouseWheel;
        _hostPulse.Tick += (_, _) => PulseZorder();
        _schedulePulse.Tick += (_, _) => Apply(Settings);
        _alarmPulse.Tick += (_, _) => PulseAlarms();
        _display.Changed += (_, _) => Dispatch(() => Apply(Settings));
        Hub.Focus.IsFocusActiveChanged += (_, _) => Dispatch(() => Apply(Settings));
        _shell.DisplayChanged += (_, _) => _display.RequestRefresh();
        _shell.ZorderHint += (_, _) => Dispatch(PulseZorder);
        _shell.HotkeyPressed += (_, action) => Dispatch(() => OnHotkey(action));
        _shell.TaskbarCreated += (_, _) => Dispatch(() => TaskbarCreated?.Invoke(this, EventArgs.Empty));
        WidgetFlyout.Elevate = PopupTopmost.TryElevate;
        WidgetFlyout.OpenStateChanged += OnFlyoutOpenStateChanged;
        _dock.WidgetCreateFailed += (_, failure) =>
            AppLog.Write($"Failed to create widget slot '{failure.WidgetId}'.", failure.Exception);
        _stackFlyout.PinActivateRequested += (_, itemId) => ActivatePin(itemId);
        _stackFlyout.PinLaunchRequested += (_, itemId) => LaunchPin(itemId);
        _stackFlyout.PinRemoveRequested += (_, itemId) => OnWidgetRemove(itemId);
        _dock.MouseEnter += (_, _) => Dispatch(PulseZorder);
        _dock.MouseMove += (_, _) =>
        {
            if (_hostPulse.Interval > TimeSpan.FromMilliseconds(100))
            {
                _hostPulse.Interval = TimeSpan.FromMilliseconds(100);
            }
        };
        Apply(Settings);
        _hostPulse.Start();
        _alarmPulse.Start();
        if (ProductFeatures.AutoProfileTriggersEnabled)
        {
            _schedulePulse.Start();
        }

        _dock.Show();
        if (!string.IsNullOrWhiteSpace(_loadMessage))
        {
            MessageBox.Show(_loadMessage, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            _loadMessage = null;
        }
    }

    public void ToggleSettings()
    {
        if (_options.SafeMode)
        {
            MessageBox.Show(UiText.SafeModeBlock, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_settings is not null && _settings.IsVisible)
        {
            if (_settings.IsActive)
            {
                _settings.Hide();
                PulseZorder();
                return;
            }

            if (_settings.WindowState == WindowState.Minimized)
            {
                _settings.WindowState = WindowState.Normal;
            }

            _settings.Activate();
            PulseZorder();
            return;
        }

        ShowSettings();
    }

    public void ShowSettings()
    {
        if (_options.SafeMode)
        {
            MessageBox.Show(UiText.SafeModeBlock, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_settings is null)
        {
            _settings = new SettingsWindow(this);
            _settings.Closed += (_, _) => _settings = null;
            _settings.IsVisibleChanged += (_, _) => Dispatch(PulseZorder);
        }

        if (_settings.WindowState == WindowState.Minimized)
        {
            _settings.WindowState = WindowState.Normal;
        }

        _settings.Reload();
        _settings.Show();
        _settings.Activate();
        PulseZorder();
    }

    public void SetTheme(ThemePreference theme)
    {
        if (Settings.Ui.Theme == theme)
        {
            return;
        }

        Commit(SettingsEditor.WithTheme(Settings, theme));
    }

    public void SetLanguage(UiLanguage language)
    {
        if (Settings.Ui.Language == language)
        {
            return;
        }

        Commit(SettingsEditor.WithLanguage(Settings, language));
    }

    public void SetBackdrop(DockBackdrop backdrop)
    {
        if (Settings.Ui.Backdrop == backdrop)
        {
            return;
        }

        Commit(SettingsEditor.WithBackdrop(Settings, backdrop));
    }

    public void SetIslandTransparency(int transparency)
    {
        _pendingTransparency = transparency;
        _transparencyDebounce.Stop();
        _transparencyDebounce.Start();
    }

    public void SetDockEdge(DockEdge edge)
    {
        if (CurrentDiff.Dock.Edge == edge)
        {
            return;
        }

        Commit(SettingsEditor.WithDockEdge(Settings, CurrentDiff.ProfileId, edge));
    }

    public void SetDockMonitor(string monitor)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var next = string.IsNullOrWhiteSpace(monitor) ? "primary" : monitor.Trim();
        if (DockLayoutCalculator.SameMonitor(_display.Current, CurrentDiff.Dock.Monitor, next))
        {
            return;
        }

        Commit(SettingsEditor.WithDockMonitor(Settings, CurrentDiff.ProfileId, next));
    }

    public DisplayTopologySnapshot Topology => _display.Current;

    public void SetAutoHide(bool autoHide)
    {
        if (CurrentDiff.Dock.AutoHide == autoHide)
        {
            return;
        }

        Commit(SettingsEditor.WithAutoHide(Settings, CurrentDiff.ProfileId, autoHide));
    }

    public void SetPeekOnPointer(bool peek)
    {
        if (CurrentDiff.Dock.PeekOnPointer == peek)
        {
            return;
        }

        Commit(SettingsEditor.WithPeekOnPointer(Settings, CurrentDiff.ProfileId, peek));
    }

    public void SetCollapseApps(bool collapse)
    {
        if (CurrentDiff.Dock.CollapseApps == collapse)
        {
            return;
        }

        Commit(SettingsEditor.WithCollapseApps(Settings, CurrentDiff.ProfileId, collapse));
    }

    public void SetCollapseFolders(bool collapse)
    {
        if (CurrentDiff.Dock.CollapseFolders == collapse)
        {
            return;
        }

        Commit(SettingsEditor.WithCollapseFolders(Settings, CurrentDiff.ProfileId, collapse));
    }

    public bool SetRevealHotkey(bool enabled)
    {
        if (CurrentDiff.Dock.RevealHotkey == enabled)
        {
            return true;
        }

        if (enabled && !TryBindReveal(Settings, CurrentDiff.Dock with { AutoHide = true, RevealHotkey = true }))
        {
            RevealHotkeyBindError = UiText.HotkeyTaken;
            _shell.BindHotkeys(EffectiveHotkeys(Settings, CurrentDiff.Dock));
            return false;
        }

        Commit(SettingsEditor.WithRevealHotkey(Settings, CurrentDiff.ProfileId, enabled));
        return true;
    }

    public bool TrySetRevealDockHotkey(string chord)
    {
        if (!HotkeyParser.TryParse(chord, out var modifiers, out var virtualKey))
        {
            return false;
        }

        var formatted = HotkeyParser.Format(modifiers, virtualKey);
        if (string.IsNullOrWhiteSpace(formatted)
            || HotkeyParser.ConflictsWith(Settings.Hotkeys, formatted))
        {
            return false;
        }

        var next = SettingsEditor.WithRevealHotkey(
            SettingsEditor.WithRevealDockHotkey(Settings, formatted),
            CurrentDiff.ProfileId,
            true);
        var probe = CurrentDiff.Dock with { AutoHide = true, RevealHotkey = true };
        if (!TryBindReveal(next, probe))
        {
            RevealHotkeyBindError = UiText.HotkeyTaken;
            _shell.BindHotkeys(EffectiveHotkeys(Settings, CurrentDiff.Dock));
            return false;
        }

        Commit(next);
        return true;
    }

    private bool TryBindReveal(AppSettings settings, DockLayout dock) =>
        _shell.BindHotkeys(EffectiveHotkeys(settings, dock));

    private static HotkeySettings EffectiveHotkeys(AppSettings settings, DockLayout dock)
    {
        var hotkeys = settings.Hotkeys;
        if (!ProductFeatures.ProfilesEnabled)
        {
            hotkeys = hotkeys with
            {
                NextProfile = null,
                PreviousProfile = null,
                UndoProfile = null
            };
        }

        if (!dock.AutoHide || !dock.RevealHotkey)
        {
            hotkeys = hotkeys with { RevealDock = null };
        }

        return hotkeys;
    }

    public void SetManualOverrideUntil(ManualOverrideUntil until) =>
        Commit(SettingsEditor.WithManualOverrideUntil(Settings, until));

    public void UndoProfile()
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return;
        }

        var target = SettingsEditor.UndoTarget(Settings, CurrentDiff.ProfileId);
        if (string.IsNullOrWhiteSpace(target) || target == CurrentDiff.ProfileId)
        {
            return;
        }

        SwitchProfile(target);
    }

    public void CycleProfile(int delta)
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return;
        }

        SwitchProfile(SettingsEditor.NextProfileId(Settings, CurrentDiff.ProfileId, delta));
    }

    public void AddWidget(string widgetId)
    {
        if (CurrentDiff is null || !BuiltInWidgets.CanAdd(widgetId))
        {
            return;
        }

        if (BuiltInWidgets.IsSingleton(widgetId)
            && SettingsEditor.HasWidgetChip(CurrentDiff.Dock.Items, widgetId))
        {
            UserAlerts.Show(ProductInfo.Name, UiText.AlreadyOnDock);
            return;
        }

        if (!TryConsentForWidget(widgetId, out var next))
        {
            return;
        }

        Commit(SettingsEditor.AddWidget(next, CurrentDiff.ProfileId, widgetId));
    }

    public void ShowHiddenNote(string itemId)
    {
        if (CurrentDiff is null || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        Commit(SettingsEditor.ShowHiddenWidget(Settings, CurrentDiff.ProfileId, itemId));
    }

    public void DiscardHiddenNote(string itemId)
    {
        if (CurrentDiff is null || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var confirm = MessageBox.Show(
            UiText.ConfirmDeleteHiddenNote,
            UiText.NotesHistory,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        Commit(SettingsEditor.DiscardHiddenWidget(Settings, CurrentDiff.ProfileId, itemId));
        DeleteNoteFile(itemId);
    }

    private void OnNotesRestoreRequested(object? sender, string hiddenId)
    {
        if (CurrentDiff is null || string.IsNullOrWhiteSpace(hiddenId))
        {
            return;
        }

        var currentId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(currentId) || currentId == hiddenId)
        {
            return;
        }

        if (sender is NotesWidget notes)
        {
            notes.Flush();
        }

        var blank = NotesText.IsBlank(Hub.Notes.Load(currentId));
        if (blank && sender is NotesWidget empty)
        {
            empty.Discard();
        }

        Commit(SettingsEditor.ReplaceDockItemWithHidden(
            Settings,
            CurrentDiff.ProfileId,
            currentId,
            hiddenId,
            stashCurrent: !blank));
        if (blank)
        {
            DeleteNoteFile(currentId);
        }

        if (_widgetViews.TryGetValue(hiddenId, out var view) && view is IWidgetOpenable openable)
        {
            openable.Open();
        }
    }

    private void OnNotesHistoryDiscardRequested(object? sender, string itemId) => DiscardHiddenNote(itemId);

    private IReadOnlyList<NotesHistoryItem> NotesHistory(string? exceptId)
    {
        if (CurrentDiff is null)
        {
            return [];
        }

        var profile = Settings.Profiles.FirstOrDefault(profile => profile.Id == CurrentDiff.ProfileId);
        if (profile is null)
        {
            return [];
        }

        return SettingsEditor.HiddenNotes(profile)
            .Where(item => !string.Equals(item.Id, exceptId, StringComparison.Ordinal))
            .Select(item => new NotesHistoryItem(
                item.Id,
                NotesText.ListPreview(Hub.Notes.Load(item.Id)),
                NotePapers.Normalize(item.PaperColor)))
            .ToArray();
    }

    private bool TryConsentForWidget(string widgetId, out AppSettings settings)
    {
        settings = Settings;
        var message = widgetId switch
        {
            BuiltInWidgets.NowPlaying => UiText.ConsentNowPlaying,
            BuiltInWidgets.Network => UiText.ConsentNetwork,
            BuiltInWidgets.Processes => UiText.ConsentProcesses,
            BuiltInWidgets.BingWallpaper => UiText.ConsentBing,
            BuiltInWidgets.Weather => UiText.ConsentWeather,
            _ => null
        };
        if (message is null)
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.NowPlaying && settings.Privacy.MediaRead)
        {
            settings = SettingsEditor.GrantPermission(settings, PermissionIds.MediaRead, CurrentDiff.ProfileId);
            return true;
        }

        if (widgetId == BuiltInWidgets.Network && settings.Privacy.NetworkDetails)
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.Processes && settings.Privacy.ProcessNames)
        {
            return true;
        }

        if (widgetId == BuiltInWidgets.BingWallpaper && settings.Privacy.BingDownload)
        {
            return true;
        }

        var result = AskConsent(message, UiText.AddWidgetTitle(BuiltInWidgets.DisplayName(widgetId)));
        if (result != MessageBoxResult.OK)
        {
            return false;
        }

        if (widgetId == BuiltInWidgets.NowPlaying)
        {
            settings = SettingsEditor.WithPrivacy(settings, settings.Privacy with { MediaRead = true });
            settings = SettingsEditor.GrantPermission(settings, PermissionIds.MediaRead, CurrentDiff.ProfileId);
            _gate.UpdatePrivacy(settings.Privacy);
            Hub.Media.CaptureMetadata = _gate.Allows(PermissionIds.MediaRead);
        }
        else if (widgetId == BuiltInWidgets.Network)
        {
            settings = SettingsEditor.WithPrivacy(settings, settings.Privacy with { NetworkDetails = true });
            _gate.UpdatePrivacy(settings.Privacy);
        }
        else if (widgetId == BuiltInWidgets.Processes)
        {
            settings = SettingsEditor.WithPrivacy(settings, settings.Privacy with { ProcessNames = true });
            _gate.UpdatePrivacy(settings.Privacy);
        }
        else if (widgetId == BuiltInWidgets.BingWallpaper)
        {
            settings = SettingsEditor.WithPrivacy(settings, settings.Privacy with { BingDownload = true });
            _gate.UpdatePrivacy(settings.Privacy);
        }

        return true;
    }

    private MessageBoxResult AskConsent(string message, string caption)
    {
        if (_settings is { IsVisible: true } owner)
        {
            return MessageBox.Show(owner, message, caption, MessageBoxButton.OKCancel, MessageBoxImage.Information);
        }

        return MessageBox.Show(message, caption, MessageBoxButton.OKCancel, MessageBoxImage.Information);
    }

    private void EnsureSampler(string widgetId)
    {
        switch (widgetId)
        {
            case BuiltInWidgets.NowPlaying:
                Hub.Media.Start();
                break;
            case BuiltInWidgets.Network:
                Hub.Network.Start();
                break;
            case BuiltInWidgets.Power:
                Hub.Power.Start();
                break;
            case BuiltInWidgets.SysActivity:
                Hub.Perf.Start();
                break;
            case BuiltInWidgets.Disk:
                Hub.Disk.Start();
                break;
            case BuiltInWidgets.Gpu:
                Hub.Gpu.Start();
                break;
            case BuiltInWidgets.Processes:
                if (Settings.Privacy.ProcessNames)
                {
                    Hub.Processes.Start();
                }
                break;
            case BuiltInWidgets.Stocks:
                Hub.Stocks.Start();
                break;
        }
    }

    private void OnIslandLayoutChanged(object? sender, EventArgs e) => RelayoutIsland();

    public void SetPrivacy(PrivacySettings privacy)
    {
        var next = SettingsEditor.WithPrivacy(Settings, privacy);
        next = privacy.MediaRead
            ? SettingsEditor.GrantPermission(next, PermissionIds.MediaRead, CurrentDiff.ProfileId)
            : SettingsEditor.RevokePermission(next, PermissionIds.MediaRead);
        _gate.UpdatePrivacy(privacy);
        Hub.Media.CaptureMetadata = _gate.Allows(PermissionIds.MediaRead);
        Hub.Weather.AllowAutoLocate = privacy.PreciseLocation;
        Commit(next);
    }

    public void SetBingWallpaperAutoApply(bool autoApply)
    {
        Hub.Bing.AutoApplyDaily = autoApply;
        Commit(SettingsEditor.WithBingWallpaperAutoApply(Settings, autoApply));
        if (autoApply)
        {
            _ = Hub.Bing.CheckAndAutoApplyAsync();
        }
    }

    public void SwitchProfile(string profileId)
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return;
        }

        var topology = _display.Current;
        Commit(SettingsEditor.SwitchProfile(Settings, profileId, Hub.Focus.IsFocusActive, topology.TopologyKey));
    }

    public void DuplicateCurrentProfile()
    {
        if (!ProductFeatures.ProfilesEnabled || CurrentDiff is null)
        {
            return;
        }

        var result = SettingsEditor.DuplicateProfile(Settings, CurrentDiff.ProfileId);
        foreach (var (from, to) in result.NotesKeys)
        {
            try
            {
                var text = Hub.Notes.Load(from);
                if (!NotesText.IsBlank(text))
                {
                    Hub.Notes.Save(to, text);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to copy note for duplicated profile.", ex);
            }
        }

        Commit(result.Settings);
    }

    public void RenameProfile(string profileId, string name)
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return;
        }

        Commit(SettingsEditor.RenameProfile(Settings, profileId, name));
    }

    public void RemoveProfile(string profileId)
    {
        if (!ProductFeatures.ProfilesEnabled || profileId == DefaultProfiles.GeneralId)
        {
            return;
        }

        Commit(SettingsEditor.RemoveProfile(Settings, profileId));
    }

    public void RemoveDockItem(string itemId) => OnWidgetRemove(itemId);

    public void MoveDockItem(int fromIndex, int toIndex)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        var profileId = CurrentDiff.ProfileId;
        Commit(SettingsEditor.MoveDockItem(Settings, profileId, fromIndex, toIndex));
    }

    public void ReplaceDockItems(IReadOnlyList<DockItem> items)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        var profileId = CurrentDiff.ProfileId;
        Commit(SettingsEditor.ReplaceDockItems(Settings, profileId, items));
    }

    public void UpdateWidgetWeather(string itemId, WeatherSettings weather)
    {
        if (CurrentDiff is null) return;
        Commit(SettingsEditor.WithWeather(Settings, CurrentDiff.ProfileId, itemId, weather));
        if (_widgetViews.TryGetValue(itemId, out var view) && view is IWeatherConfigurable w)
        {
            w.ApplySettings(weather);
        }
    }

    public void UpdateWidgetStock(string itemId, StockWidgetSettings stocks)
    {
        if (CurrentDiff is null) return;
        Commit(SettingsEditor.WithStockWidget(Settings, CurrentDiff.ProfileId, itemId, stocks));
        if (_widgetViews.TryGetValue(itemId, out var view) && view is IStockConfigurable s)
        {
            s.ApplySettings(stocks);
        }
    }

    public void UpdateWidgetWhiteNoise(string itemId, WhiteNoiseSettings noise)
    {
        if (CurrentDiff is null) return;
        Commit(SettingsEditor.WithWhiteNoise(Settings, CurrentDiff.ProfileId, itemId, noise));
    }

    public void UpdateWidgetClockTimeZone(string itemId, string? timeZoneId)
    {
        if (CurrentDiff is null) return;
        Commit(SettingsEditor.WithClockTimeZone(Settings, CurrentDiff.ProfileId, itemId, timeZoneId));
    }

    public void UpdateWidgetSysActivity(string itemId, SysActivitySettings activity)
    {
        if (CurrentDiff is null) return;
        Commit(SettingsEditor.WithSysActivity(Settings, CurrentDiff.ProfileId, itemId, activity));
    }

    public void SetWidgetDensity(string itemId, WidgetDensity density)
    {
        ApplyWidgetDensity(new WidgetDensityRequest { ItemId = itemId, Density = density });
    }

    public void ResetSettings() => Commit(DefaultProfiles.CreateFactorySettings());

    public string ExportSettingsJson()
    {
        return JsonSerializer.Serialize(Settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public bool ImportSettingsJson(string json, out string? error)
    {
        error = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<AppSettings>(json);
            if (parsed is null)
            {
                error = "Empty settings content.";
                return false;
            }

            var validated = SettingsValidator.EnsureInvariants(parsed);
            Commit(validated);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetProfileHotkeys(string? next, string? previous, string? undo)
    {
        string? Fmt(string? c) => string.IsNullOrWhiteSpace(c) || !HotkeyParser.TryParse(c, out var m, out var vk)
            ? null
            : HotkeyParser.Format(m, vk);

        var candidate = Settings.Hotkeys with
        {
            NextProfile = Fmt(next),
            PreviousProfile = Fmt(previous),
            UndoProfile = Fmt(undo)
        };

        var nextSettings = Settings with { Hotkeys = candidate };
        if (!_shell.BindHotkeys(EffectiveHotkeys(nextSettings, CurrentDiff.Dock)))
        {
            _shell.BindHotkeys(EffectiveHotkeys(Settings, CurrentDiff.Dock));
            return false;
        }

        Commit(nextSettings);
        return true;
    }

    public bool IsLightTheme() => ThemeResolver.IsLight(Settings.Ui.Theme);

    public void Dispose()
    {
        if (_disposing)
        {
            return;
        }

        _disposing = true;
        _hostPulse.Stop();
        _schedulePulse.Stop();
        _alarmPulse.Stop();
        _transparencyDebounce.Stop();
        _runningPins.Dispose();
        _stackFlyout.Hide();
        _widgetViews.Clear();
        WidgetFlyout.OpenStateChanged -= OnFlyoutOpenStateChanged;
        _dock.IslandMouseWheel -= OnIslandMouseWheel;
        _dock.Dispose();
        _appBar.Remove();
        _display.Dispose();
        _shell.Dispose();
        Hub.Dispose();
        _settings?.AllowClose();
        _settings?.Close();
    }

    private void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private void Commit(AppSettings next)
    {
        try
        {
            _store.Save(next);
            Apply(next);
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to save settings.", ex);
            UserAlerts.Show(ProductInfo.Name, UiText.SaveFailed);
            Apply(Settings);
        }
    }


    private void Apply(AppSettings settings)
    {
        try
        {
            Settings = SettingsValidator.EnsureInvariants(settings);
            UiLocale.Apply(Settings.Ui.Language);
            var snapshot = ContextAssembler.Create(_options, Settings, Hub, _display.Current);
            CurrentDiff = _engine.Evaluate(Settings, snapshot);
            var switched = ProductFeatures.ProfilesEnabled && _appliedProfileId != CurrentDiff.ProfileId;
            string? announceProfileId = null;
            if (switched)
            {
                if (_appliedProfileId is not null)
                {
                    announceProfileId = CurrentDiff.ProfileId;
                }

                Settings = SettingsEditor.RecordSwitch(Settings, _appliedProfileId, CurrentDiff.ProfileId, CurrentDiff.Reason);
                try
                {
                    _store.Save(Settings);
                }
                catch (Exception ex)
                {
                    AppLog.Write("Failed to persist profile history.", ex);
                }

                _appliedProfileId = CurrentDiff.ProfileId;
            }

            _gate.SetActiveProfile(CurrentDiff.Permissions);
            Hub.Media.CaptureMetadata = _gate.Allows(PermissionIds.MediaRead);

            var isLight = ThemeResolver.IsLight(Settings.Ui.Theme);
            var monitor = _layout.ResolveMonitor(snapshot.Topology, CurrentDiff.Dock.Monitor);
            var thickness = DockLayoutCalculator.ThicknessFor(CurrentDiff.Dock.Edge);
            _dock.SetEdge(CurrentDiff.Dock.Edge);
            WidgetFlyout.Edge = CurrentDiff.Dock.Edge;
            _dock.SetHostWorkArea(monitor.PixelWorkArea);
            _trackedNames = PinRunning.TrackedProcessNames(CurrentDiff.Dock.Items);
            SyncRunningSampler();
            var itemKey = ItemLayoutKey(
                CurrentDiff.Dock.Items,
                CurrentDiff.Dock.CollapseApps,
                CurrentDiff.Dock.CollapseFolders);
            if (itemKey != _itemKey)
            {
                _stackFlyout.Hide();
                _dock.ApplyItems(
                    DockPinCollapse.Project(
                        CurrentDiff.Dock.Items,
                        CurrentDiff.Dock.CollapseApps,
                        CurrentDiff.Dock.CollapseFolders),
                    _options.SafeMode ? null : GetOrCreateWidget,
                    _options.SafeMode ? null : PinIcon,
                    _options.SafeMode ? null : StockIcon);
                _itemKey = itemKey;
                PrunePinIcons();
            }
            foreach (var item in CurrentDiff.Dock.Items)
            {
                if (item.Type == DockItemType.WidgetChip
                    && !string.IsNullOrWhiteSpace(item.WidgetId)
                    && _widgetViews.TryGetValue(item.Id, out var view))
                {
                    try
                    {
                        ApplyWidget(view, item, isLight);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write($"Failed to apply widget '{item.WidgetId}'.", ex);
                    }
                }
            }

            RelayoutIsland(monitor, thickness);
            _dock.ApplyTheme(isLight, Settings.Ui.Backdrop, Settings.Ui.IslandTransparency);
            _stackFlyout.ApplyTheme(isLight, Settings.Ui.Backdrop);
            _dock.ApplyProfileMenu(
                ProductFeatures.ProfilesEnabled ? Settings.Profiles : [],
                CurrentDiff.ProfileId);
            if (announceProfileId is not null)
            {
                var profile = Settings.Profiles.FirstOrDefault(item => item.Id == announceProfileId);
                var name = profile is null ? announceProfileId : DefaultProfiles.DisplayName(profile);
                _dock.ShowStatus(UiText.ProfileNow(name));
            }
            RevealHotkeyBindError = _shell.BindHotkeys(EffectiveHotkeys(Settings, CurrentDiff.Dock))
                ? null
                : UiText.HotkeyTaken;
            ThreadPool.QueueUserWorkItem(_ => SampleRunning());
            PulseZorder();

            PruneUnusedWidgets();
            SweepNotes();
            SyncProviders();
            if (switched)
            {
                RunProfileActions(CurrentDiff.ActionsToRun);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to apply settings.", ex);
            UserAlerts.Show(ProductInfo.Name, UiText.DockUpdateFailed);
        }
    }

    private void ApplyWidget(UIElement view, DockItem item, bool isLight)
    {
        if (view is IWidgetModeAware mode)
        {
            mode.ApplyDensity(item.Density);
        }

        if (view is IDockEdgeAware edgeAware)
        {
            edgeAware.ApplyDockEdge(CurrentDiff?.Dock.Edge ?? DockEdge.Top);
        }

        if (view is IWidgetCustomizable paper)
        {
            paper.ApplyPaper(item.PaperColor);
        }

        if (view is IClockConfigurable clock)
        {
            clock.ApplyTimeZone(item.TimeZoneId);
        }

        if (view is IFocusTimerConfigurable timer)
        {
            timer.ApplySettings(item.FocusTimer ?? FocusTimerSettings.Default);
        }

        if (view is ICountdownConfigurable countdown)
        {
            countdown.ApplySettings(item.Countdown ?? CountdownSettings.Default);
        }

        if (view is IAlarmConfigurable alarm)
        {
            alarm.ApplySettings(item.Alarm ?? AlarmSettings.Default);
        }

        if (view is IPowerWidgetConfigurable power)
        {
            power.ApplySettings(item.Power ?? PowerWidgetSettings.Default);
        }

        if (view is ITimeProgressConfigurable progress)
        {
            progress.ApplySettings(item.TimeProgress ?? TimeProgressSettings.Default);
        }

        if (view is ISysActivityConfigurable activity)
        {
            activity.ApplySettings(item.SysActivity ?? SysActivitySettings.Default);
        }

        if (view is INetworkConfigurable net)
        {
            net.ApplySettings(item.Network ?? NetworkWidgetSettings.Default);
        }

        if (view is IDiskConfigurable disk)
        {
            disk.ApplySettings(item.Disk ?? DiskWidgetSettings.Default);
        }

        if (view is IGpuConfigurable gpu)
        {
            gpu.ApplySettings(item.Gpu ?? GpuWidgetSettings.Default);
        }

        if (view is IProcessConfigurable processes)
        {
            processes.ApplySettings(item.Processes ?? ProcessWidgetSettings.Default);
        }

        if (view is IWeatherConfigurable weather)
        {
            weather.ApplySettings(item.Weather ?? WeatherSettings.Default);
        }

        if (view is IStockConfigurable stocks)
        {
            stocks.ApplySettings(item.Stocks ?? StockWidgetSettings.Default);
        }

        if (view is IWhiteNoiseConfigurable whiteNoise)
        {
            whiteNoise.ApplySettings(item.WhiteNoise ?? WhiteNoiseSettings.Default);
        }

        if (view is IScheduleConfigurable schedule)
        {
            schedule.ApplySettings(item.Schedule ?? ScheduleSettings.Default);
        }

        if (view is IWallpaperConfigurable wallpaperConfig)
        {
            wallpaperConfig.ApplySettings(item.Wallpaper ?? WallpaperSettings.Default);
        }

        if (view is IBingWallpaperConfigurable bingConfig)
        {
            bingConfig.ApplySettings(item.BingWallpaper ?? BingWallpaperSettings.Default);
        }

        if (view is INotesHistory history)
        {
            history.ApplyHistory(NotesHistory(exceptId: item.Id));
        }

        if (view is IPrivacyAware privacy)
        {
            privacy.OnPrivacyChanged();
        }

        WidgetChrome.ApplyTree(view, isLight, Settings.Ui.Backdrop);
    }

    private UIElement GetOrCreateWidget(DockItem item)
    {
        if (_widgetViews.TryGetValue(item.Id, out var view) && view is not PlaceholderWidget)
        {
            return view;
        }

        var widgetId = item.WidgetId ?? string.Empty;
        try
        {
            EnsureSampler(widgetId);
            if (widgetId == BuiltInWidgets.Notes)
            {
                Hub.Notes.TryAdoptLegacy(item.Id);
            }

            view = _views.Create(item, Hub, ApplyBingWallpaperAsync, ApplyWallpaperDesktopAsync);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Failed to create widget '{widgetId}'.", ex);
            view = new PlaceholderWidget(widgetId);
        }

        _widgetViews[item.Id] = view;
        if (view is PlaceholderWidget)
        {
            return view;
        }

        if (view is IIslandLayoutAware layout)
        {
            layout.IslandLayoutChanged += OnIslandLayoutChanged;
        }
        if (view is IWidgetCustomizable custom)
        {
            custom.PaperChanged += OnWidgetPaperChanged;
        }

        if (view is IFocusTimerConfigurable timer)
        {
            timer.SettingsChanged += OnFocusTimerChanged;
        }

        if (view is ICountdownConfigurable countdown)
        {
            countdown.SettingsChanged += OnCountdownChanged;
        }

        if (view is IAlarmConfigurable alarm)
        {
            alarm.SettingsChanged += OnAlarmChanged;
        }

        if (view is IPowerWidgetConfigurable power)
        {
            power.SettingsChanged += OnPowerWidgetChanged;
        }

        if (view is ITimeProgressConfigurable progress)
        {
            progress.SettingsChanged += OnTimeProgressChanged;
        }

        if (view is ISysActivityConfigurable activity)
        {
            activity.SettingsChanged += OnSysActivityChanged;
        }

        if (view is INetworkConfigurable net)
        {
            net.SettingsChanged += OnNetworkWidgetChanged;
        }

        if (view is IDiskConfigurable disk)
        {
            disk.SettingsChanged += OnDiskWidgetChanged;
        }

        if (view is IGpuConfigurable gpu)
        {
            gpu.SettingsChanged += OnGpuWidgetChanged;
        }

        if (view is IProcessConfigurable processes)
        {
            processes.SettingsChanged += OnProcessWidgetChanged;
        }

        if (view is IWeatherConfigurable weather)
        {
            weather.SettingsChanged += OnWeatherChanged;
        }

        if (view is IStockConfigurable stocks)
        {
            stocks.SettingsChanged += OnStockWidgetChanged;
        }

        if (view is IWhiteNoiseConfigurable whiteNoise)
        {
            whiteNoise.SettingsChanged += OnWhiteNoiseChanged;
        }

        if (view is IScheduleConfigurable schedule)
        {
            schedule.SettingsChanged += OnScheduleChanged;
        }

        if (view is IWallpaperConfigurable wallpaperConfig)
        {
            wallpaperConfig.SettingsChanged += OnWallpaperChanged;
        }

        if (view is IBingWallpaperConfigurable bingConfig)
        {
            bingConfig.SettingsChanged += OnBingWallpaperChanged;
        }

        if (view is IClockConfigurable clock)
        {
            clock.TimeZoneChanged += OnClockTimeZoneChanged;
        }

        if (view is NotesWidget notes)
        {
            notes.DeleteRequested += OnNotesDeleteRequested;
        }

        if (view is INotesHistory history)
        {
            history.RestoreRequested += OnNotesRestoreRequested;
            history.DiscardRequested += OnNotesHistoryDiscardRequested;
        }

        try
        {
            ApplyWidget(view, item, ThemeResolver.IsLight(Settings.Ui.Theme));
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to apply widget   + item.WidgetId +  .", ex);
        }

        return view;
    }

    private void PickPin(PinPickKind kind)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        if (kind == PinPickKind.InstalledApp)
        {
            var picker = new InstalledAppPicker(Hub.Shell, IsLightTheme()) { Owner = _dock };
            if (picker.ShowDialog() != true || picker.SelectedPaths.Count == 0)
            {
                return;
            }

            CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, Hub.Shell.CreatePins(picker.SelectedPaths)));
            return;
        }

        if (kind == PinPickKind.Folder)
        {
            var dialog = new OpenFolderDialog { Title = UiText.AddFolderTitle };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, [Hub.Shell.CreatePin(dialog.FolderName)]));
            return;
        }

        var files = new OpenFileDialog
        {
            Title = UiText.AddShortcutTitle,
            Filter = UiText.FileFilter,
            Multiselect = true
        };
        if (files.ShowDialog() != true || files.FileNames.Length == 0)
        {
            return;
        }

        CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, Hub.Shell.CreatePins(files.FileNames)));
    }

    private void AddDroppedPins(PinDropRequest drop)
    {
        if (_options.SafeMode || CurrentDiff is null || drop.Paths.Count == 0)
        {
            return;
        }

        var dock = CurrentDiff.Dock;
        int? insertAt = null;
        if (drop.InsertAt is int visible)
        {
            var visibleIndex = DockPinCollapse.InsertIndex(dock.Items, dock.CollapseApps, dock.CollapseFolders, visible);
            var stored = Settings.Profiles.FirstOrDefault(profile => profile.Id == CurrentDiff.ProfileId)?.Dock.Items
                ?? dock.Items;
            insertAt = SettingsEditor.MapInsertIndex(stored, dock.Items, visibleIndex);
        }

        CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, Hub.Shell.CreatePins(drop.Paths), insertAt));
    }

    private void CommitPins(AddPinsResult result)
    {
        if (result.Skipped > 0 && result.Added == 0)
        {
            UserAlerts.Show(ProductInfo.Name, UiText.AlreadyOnDock);
        }
        else if (result.Skipped > 0)
        {
            UserAlerts.Show(ProductInfo.Name, UiText.AlreadyOnDockSkipped(result.Skipped));
        }

        if (result.Added > 0)
        {
            Commit(result.Settings);
        }
    }

    private void ActivatePin(string itemId)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        var item = CurrentDiff.Dock.Items.FirstOrDefault(entry => itemId == entry.Id);
        if (item is null || !DockPins.IsPin(item.Type))
        {
            return;
        }

        Task.Run(() =>
        {
            if (!Hub.Shell.TryActivate(item))
            {
                Dispatch(() => Hub.Shell.TryLaunch(item));
            }

            SampleRunning();
        });
    }

    private void LaunchPin(string itemId)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        var item = CurrentDiff.Dock.Items.FirstOrDefault(entry => itemId == entry.Id);
        if (item is null || !DockPins.IsPin(item.Type))
        {
            return;
        }

        Hub.Shell.TryLaunch(item);
        ThreadPool.QueueUserWorkItem(_ => SampleRunning());
    }

    private void SyncRunningSampler()
    {
        if (_trackedNames.Count == 0)
        {
            _runningPins.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }

        var period = _samplingQuiet ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(3);
        _runningPins.Change(period, period);
    }

    private void SampleRunning()
    {
        if (_disposing)
        {
            return;
        }

        if (Interlocked.Exchange(ref _runningBusy, 1) != 0)
        {
            return;
        }

        try
        {
            var names = _trackedNames;
            if (names.Count == 0)
            {
                if (_lastImages.Count == 0)
                {
                    return;
                }

                _lastImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Dispatch(() =>
                {
                    if (_disposing)
                    {
                        return;
                    }

                    _runningIds = new HashSet<string>(StringComparer.Ordinal);
                    _dock.SetPinRunning(_runningIds);
                    _stackFlyout.SetRunning(_runningIds);
                });
                return;
            }

            var paths = Hub.Shell.GetRunningImagePaths(names);
            if (_lastImages.SetEquals(paths))
            {
                return;
            }

            _lastImages = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            var snapshot = paths;
            Dispatch(() =>
            {
                if (_disposing || CurrentDiff is null)
                {
                    return;
                }

                _runningIds = PinRunning.ActiveIds(CurrentDiff.Dock.Items, snapshot);
                _dock.SetPinRunning(_runningIds);
                _stackFlyout.SetRunning(_runningIds);
            });
        }
        catch (Exception)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _runningBusy, 0);
        }
    }

    private void PulseZorder()
    {
        if (_disposing || CurrentDiff is null)
        {
            return;
        }

        var peek = CurrentDiff.Dock.PeekOnPointer;
        var overWindow = _dock.IsPointerOverIsland();
        var inDock = DockHostGeometry.IsPointerOnIsland(
            overWindow,
            _dock.IsStripMode,
            WidgetFlyout.IsPointerOverOpen());
        var pointerInHot = _dock.IsPointerInHotzone() || (_dock.IsStripMode && overWindow);
        var inHot = peek && pointerInHot;
        var wake = _wakeUntil is { } until && DateTimeOffset.UtcNow < until;
        if (inDock)
        {
            _wakeUntil = null;
            wake = false;
        }

        var keepVisible = IsSettingsVisible() || WidgetFlyout.AnyOpen() || _dock.HasOpenMenu() || wake;
        if (inDock || inHot || keepVisible)
        {
            _leftHostAt = null;
        }
        else
        {
            _leftHostAt ??= DateTimeOffset.UtcNow;
        }

        var idle = CurrentDiff.Dock.AutoHide
                    && _leftHostAt is { } left
                    && DateTimeOffset.UtcNow - left >= TimeSpan.FromMilliseconds(DockTokens.AutoHideIdleMs);
        var state = _zorder.Evaluate(new DockHostContext
        {
            SafeMode = _options.SafeMode,
            DockMode = CurrentDiff.DockMode,
            AutoHide = CurrentDiff.Dock.AutoHide,
            PeekOnPointer = peek,
            IsPresentationOrFullscreen = UserNotificationState.IsPresentationOrFullscreen(_dock.Handle),
            PointerInDock = inDock,
            PointerInHotzone = pointerInHot,
            KeepVisible = keepVisible,
            IsIdle = idle,
            AppBarHealthy = _appBar.IsRegistered
        });
        if (state != _zorderState)
        {
            _zorderState = state;
            _dock.ApplyZorder(state);
        }

        var quiet = state is DockZorderState.AutoHidden or DockZorderState.FullscreenYield;
        if (!_samplingQuietSet || quiet != _samplingQuiet)
        {
            _samplingQuietSet = true;
            _samplingQuiet = quiet;
            Hub.SetQuietSampling(quiet);
            SyncRunningSampler();
        }

        if (!CurrentDiff.Dock.AutoHide)
        {
            _hostPulse.Interval = TimeSpan.FromSeconds(2);
        }
        else
        {
            var isIdleHidden = _zorderState == DockZorderState.AutoHidden && !pointerInHot && !inDock && !keepVisible;
            _hostPulse.Interval = isIdleHidden
                ? TimeSpan.FromMilliseconds(1000)
                : TimeSpan.FromMilliseconds(100);
        }
    }

    private void OnFlyoutOpenStateChanged(object? sender, EventArgs e) => Dispatch(() =>
    {
        _dock.SetTileDragSuspended(WidgetFlyout.AnyOpen());
        PulseZorder();
        WidgetFlyout.RelayoutOpen();
    });

    private void OnIslandMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        System.Windows.Point screen;
        try
        {
            screen = _dock.PointToScreen(e.GetPosition(_dock));
        }
        catch (InvalidOperationException)
        {
            if (WidgetFlyout.AnyOpen())
            {
                e.Handled = true;
            }

            return;
        }

        WidgetFlyout.TryDeliverMouseWheel(e, screen);
    }

    private bool IsSettingsVisible() => _settings is { IsVisible: true };

    private void OnHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.NextProfile:
                CycleProfile(1);
                break;
            case HotkeyAction.PreviousProfile:
                CycleProfile(-1);
                break;
            case HotkeyAction.UndoProfile:
                UndoProfile();
                break;
            case HotkeyAction.RevealDock:
                WakeDock();
                break;
        }
    }

    private void WakeDock()
    {
        if (CurrentDiff is null || !CurrentDiff.Dock.AutoHide || !CurrentDiff.Dock.RevealHotkey)
        {
            return;
        }

        RevealDock();
    }

    private void RevealDock()
    {
        if (CurrentDiff is null || !CurrentDiff.Dock.AutoHide)
        {
            return;
        }

        _wakeUntil = DateTimeOffset.UtcNow.AddMilliseconds(DockTokens.WakeHoldMs);
        _leftHostAt = null;
        PulseZorder();
    }

    private void FlushTransparency()
    {
        _transparencyDebounce.Stop();
        if (_pendingTransparency is not int value)
        {
            return;
        }

        _pendingTransparency = null;
        if (Settings.Ui.IslandTransparency == value)
        {
            return;
        }

        Commit(SettingsEditor.WithIslandTransparency(Settings, value));
    }

    public async Task ApplyBingWallpaperAsync()
    {
        var settings = Settings;
        if (!settings.Privacy.BingDownload)
        {
            var result = AskConsent(
                UiText.ApplyBingMessage,
                UiText.TodayBing);
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            Commit(SettingsEditor.WithPrivacy(Settings, Settings.Privacy with { BingDownload = true }));
            settings = Settings;
        }

        var ok = await Hub.Bing.TryApplyCurrentAsync();
        UserAlerts.Show(ProductInfo.Name, ok ? UiText.BingApplied : UiText.BingApplyFailed);
    }

    public async Task ApplyWallpaperDesktopAsync()
    {
        var ok = await Hub.Wallpaper.TryApplyCurrentDesktopAsync();
        UserAlerts.Show(ProductInfo.Name, ok ? UiText.WallpaperApplied : UiText.WallpaperApplyFailed);
    }

    private void SyncProviders()
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            Hub.Perf.Stop();
            Hub.Network.Stop();
            Hub.Media.Stop();
            Hub.Power.Stop();
            Hub.Weather.Stop();
            Hub.Disk.Stop();
            Hub.Gpu.Stop();
            Hub.Processes.Stop();
            Hub.Stocks.Stop();
            return;
        }

        var ids = CurrentDiff.Dock.Items
            .Where(item => item.Type == DockItemType.WidgetChip)
            .Select(item => item.WidgetId)
            .ToHashSet(StringComparer.Ordinal);
        var weatherSettings = CurrentDiff.Dock.Items
            .Where(item =>
                item.Type == DockItemType.WidgetChip
                && string.Equals(item.WidgetId, BuiltInWidgets.Weather, StringComparison.Ordinal))
            .Select(item => item.Weather ?? WeatherSettings.Default)
            .ToArray();
        Hub.Weather.AllowAutoLocate = Settings.Privacy.PreciseLocation;
        Hub.Weather.Watch(weatherSettings);
        var stockSymbols = CurrentDiff.Dock.Items
            .Where(item =>
                item.Type == DockItemType.WidgetChip
                && string.Equals(item.WidgetId, BuiltInWidgets.Stocks, StringComparison.Ordinal))
            .SelectMany(item => StockWidgetSettings.AllSymbols(item.Stocks ?? StockWidgetSettings.Default))
            .ToArray();
        Hub.Stocks.Watch(stockSymbols);
        Toggle(ids.Contains(BuiltInWidgets.SysActivity), Hub.Perf.Start, Hub.Perf.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Network), Hub.Network.Start, Hub.Network.Stop);
        Toggle(ids.Contains(BuiltInWidgets.NowPlaying), Hub.Media.Start, Hub.Media.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Power), Hub.Power.Start, Hub.Power.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Disk), Hub.Disk.Start, Hub.Disk.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Gpu), Hub.Gpu.Start, Hub.Gpu.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Processes) && Settings.Privacy.ProcessNames, Hub.Processes.Start, Hub.Processes.Stop);
        Toggle(weatherSettings.Length > 0, Hub.Weather.Start, Hub.Weather.Stop);
        Toggle(stockSymbols.Length > 0, Hub.Stocks.Start, Hub.Stocks.Stop);
        var bingItem = CurrentDiff.Dock.Items.FirstOrDefault(item =>
            item.Type == DockItemType.WidgetChip
            && string.Equals(item.WidgetId, BuiltInWidgets.BingWallpaper, StringComparison.Ordinal));
        if (bingItem?.BingWallpaper is not null)
        {
            Hub.Bing.AutoApplyDaily = bingItem.BingWallpaper.AutoApplyDaily;
        }
    }

    private static void Toggle(bool on, Action start, Action stop)
    {
        if (on)
        {
            start();
        }
        else
        {
            stop();
        }
    }

    private static string ItemLayoutKey(IEnumerable<DockItem> items, bool collapseApps, bool collapseFolders) =>
        $"{(collapseApps ? 1 : 0)}|{(collapseFolders ? 1 : 0)};" +
        string.Join(";", items.Select(item =>
            $"{item.Id}|{item.Type}|{item.Path}|{item.ShortcutPath}|{item.Url}|{item.WidgetId}|{item.Title}|{item.Density}|{item.TimeZoneId}"));

    private RectLogical IslandBounds()
    {
        static double Finite(double value) => double.IsFinite(value) ? value : 0;
        var width = _dock.ActualWidth > 1 ? _dock.ActualWidth : _dock.Width;
        var height = _dock.ActualHeight > 1 ? _dock.ActualHeight : _dock.Height;
        return new RectLogical(Finite(_dock.Left), Finite(_dock.Top), Finite(width), Finite(height));
    }

    private void TogglePinStack(PinStackToggleRequest request)
    {
        if (_options.SafeMode || CurrentDiff is null || request.Members.Count < 2)
        {
            _stackFlyout.Hide();
            return;
        }

        var monitor = _layout.ResolveMonitor(Topology, CurrentDiff.Dock.Monitor);
        _stackFlyout.Toggle(
            request.Target,
            request.Kind,
            request.Members,
            PinIcon,
            ThemeResolver.IsLight(Settings.Ui.Theme),
            Settings.Ui.Backdrop,
            _runningIds,
            monitor.WorkArea,
            IslandBounds());
    }

    private ImageSource? StockIcon(DockPinStackKind kind)
    {
        try
        {
            var path = Hub.Shell.GetStockIconFile(kind);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
            if (_pinIcons.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 48;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            _pinIcons[key] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ImageSource? PinIcon(DockItem item)
    {
        try
        {
            var path = Hub.Shell.GetIconFile(item);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
            if (_pinIcons.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 48;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            _pinIcons[key] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnWidgetRemove(string itemId)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var item = CurrentDiff.Dock.Items.FirstOrDefault(entry => entry.Id == itemId);
        if (item is not null && SettingsEditor.IsNotesChip(item))
        {
            if (_widgetViews.TryGetValue(itemId, out var view) && view is NotesWidget notes)
            {
                notes.Flush();
            }

            if (NotesText.IsBlank(Hub.Notes.Load(itemId)))
            {
                var deleteBlank = MessageBox.Show(
                    UiText.ConfirmDeleteBlankNote,
                    BuiltInWidgets.DisplayName(BuiltInWidgets.Notes),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (deleteBlank != MessageBoxResult.OK)
                {
                    return;
                }

                if (_widgetViews.TryGetValue(itemId, out var empty) && empty is NotesWidget blank)
                {
                    blank.Discard();
                }

                Commit(SettingsEditor.RemoveDockItem(Settings, CurrentDiff.ProfileId, itemId));
                DeleteNoteFile(itemId);
                return;
            }

            Commit(SettingsEditor.HideDockItem(Settings, CurrentDiff.ProfileId, itemId));
            return;
        }

        if (item is not null && item.WidgetId == BuiltInWidgets.Schedule)
        {
            var confirm = MessageBox.Show(
                UiText.ConfirmRemoveSchedule,
                BuiltInWidgets.DisplayName(BuiltInWidgets.Schedule),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK)
            {
                return;
            }
        }
        else if (item is not null)
        {
            var confirm = MessageBox.Show(
                UiText.ConfirmRemoveFromDock(RemoveLabel(item)),
                ProductInfo.Name,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK)
            {
                return;
            }
        }

        Commit(SettingsEditor.RemoveDockItem(Settings, CurrentDiff.ProfileId, itemId));
    }

    private static string RemoveLabel(DockItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.WidgetId))
        {
            return BuiltInWidgets.DisplayName(item.WidgetId);
        }

        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return item.Title;
        }

        return UiText.PinFallback;
    }

    private void OnNotesDeleteRequested(object? sender, EventArgs e)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (sender is NotesWidget notes)
        {
            notes.Discard();
        }

        Commit(SettingsEditor.RemoveDockItem(Settings, CurrentDiff.ProfileId, itemId));
        DeleteNoteFile(itemId);
    }

    private void DeleteNoteFile(string key)
    {
        try
        {
            Hub.Notes.Delete(key);
        }
        catch (Exception)
        {
            // Sweep on the next Apply will retry.
        }
    }

    private void SweepNotes()
    {
        if (_store.SkipNotesSweep || _store.IsImmutable)
        {
            return;
        }

        try
        {
            Hub.Notes.Sweep(SettingsEditor.LiveNoteKeys(Settings));
        }
        catch (Exception)
        {
            // Notes files are not required for the dock to stay up.
        }
    }

    private void OnWidgetPaperChanged(object? sender, string paper)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var currentItem = CurrentDiff.Dock.Items.FirstOrDefault(i => i.Id == itemId);
        if (currentItem is not null && string.Equals(currentItem.PaperColor, paper, StringComparison.Ordinal))
        {
            return;
        }

        Commit(SettingsEditor.WithWidgetPaperColor(Settings, CurrentDiff.ProfileId, itemId, paper));
    }

    private void OnFocusTimerChanged(object? sender, FocusTimerSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithFocusTimer(Settings, CurrentDiff.ProfileId, itemId, settings));
    }

    private void OnCountdownChanged(object? sender, CountdownSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithCountdown(Settings, CurrentDiff.ProfileId, itemId, settings));
    }

    private void OnAlarmChanged(object? sender, AlarmSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithAlarm(Settings, CurrentDiff.ProfileId, itemId, settings));
    }

    private void OnClockTimeZoneChanged(object? sender, string? timeZoneId)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithClockTimeZone(Settings, CurrentDiff.ProfileId, itemId, timeZoneId));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPowerWidgetChanged(object? sender, PowerWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithPowerWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimeProgressChanged(object? sender, TimeProgressSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithTimeProgress(Settings, CurrentDiff.ProfileId, itemId, settings));
    }

    private void OnSysActivityChanged(object? sender, SysActivitySettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithSysActivity(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnNetworkWidgetChanged(object? sender, NetworkWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithNetworkWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDiskWidgetChanged(object? sender, DiskWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithDiskWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnGpuWidgetChanged(object? sender, GpuWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithGpuWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessWidgetChanged(object? sender, ProcessWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithProcessWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
    }

    private void OnStockWidgetChanged(object? sender, StockWidgetSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithStockWidget(Settings, CurrentDiff.ProfileId, itemId, settings));
        if (CurrentDiff is not null)
        {
            var symbols = CurrentDiff.Dock.Items
                .Where(item =>
                    item.Type == DockItemType.WidgetChip
                    && string.Equals(item.WidgetId, BuiltInWidgets.Stocks, StringComparison.Ordinal))
                .SelectMany(item => StockWidgetSettings.AllSymbols(item.Stocks ?? StockWidgetSettings.Default))
                .ToArray();
            Hub.Stocks.Watch(symbols);
            Hub.Stocks.Start();
        }

        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWhiteNoiseChanged(object? sender, WhiteNoiseSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithWhiteNoise(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWeatherChanged(object? sender, WeatherSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var next = WeatherSettings.Normalize(settings);
        if (next.AutoLocate && !Settings.Privacy.PreciseLocation)
        {
            if (AskConsent(UiText.ConsentLocation, BuiltInWidgets.DisplayName(BuiltInWidgets.Weather)) != MessageBoxResult.OK)
            {
                next = next with { AutoLocate = false };
                if (sender is IWeatherConfigurable weather)
                {
                    weather.ApplySettings(next);
                }
            }
            else
            {
                var privacy = Settings.Privacy with { PreciseLocation = true };
                _gate.UpdatePrivacy(privacy);
                Hub.Weather.AllowAutoLocate = true;
                var withPrivacy = SettingsEditor.WithPrivacy(Settings, privacy);
                SaveWithoutRelayout(SettingsEditor.WithWeather(withPrivacy, CurrentDiff.ProfileId, itemId, next));
                RelayoutIsland();
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        Hub.Weather.AllowAutoLocate = Settings.Privacy.PreciseLocation;
        SaveWithoutRelayout(SettingsEditor.WithWeather(Settings, CurrentDiff.ProfileId, itemId, next));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnScheduleChanged(object? sender, ScheduleSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithSchedule(Settings, CurrentDiff.ProfileId, itemId, settings));
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnBingWallpaperChanged(object? sender, BingWallpaperSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        Hub.Bing.AutoApplyDaily = settings.AutoApplyDaily;
        SaveWithoutRelayout(SettingsEditor.WithBingWallpaper(Settings, CurrentDiff.ProfileId, itemId, settings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnWallpaperChanged(object? sender, WallpaperSettings settings)
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var itemId = _widgetViews.FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithWallpaper(Settings, CurrentDiff.ProfileId, itemId, settings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyWidgetDensity(WidgetDensityRequest request)
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            return;
        }

        var item = CurrentDiff.Dock.Items.FirstOrDefault(current => current.Id == request.ItemId);
        if (item is null || item.Density == request.Density)
        {
            return;
        }

        SaveWithoutRelayout(SettingsEditor.WithWidgetDensity(Settings, CurrentDiff.ProfileId, request.ItemId, request.Density));
        if (_widgetViews.TryGetValue(request.ItemId, out var view) && view is IWidgetModeAware mode)
        {
            mode.ApplyDensity(request.Density);
        }

        if (_widgetViews.TryGetValue(request.ItemId, out var edged) && edged is IDockEdgeAware edgeAware && CurrentDiff is not null)
        {
            edgeAware.ApplyDockEdge(CurrentDiff.Dock.Edge);
        }

        _dock.ApplyWidgetDensity(request.ItemId, request.Density);
        RelayoutIsland();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RelayoutIsland()
    {
        if (CurrentDiff is null)
        {
            return;
        }

        var snapshot = ContextAssembler.Create(_options, Settings, Hub, _display.Current);
        var monitor = _layout.ResolveMonitor(snapshot.Topology, CurrentDiff.Dock.Monitor);
        var thickness = DockLayoutCalculator.ThicknessFor(CurrentDiff.Dock.Edge);
        RelayoutIsland(monitor, thickness);
    }

    private void RelayoutIsland(MonitorInfo monitor, double thickness)
    {
        _dock.SetHostWorkArea(monitor.PixelWorkArea);
        var measured = _dock.MeasureIsland();
        var maxLength = CurrentDiff.Dock.Edge is DockEdge.Left or DockEdge.Right
            ? Math.Max(1, monitor.WorkArea.Height - (2 * DockTokens.IslandMargin))
            : Math.Max(1, monitor.WorkArea.Width - (2 * DockTokens.IslandMargin));
        var bounds = _layout.GetIslandBounds(
            monitor,
            CurrentDiff.Dock.Edge,
            thickness,
            measured.Width,
            measured.Height,
            CurrentDiff.Dock.IslandLength);
        _dock.ApplyLayout(bounds, CurrentDiff.Dock.Edge, DockTokens.MinIslandLength, maxLength);
        if (!_options.SafeMode && CurrentDiff.DockMode == DockMode.AppBar)
        {
            var reg = _appBar.TryRegister(new AppBarRequest
            {
                WindowHandle = _dock.Handle,
                Edge = CurrentDiff.Dock.Edge,
                Proposed = bounds
            });
            if (!reg.Succeeded)
            {
                AppLog.Write($"AppBar registration failed, falling back to overlay: {reg.FailureReason}");
            }
        }
        else
        {
            _appBar.Remove();
        }
    }

    private void SaveWithoutRelayout(AppSettings next)
    {
        try
        {
            _store.Save(next);
            Settings = SettingsValidator.EnsureInvariants(next);
            var snapshot = ContextAssembler.Create(_options, Settings, Hub, _display.Current);
            CurrentDiff = _engine.Evaluate(Settings, snapshot);
            _gate.SetActiveProfile(CurrentDiff.Permissions);
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to save settings.", ex);
            UserAlerts.Show(ProductInfo.Name, UiText.SaveFailed);
            Apply(Settings);
        }
    }

    private void PrunePinIcons()
    {
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in CurrentDiff.Dock.Items)
        {
            if (!DockPins.IsPin(item.Type))
            {
                continue;
            }

            try
            {
                var path = Hub.Shell.GetIconFile(item);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                live.Add(path + "|" + File.GetLastWriteTimeUtc(path).Ticks);
            }
            catch (Exception)
            {
            }
        }

        foreach (var key in _pinIcons.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            _pinIcons.Remove(key);
        }
    }

    private void RunProfileActions(IReadOnlyList<ProfileAction> actions)
    {
        foreach (var action in actions)
        {
            try
            {
                if (action.Kind == ActionKind.Notify)
                {
                    UserAlerts.Show(ProductInfo.Name, string.IsNullOrWhiteSpace(action.Message) ? UiText.ProfileSwitched : action.Message);
                    continue;
                }

                if (action.Kind != ActionKind.Launch || string.IsNullOrWhiteSpace(action.Target))
                {
                    continue;
                }

                var target = action.Target.Trim();
                if (!_launchConfirmed.Contains(target))
                {
                    var confirm = MessageBox.Show(
                        UiText.ProfileWantsLaunch(target),
                        ProductInfo.Name,
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.OK)
                    {
                        continue;
                    }

                    _launchConfirmed.Add(target);
                }

                var pin = Hub.Shell.CreatePin(target);
                if (!Hub.Shell.TryLaunch(pin))
                {
                    AppLog.Write($"Profile launch failed: {target}");
                    UserAlerts.Show(ProductInfo.Name, UiText.ProfileLaunchFailed);
                    try
                    {
                        Settings = SettingsEditor.RecordSwitch(
                            Settings,
                            CurrentDiff.ProfileId,
                            CurrentDiff.ProfileId,
                            CurrentDiff.Reason,
                            ProfileSwitchResult.Failed,
                            "launch failed");
                        _store.Save(Settings);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("Failed to record launch failure.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Profile action failed.", ex);
            }
        }
    }

    private void PulseAlarms()
    {
        if (_disposing || _options.SafeMode)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var settings = Settings;
        var changed = false;
        var messages = new List<string>();
        foreach (var profile in Settings.Profiles)
        {
            foreach (var item in profile.Dock.Items)
            {
                if (!string.Equals(item.WidgetId, BuiltInWidgets.Alarm, StringComparison.Ordinal))
                {
                    continue;
                }

                var alarm = AlarmSettings.Normalize(item.Alarm);
                if (!alarm.Armed)
                {
                    continue;
                }

                if (_widgetViews.TryGetValue(item.Id, out var view) && view is AlarmWidget widget && widget.IsAlive)
                {
                    continue;
                }

                var updated = AlarmEngine.Resolve(alarm, now, TimeZoneInfo.Local, out var ring);
                if (!ring && AlarmSettings.Same(updated, alarm))
                {
                    continue;
                }

                settings = SettingsEditor.WithAlarm(settings, profile.Id, item.Id, updated);
                changed = true;
                if (ring)
                {
                    messages.Add(AlarmFormat.FinishedMessage(alarm.Label));
                }
            }
        }

        if (!changed)
        {
            return;
        }

        Commit(settings);
        if (messages.Count == 0)
        {
            return;
        }

        SystemSounds.Asterisk.Play();
        var title = BuiltInWidgets.DisplayName(BuiltInWidgets.Alarm);
        foreach (var message in messages)
        {
            UserAlerts.Show(title, message);
        }
    }

    private void PruneUnusedWidgets()
    {
        var wanted = CurrentDiff.Dock.Items
            .Where(item => item.Type == DockItemType.WidgetChip)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in _widgetViews.Keys.Except(wanted, StringComparer.Ordinal).ToArray())
        {
            if (_widgetViews.TryGetValue(id, out var view))
            {
                var stillConfigured = Settings.Profiles.SelectMany(profile => profile.Dock.Items)
                    .Any(item => item.Id == id);
                if (view is IKeepAliveWhileRunning { IsAlive: true } && stillConfigured)
                {
                    continue;
                }

                if (view is IWidgetDetachable detachable)
                {
                    detachable.Detach();
                }

                if (view is IIslandLayoutAware layout)
                {
                    layout.IslandLayoutChanged -= OnIslandLayoutChanged;
                }

                if (view is IWidgetCustomizable custom)
                {
                    custom.PaperChanged -= OnWidgetPaperChanged;
                }

                if (view is IFocusTimerConfigurable timer)
                {
                    timer.SettingsChanged -= OnFocusTimerChanged;
                }

                if (view is ICountdownConfigurable countdown)
                {
                    countdown.SettingsChanged -= OnCountdownChanged;
                }

                if (view is IAlarmConfigurable alarm)
                {
                    alarm.SettingsChanged -= OnAlarmChanged;
                }

                if (view is IClockConfigurable clock)
                {
                    clock.TimeZoneChanged -= OnClockTimeZoneChanged;
                }

                if (view is IPowerWidgetConfigurable power)
                {
                    power.SettingsChanged -= OnPowerWidgetChanged;
                }

                if (view is ITimeProgressConfigurable progress)
                {
                    progress.SettingsChanged -= OnTimeProgressChanged;
                }

                if (view is ISysActivityConfigurable activity)
                {
                    activity.SettingsChanged -= OnSysActivityChanged;
                }

                if (view is INetworkConfigurable net)
                {
                    net.SettingsChanged -= OnNetworkWidgetChanged;
                }

                if (view is IDiskConfigurable disk)
                {
                    disk.SettingsChanged -= OnDiskWidgetChanged;
                }

                if (view is IGpuConfigurable gpu)
                {
                    gpu.SettingsChanged -= OnGpuWidgetChanged;
                }

                if (view is IProcessConfigurable processes)
                {
                    processes.SettingsChanged -= OnProcessWidgetChanged;
                }

                if (view is IWeatherConfigurable weather)
                {
                    weather.SettingsChanged -= OnWeatherChanged;
                }

                if (view is IStockConfigurable stocks)
                {
                    stocks.SettingsChanged -= OnStockWidgetChanged;
                }

                if (view is IWhiteNoiseConfigurable whiteNoise)
                {
                    whiteNoise.SettingsChanged -= OnWhiteNoiseChanged;
                }

                if (view is IScheduleConfigurable schedule)
                {
                    schedule.SettingsChanged -= OnScheduleChanged;
                }

                if (view is IBingWallpaperConfigurable bingConfig)
                {
                    bingConfig.SettingsChanged -= OnBingWallpaperChanged;
                }

                if (view is NotesWidget notes)
                {
                    notes.DeleteRequested -= OnNotesDeleteRequested;
                }

                if (view is INotesHistory history)
                {
                    history.RestoreRequested -= OnNotesRestoreRequested;
                    history.DiscardRequested -= OnNotesHistoryDiscardRequested;
                }
            }

            _widgetViews.Remove(id);
        }
    }
}

public static class ThemeResolver
{
    public static bool IsLight(ThemePreference theme)
    {
        if (theme == ThemePreference.Light)
        {
            return true;
        }

        if (theme == ThemePreference.Dark)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public static class ContextAssembler
{
    public static ContextSnapshot Create(
        IStartupOptions options,
        AppSettings settings,
        IProviderHub hub,
        DisplayTopologySnapshot topology)
    {
        return new ContextSnapshot
        {
            UtcNow = hub.Clock.UtcNow,
            LocalNow = hub.Clock.LocalNow,
            FocusSupported = hub.Focus.IsSupported,
            FocusActive = hub.Focus.IsFocusActive,
            Topology = topology,
            SafeMode = options.SafeMode,
            ProfilesEnabled = ProductFeatures.ProfilesEnabled,
            AutoProfileTriggersEnabled = ProductFeatures.AutoProfileTriggersEnabled,
            ManualOverrideProfileId = settings.ManualOverrideProfileId,
            ManualOverrideUntil = settings.ManualOverride.Until,
            ManualOverrideFocusActive = settings.ManualOverrideFocusActive,
            ManualOverrideTopologyKey = settings.ManualOverrideTopologyKey
        };
    }
}

public sealed class StartupOptions : IStartupOptions
{
    public bool SafeMode { get; init; }

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var safe = args.Any(arg => string.Equals(arg, "--safe", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(arg, "/safe", StringComparison.OrdinalIgnoreCase));
        return new StartupOptions { SafeMode = safe };
    }
}
