using System.IO;
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
    private readonly ProfileEngine _engine = new();
    private readonly WidgetViewRegistry _views = new();
    private readonly DockWindow _dock = new();
    private readonly PinStackFlyout _stackFlyout = new();
    private readonly System.Threading.Timer _runningPins;
    private readonly ShellHost _shell;
    private readonly DispatcherTimer _hostPulse = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _schedulePulse = new() { Interval = TimeSpan.FromSeconds(30) };
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
        Hub = new ProviderHub(_gate, new FileNotesStore(), AppPaths.BingWallpaperDirectory, AppPaths.IconCacheDirectory);
        Hub.Media.CaptureMetadata = _gate.Allows(PermissionIds.MediaRead);
        _display = new WinFormsDisplayService();
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

    public void Start()
    {
        _dock.SettingsRequested += (_, _) => ShowSettings();
        _dock.ExitRequested += (_, _) => Application.Current.Shutdown();
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
            Commit(SettingsEditor.ReplaceDockItems(Settings, CurrentDiff.ProfileId, next));
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
        _display.Changed += (_, _) => Dispatch(() => Apply(Settings));
        Hub.Focus.IsFocusActiveChanged += (_, _) => Dispatch(() => Apply(Settings));
        _shell.DisplayChanged += (_, _) => _display.RequestRefresh();
        _shell.ZorderHint += (_, _) => Dispatch(PulseZorder);
        _shell.HotkeyPressed += (_, action) => Dispatch(() => OnHotkey(action));
        _shell.TaskbarCreated += (_, _) => Dispatch(() => TaskbarCreated?.Invoke(this, EventArgs.Empty));
        WidgetFlyout.Elevate = PopupTopmost.TryElevate;
        WidgetFlyout.OpenStateChanged += OnFlyoutOpenStateChanged;
        _stackFlyout.PinActivateRequested += (_, itemId) => ActivatePin(itemId);
        _stackFlyout.PinLaunchRequested += (_, itemId) => LaunchPin(itemId);
        _stackFlyout.PinRemoveRequested += (_, itemId) => OnWidgetRemove(itemId);
        Apply(Settings);
        _hostPulse.Start();
        if (ProductFeatures.AutoProfileTriggersEnabled)
        {
            _schedulePulse.Start();
        }

        _dock.Show();
        if (!string.IsNullOrWhiteSpace(_loadMessage))
        {
            MessageBox.Show(_loadMessage, "WinDock", MessageBoxButton.OK, MessageBoxImage.Information);
            _loadMessage = null;
        }
    }

    public void ShowSettings()
    {
        if (_options.SafeMode)
        {
            MessageBox.Show("当前是安全模式：已停用模块、设置和拖放。退出后以正常方式启动即可恢复。", "WinDock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_settings is null)
        {
            _settings = new SettingsWindow(this);
            _settings.IsVisibleChanged += (_, _) => Dispatch(PulseZorder);
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
        if (string.Equals(CurrentDiff.Dock.Monitor, next, StringComparison.OrdinalIgnoreCase))
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
            RevealHotkeyBindError = "快捷键已被占用，请改成别的组合";
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
            RevealHotkeyBindError = "快捷键已被占用，请改成别的组合";
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
        if (!TryConsentForWidget(widgetId, out var next))
        {
            return;
        }

        Commit(SettingsEditor.AddWidget(next, CurrentDiff.ProfileId, widgetId));
    }

    public void RemoveLastWidget(string widgetId)
    {
        if (CurrentDiff is null || string.IsNullOrWhiteSpace(widgetId))
        {
            return;
        }

        var item = CurrentDiff.Dock.Items.LastOrDefault(entry =>
            entry.Type == DockItemType.WidgetChip
            && string.Equals(entry.WidgetId, widgetId, StringComparison.Ordinal));
        if (item is null)
        {
            return;
        }

        OnWidgetRemove(item.Id);
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
            BuiltInWidgets.NowPlaying => "将读取正在播放的歌曲名、封面和播放器名称。可以在设置的隐私页随时关闭。",
            BuiltInWidgets.Network => "将显示网络适配器名称和上下行速率。数据只留在本机，不会上传。",
            BuiltInWidgets.BingWallpaper => "将访问 bing.com 下载壁纸图。设为桌面时会再确认一次。",
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

        if (widgetId == BuiltInWidgets.BingWallpaper && settings.Privacy.BingDownload)
        {
            return true;
        }

        var result = AskConsent(message, "添加" + BuiltInWidgets.DisplayName(widgetId));
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
        Commit(next);
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
        _transparencyDebounce.Stop();
        _runningPins.Dispose();
        _stackFlyout.Hide();
        _widgetViews.Clear();
        WidgetFlyout.OpenStateChanged -= OnFlyoutOpenStateChanged;
        _dock.IslandMouseWheel -= OnIslandMouseWheel;
        _dock.Dispose();
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
            UserAlerts.Show("WinDock", "未能保存设置，已记入日志。");
            Apply(Settings);
        }
    }

    private void Apply(AppSettings settings)
    {
        try
        {
            Settings = SettingsValidator.EnsureInvariants(settings);
            var snapshot = ContextAssembler.Create(_options, Settings, Hub, _display.Current);
            CurrentDiff = _engine.Evaluate(Settings, snapshot);
            var switched = ProductFeatures.ProfilesEnabled && _appliedProfileId != CurrentDiff.ProfileId;
            if (switched)
            {
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
                    if (view is IWidgetModeAware mode)
                    {
                        mode.ApplyDensity(item.Density);
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
            }

            RelayoutIsland(monitor, thickness);
            _dock.ApplyTheme(isLight, Settings.Ui.Backdrop, Settings.Ui.IslandTransparency);
            _stackFlyout.ApplyTheme(isLight, Settings.Ui.Backdrop);
            _dock.ApplyProfileMenu(
                ProductFeatures.ProfilesEnabled ? Settings.Profiles : [],
                CurrentDiff.ProfileId);
            RevealHotkeyBindError = _shell.BindHotkeys(EffectiveHotkeys(Settings, CurrentDiff.Dock))
                ? null
                : "快捷键已被占用，请改成别的组合";
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
            UserAlerts.Show("WinDock", "未能更新 Dock，已记入日志。");
        }
    }

    private UIElement GetOrCreateWidget(DockItem item)
    {
        if (_widgetViews.TryGetValue(item.Id, out var view))
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

            view = _views.Create(item, Hub);
        }
        catch (Exception)
        {
            view = new PlaceholderWidget(BuiltInWidgets.DisplayName(widgetId));
        }

        _widgetViews[item.Id] = view;
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
            var dialog = new OpenFolderDialog { Title = "添加文件夹" };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, [Hub.Shell.CreatePin(dialog.FolderName)]));
            return;
        }

        var files = new OpenFileDialog
        {
            Title = "添加快捷方式",
            Filter = "快捷方式与程序|*.lnk;*.exe|所有文件|*.*",
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
        var insertAt = drop.InsertAt is int visible
            ? DockPinCollapse.InsertIndex(dock.Items, dock.CollapseApps, dock.CollapseFolders, visible)
            : (int?)null;
        CommitPins(SettingsEditor.AddPins(Settings, CurrentDiff.ProfileId, Hub.Shell.CreatePins(drop.Paths), insertAt));
    }

    private void CommitPins(AddPinsResult result)
    {
        if (result.Skipped > 0 && result.Added == 0)
        {
            UserAlerts.Show("WinDock", "已在 Dock 上。");
        }
        else if (result.Skipped > 0)
        {
            UserAlerts.Show("WinDock", $"已在 Dock 上（跳过 {result.Skipped} 项）。");
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

        if (!Hub.Shell.TryActivate(item))
        {
            Hub.Shell.TryLaunch(item);
        }

        ThreadPool.QueueUserWorkItem(_ => SampleRunning());
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

        _runningPins.Change(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
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
            IsIdle = idle
        });
        if (state != _zorderState)
        {
            _zorderState = state;
            _dock.ApplyZorder(state);
        }

        _hostPulse.Interval = CurrentDiff.Dock.AutoHide
            ? TimeSpan.FromMilliseconds(100)
            : TimeSpan.FromSeconds(2);
    }

    private void OnFlyoutOpenStateChanged(object? sender, EventArgs e) => Dispatch(() =>
    {
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
                "将访问 bing.com 下载壁纸图，并把它设为桌面。",
                "今日 Bing 壁纸");
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            Commit(SettingsEditor.WithPrivacy(Settings, Settings.Privacy with { BingDownload = true }));
            settings = Settings;
        }

        var ok = await Hub.Bing.TryApplyTodayAsync();
        UserAlerts.Show("WinDock", ok ? "已设为今日 Bing 壁纸。" : "未能应用 Bing 壁纸。");
    }

    private void SyncProviders()
    {
        if (_options.SafeMode || CurrentDiff is null)
        {
            Hub.Perf.Stop();
            Hub.Network.Stop();
            Hub.Media.Stop();
            Hub.Power.Stop();
            return;
        }

        var ids = CurrentDiff.Dock.Items
            .Where(item => item.Type == DockItemType.WidgetChip)
            .Select(item => item.WidgetId)
            .ToHashSet(StringComparer.Ordinal);
        Toggle(ids.Contains(BuiltInWidgets.SysActivity), Hub.Perf.Start, Hub.Perf.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Network), Hub.Network.Start, Hub.Network.Stop);
        Toggle(ids.Contains(BuiltInWidgets.NowPlaying), Hub.Media.Start, Hub.Media.Stop);
        Toggle(ids.Contains(BuiltInWidgets.Power), Hub.Power.Start, Hub.Power.Stop);
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

    private void TogglePinStack(PinStackToggleRequest request)
    {
        if (_options.SafeMode || CurrentDiff is null || request.Members.Count < 2)
        {
            _stackFlyout.Hide();
            return;
        }

        _stackFlyout.Toggle(
            request.Target,
            request.Kind,
            request.Members,
            PinIcon,
            ThemeResolver.IsLight(Settings.Ui.Theme),
            Settings.Ui.Backdrop,
            _runningIds);
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

        Commit(SettingsEditor.RemoveDockItem(Settings, CurrentDiff.ProfileId, itemId));
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
            UserAlerts.Show("WinDock", "未能保存设置，已记入日志。");
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
                    UserAlerts.Show("WinDock", string.IsNullOrWhiteSpace(action.Message) ? "情景已切换" : action.Message);
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
                        $"情景要启动：{target}",
                        "WinDock",
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
                    UserAlerts.Show("WinDock", "情景未能启动程序，已记入日志。");
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
