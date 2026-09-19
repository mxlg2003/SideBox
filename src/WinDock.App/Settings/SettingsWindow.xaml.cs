using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinDock.Core;
using WinDock.Core.Permissions;
using WinDock.Infrastructure;

namespace WinDock.App;

public partial class SettingsWindow : Window
{
    private readonly DockSession _session;
    private readonly EventHandler _onChanged;
    private bool _suppress;
    private bool _allowClose;
    private bool _listening;
    private bool _pickerDirty = true;
    private string _page = "modules";
    private bool _capturingRevealHotkey;
    private string _revealHotkeyBeforeCapture = "";

    public SettingsWindow(DockSession session)
    {
        _session = session;
        _onChanged = (_, _) => Dispatcher.BeginInvoke(OnSessionChanged, DispatcherPriority.Background);
        InitializeComponent();
        NavColumn.Width = new GridLength(DockTokens.Settings.NavWidth);
        var advanced = ProductFeatures.ProfilesEnabled ? Visibility.Visible : Visibility.Collapsed;
        AdvancedGroup.Visibility = advanced;
        NavProfiles.Visibility = advanced;
        WidgetPicker.Attach(_session);
        IsVisibleChanged += OnIsVisibleChanged;
        Closing += (_, e) =>
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;
            Hide();
        };
        NavModules.IsChecked = true;
        ShowPage("modules");
        Reload();
    }

    public void AllowClose() => _allowClose = true;

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Attach();
            WidgetPicker.SetCatalogActive(IsModulesPage());
        }
        else
        {
            Detach();
            WidgetPicker.SetCatalogActive(false);
        }
    }

    private void Attach()
    {
        if (_listening)
        {
            return;
        }

        _session.Changed += _onChanged;
        _listening = true;
    }

    private void Detach()
    {
        if (!_listening)
        {
            return;
        }

        _session.Changed -= _onChanged;
        _listening = false;
    }

    private void OnSessionChanged()
    {
        if (!IsVisible)
        {
            _pickerDirty = true;
            return;
        }

        Reload();
    }

    public void Reload()
    {
        _suppress = true;
        var settings = _session.Settings;
        var diff = _session.CurrentDiff;
        SelectTag(ThemeBox, settings.Ui.Theme.ToString());
        SelectTag(BackdropBox, settings.Ui.Backdrop.ToString());
        TransparencySlider.Value = settings.Ui.IslandTransparency;
        TransparencyLabel.Text = $"{settings.Ui.IslandTransparency}%";
        SelectTag(EdgeBox, diff.Dock.Edge.ToString());
        ReloadMonitors(diff.Dock.Monitor);
        MediaCheck.IsChecked = settings.Privacy.MediaRead;
        NetworkCheck.IsChecked = settings.Privacy.NetworkDetails;
        BingCheck.IsChecked = settings.Privacy.BingDownload;
        AutoHideCheck.IsChecked = diff.Dock.AutoHide;
        PeekCheck.IsChecked = diff.Dock.PeekOnPointer;
        RevealHotkeyCheck.IsChecked = diff.Dock.RevealHotkey;
        CollapseAppsCheck.IsChecked = diff.Dock.CollapseApps;
        CollapseFoldersCheck.IsChecked = diff.Dock.CollapseFolders;
        if (!_capturingRevealHotkey)
        {
            RevealHotkeyBox.Text = string.IsNullOrWhiteSpace(settings.Hotkeys.RevealDock)
                ? "Win+Shift+A"
                : settings.Hotkeys.RevealDock;
            RevealHotkeyHint.Text = _session.RevealHotkeyBindError ?? "";
        }

        SyncAutoHideDependents();
        if (ProductFeatures.ProfilesEnabled)
        {
            HotkeyText.Text = $"当前快捷键（暂不能改）：下一个 {settings.Hotkeys.NextProfile ?? "无"}，撤销 {settings.Hotkeys.UndoProfile ?? "无"}。托盘右键也可切换。";
            FocusHint.Text = "模块里的「专注计时器」是本地番茄钟，不会打开系统专注，也不会自动换情景。";
            DeleteProfileButton.IsEnabled = diff.ProfileId != DefaultProfiles.GeneralId;
            DeleteProfileButton.ToolTip = diff.ProfileId == DefaultProfiles.GeneralId
                ? "日常是兜底情景，不能删除"
                : "删除当前情景";
            HistoryList.Items.Clear();
            foreach (var record in settings.SwitchHistory.Reverse().Take(ProfileSwitchLog.MaxRecords))
            {
                HistoryList.Items.Add(ProfileSwitchLog.Summarize(record, settings.Profiles));
            }

            ReloadProfileBox(settings.Profiles, diff.ProfileId);
            UndoButton.IsEnabled = SettingsEditor.UndoTarget(settings, diff.ProfileId) is not null;
        }

        StartupCheck.IsChecked = RunAtStartup.IsEnabled();
        AboutVersion.Text = $"WinDock {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0"}";
        AboutPaths.Text = $"配置：{AppPaths.SettingsFile}{Environment.NewLine}日志：{AppPaths.LogsDirectory}";
        MediaHint.Text = MediaGrantHint(settings, diff.ProfileName);
        ApplyChrome();
        PaintIslandPreview();
        WidgetPicker.SetProfileName(diff.ProfileName);
        if (IsModulesPage())
        {
            WidgetPicker.Refresh();
            _pickerDirty = false;
        }
        else
        {
            _pickerDirty = true;
        }

        _suppress = false;
    }

    private void ReloadProfileBox(IReadOnlyList<Profile> profiles, string activeId)
    {
        var selected = ProfileBox.SelectedItem as ComboBoxItem;
        if (ProfileBox.Items.Count == profiles.Count
            && ProfileBox.Items.OfType<ComboBoxItem>().Select(item => item.Tag as string).SequenceEqual(profiles.Select(profile => profile.Id))
            && ProfileBox.Items.OfType<ComboBoxItem>().Select(item => item.Content as string).SequenceEqual(profiles.Select(profile => profile.Name)))
        {
            if (selected?.Tag as string != activeId)
            {
                SelectTag(ProfileBox, activeId);
            }

            return;
        }

        ProfileBox.Items.Clear();
        foreach (var profile in profiles)
        {
            ProfileBox.Items.Add(new ComboBoxItem
            {
                Content = profile.Name,
                Tag = profile.Id,
                IsSelected = profile.Id == activeId
            });
        }
    }

    private static string MediaGrantHint(AppSettings settings, string currentName)
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return "关掉后模块仍在岛上，只是不读曲名和封面。";
        }

        var granted = settings.Profiles
            .Where(profile => profile.Permissions.Contains(PermissionIds.MediaRead, StringComparer.Ordinal))
            .Select(profile => profile.Name)
            .ToArray();
        if (granted.Length == 0)
        {
            return $"开启后只授权当前情景「{currentName}」。关掉后模块仍在岛上，只是不读曲名和封面。";
        }

        return $"已授权：{string.Join("、", granted)}。关掉后模块仍在岛上，只是不读曲名和封面。";
    }

    private void ApplyChrome()
    {
        var light = _session.IsLightTheme();
        SettingsChrome.ApplyWindow(this, light);
        NavPane.Background = SettingsChrome.Brush(DockTokens.Settings.NavPane(light));
        NavPane.BorderBrush = SettingsChrome.Brush(DockTokens.Settings.Stroke(light));
        WidgetPicker.ApplyTheme(light, rebuild: false);
    }

    private void PaintIslandPreview()
    {
        if (IslandPreview is null || IslandPreviewTile is null || IslandPreviewLabel is null)
        {
            return;
        }

        var light = _session.IsLightTheme();
        var backdrop = _session.Settings.Ui.Backdrop;
        var transparency = _session.Settings.Ui.IslandTransparency;
        IslandPreview.Background = SettingsChrome.Brush(
            DockTokens.ResolveIslandFill(light, true, backdrop, transparency));
        IslandPreview.BorderBrush = SettingsChrome.Brush(light ? DockTokens.Light.Stroke : DockTokens.Dark.Stroke);
        IslandPreviewTile.Background = SettingsChrome.Brush(DockTokens.ResolveTileFill(light, backdrop));
        IslandPreviewLabel.Foreground = SettingsChrome.Brush(light ? DockTokens.Light.Text : DockTokens.Dark.Text);
        IslandPreviewLabel.Text = backdrop switch
        {
            DockBackdrop.Frosted => "磨砂",
            DockBackdrop.Glass => "玻璃",
            _ => "云母"
        };
    }

    private void ReloadMonitors(string current)
    {
        MonitorBox.Items.Clear();
        MonitorBox.Items.Add(new ComboBoxItem { Content = MonitorLabels.FollowPrimary, Tag = "primary" });
        var index = 1;
        foreach (var monitor in _session.Topology.Monitors)
        {
            MonitorBox.Items.Add(new ComboBoxItem
            {
                Content = MonitorLabels.For(monitor, index),
                Tag = monitor.DevicePath
            });
            index++;
        }

        var tag = string.IsNullOrWhiteSpace(current) ? "primary" : current;
        SelectTag(MonitorBox, tag);
        if (MonitorBox.SelectedItem is null && MonitorBox.Items.Count > 0)
        {
            MonitorBox.SelectedIndex = 0;
        }
    }

    private void Nav_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { Tag: string id })
        {
            return;
        }

        ShowPage(id);
    }

    private void ShowPage(string id)
    {
        _page = id;
        PageModules.Visibility = VisibleIf(id == "modules");
        PageLayout.Visibility = VisibleIf(id == "layout");
        PageAppearance.Visibility = VisibleIf(id == "appearance");
        PagePrivacy.Visibility = VisibleIf(id == "privacy");
        PageAbout.Visibility = VisibleIf(id == "about");
        PageProfiles.Visibility = VisibleIf(id == "profiles" && ProductFeatures.ProfilesEnabled);

        var modules = IsModulesPage();
        WidgetPicker.SetCatalogActive(IsVisible && modules);
        if (modules && _pickerDirty)
        {
            WidgetPicker.Refresh();
            _pickerDirty = false;
        }
    }

    private bool IsModulesPage() => string.Equals(_page, "modules", StringComparison.Ordinal);

    private static Visibility VisibleIf(bool on) => on ? Visibility.Visible : Visibility.Collapsed;

    private void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || ThemeBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        _session.SetTheme(Enum.Parse<ThemePreference>(tag));
    }

    private void BackdropBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || BackdropBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        _session.SetBackdrop(Enum.Parse<DockBackdrop>(tag));
    }

    private void TransparencySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || !IsLoaded)
        {
            return;
        }

        var value = (int)Math.Round(TransparencySlider.Value);
        TransparencyLabel.Text = $"{value}%";
        _session.SetIslandTransparency(value);
        PaintIslandPreview();
    }

    private void EdgeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || EdgeBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        _session.SetDockEdge(Enum.Parse<DockEdge>(tag));
    }

    private void MonitorBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || MonitorBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        _session.SetDockMonitor(tag);
    }

    private void ProfileBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || !ProductFeatures.ProfilesEnabled
            || ProfileBox.SelectedItem is not ComboBoxItem item || item.Tag is not string id)
        {
            return;
        }

        if (id == _session.CurrentDiff.ProfileId)
        {
            return;
        }

        _session.SwitchProfile(id);
    }

    private void Privacy_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetPrivacy(_session.Settings.Privacy with
        {
            MediaRead = MediaCheck.IsChecked == true,
            NetworkDetails = NetworkCheck.IsChecked == true,
            BingDownload = BingCheck.IsChecked == true
        });
    }

    private void AutoHide_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetAutoHide(AutoHideCheck.IsChecked == true);
        SyncAutoHideDependents();
    }

    private void Peek_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetPeekOnPointer(PeekCheck.IsChecked == true);
    }

    private void CollapseApps_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetCollapseApps(CollapseAppsCheck.IsChecked == true);
    }

    private void CollapseFolders_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetCollapseFolders(CollapseFoldersCheck.IsChecked == true);
    }

    private void RevealHotkey_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        if (!_session.SetRevealHotkey(RevealHotkeyCheck.IsChecked == true))
        {
            RevealHotkeyCheck.IsChecked = false;
            SyncAutoHideDependents();
            RevealHotkeyBox.Focus();
            RevealHotkeyHint.Text = "快捷键已被占用，请改成别的组合";
            return;
        }

        SyncAutoHideDependents();
    }

    private void RevealHotkeyBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppress || RevealHotkeyBox.IsEnabled == false)
        {
            return;
        }

        _capturingRevealHotkey = true;
        _revealHotkeyBeforeCapture = RevealHotkeyBox.Text;
        RevealHotkeyBox.Text = "按下快捷键…";
        RevealHotkeyHint.Text = "";
    }

    private void RevealHotkeyBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_capturingRevealHotkey)
        {
            return;
        }

        _capturingRevealHotkey = false;
        RevealHotkeyBox.Text = string.IsNullOrWhiteSpace(_session.Settings.Hotkeys.RevealDock)
            ? "Win+Shift+A"
            : _session.Settings.Hotkeys.RevealDock;
    }

    private void RevealHotkeyBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingRevealHotkey)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Escape)
        {
            _capturingRevealHotkey = false;
            RevealHotkeyBox.Text = _revealHotkeyBeforeCapture;
            Keyboard.ClearFocus();
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            _capturingRevealHotkey = false;
            _session.SetRevealHotkey(false);
            Keyboard.ClearFocus();
            return;
        }

        if (!TryChordFromKeyEvent(e, out var chord))
        {
            return;
        }

        if (!_session.TrySetRevealDockHotkey(chord))
        {
            RevealHotkeyHint.Text = "快捷键已被占用，请改成别的组合";
            RevealHotkeyBox.Text = "按下快捷键…";
            return;
        }

        _capturingRevealHotkey = false;
        RevealHotkeyHint.Text = "";
        Keyboard.ClearFocus();
    }

    private void SyncAutoHideDependents()
    {
        var auto = AutoHideCheck.IsChecked == true;
        PeekCheck.IsEnabled = auto;
        RevealHotkeyCheck.IsEnabled = auto;
        RevealHotkeyBox.IsEnabled = auto;
    }

    private static bool TryChordFromKeyEvent(KeyEventArgs e, out string chord)
    {
        chord = "";
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
        {
            return false;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        var keyOk = vk is >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or 0x20
            or (>= 0x70 and <= 0x7B);
        if (!keyOk)
        {
            return false;
        }

        var modifiers = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0
            || Keyboard.IsKeyDown(Key.LWin)
            || Keyboard.IsKeyDown(Key.RWin))
        {
            modifiers |= HotkeyParser.ModWin;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            modifiers |= HotkeyParser.ModControl;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            modifiers |= HotkeyParser.ModAlt;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            modifiers |= HotkeyParser.ModShift;
        }

        var formatted = HotkeyParser.Format(modifiers, vk);
        if (formatted is null)
        {
            return false;
        }

        chord = formatted;
        return true;
    }

    private void Startup_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        RunAtStartup.SetEnabled(StartupCheck.IsChecked == true);
    }

    private void Undo_OnClick(object sender, RoutedEventArgs e) => _session.UndoProfile();

    private void DuplicateProfile_OnClick(object sender, RoutedEventArgs e) =>
        _session.DuplicateCurrentProfile();

    private void RenameProfile_OnClick(object sender, RoutedEventArgs e)
    {
        var id = _session.CurrentDiff.ProfileId;
        var current = _session.Settings.Profiles.FirstOrDefault(profile => profile.Id == id)?.Name ?? id;
        if (!TextPrompt.TryAsk(this, "重命名情景", "显示名称", current, out var name, _session.IsLightTheme()))
        {
            return;
        }

        _session.RenameProfile(id, name);
    }

    private void DeleteProfile_OnClick(object sender, RoutedEventArgs e)
    {
        var id = _session.CurrentDiff.ProfileId;
        if (id == DefaultProfiles.GeneralId)
        {
            return;
        }

        var name = _session.CurrentDiff.ProfileName;
        var result = MessageBox.Show(
            this,
            $"确定删除情景「{name}」？日常会保留。",
            "删除情景",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _session.RemoveProfile(id);
    }

    private static void SelectTag(ComboBox box, string tag)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }
}
