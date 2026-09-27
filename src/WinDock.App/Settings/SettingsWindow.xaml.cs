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
        NavPane.Padding = new Thickness(DockTokens.Settings.NavPad);
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
            _session.NotifyBackgroundRunning();
        };
        NavModules.IsChecked = true;
        ShowPage("modules");
        ApplyCopy();
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
        ApplyCopy();
        var settings = _session.Settings;
        var diff = _session.CurrentDiff;
        SelectTag(ThemeBox, settings.Ui.Theme.ToString());
        SelectTag(LanguageBox, settings.Ui.Language.ToString());
        SelectTag(BackdropBox, settings.Ui.Backdrop.ToString());
        TransparencySlider.Value = settings.Ui.IslandTransparency;
        TransparencyLabel.Text = $"{settings.Ui.IslandTransparency}%";
        SelectTag(EdgeBox, diff.Dock.Edge.ToString());
        ReloadMonitors(diff.Dock.Monitor);
        MediaCheck.IsChecked = settings.Privacy.MediaRead;
        NetworkCheck.IsChecked = settings.Privacy.NetworkDetails;
        LocationCheck.IsChecked = settings.Privacy.PreciseLocation;
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
            HotkeyText.Text = UiText.ProfileHotkeys(settings.Hotkeys.NextProfile ?? UiText.None, settings.Hotkeys.UndoProfile ?? UiText.None);
            FocusHint.Text = UiText.FocusProfileHint;
            DeleteProfileButton.IsEnabled = diff.ProfileId != DefaultProfiles.GeneralId;
            DeleteProfileButton.ToolTip = diff.ProfileId == DefaultProfiles.GeneralId
                ? UiText.CannotDeleteGeneral
                : UiText.DeleteCurrentProfile;
            HistoryList.Items.Clear();
            foreach (var record in settings.SwitchHistory.Reverse().Take(ProfileSwitchLog.MaxRecords))
            {
                var line = ProfileSwitchLog.Summarize(record, settings.Profiles);
                HistoryList.Items.Add(new TextBlock
                {
                    Text = line,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = line,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });
            }

            ReloadProfileBox(settings.Profiles, diff.ProfileId);
            UndoButton.IsEnabled = SettingsEditor.UndoTarget(settings, diff.ProfileId) is not null;
        }

        StartupCheck.IsChecked = RunAtStartup.IsEnabled();
        AboutVersion.Text = $"WinDock {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0"}";
        AboutPaths.Text = UiText.AboutConfig(AppPaths.SettingsFile, AppPaths.LogsDirectory);
        var active = settings.Profiles.FirstOrDefault(profile => profile.Id == diff.ProfileId);
        MediaHint.Text = MediaGrantHint(settings, active is null ? diff.ProfileName : DefaultProfiles.DisplayName(active));
        ApplyChrome();
        PaintIslandPreview();
        WidgetPicker.SetProfileName(active is null ? diff.ProfileName : DefaultProfiles.DisplayName(active));
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
            && ProfileBox.Items.OfType<ComboBoxItem>().Select(ComboName).SequenceEqual(profiles.Select(DefaultProfiles.DisplayName)))
        {
            if (selected?.Tag as string != activeId)
            {
                SelectTag(ProfileBox, activeId);
            }

            foreach (var item in ProfileBox.Items.OfType<ComboBoxItem>())
            {
                ApplyOverflow(item, ComboName(item));
            }

            ApplyOverflow(ProfileBox, profiles.FirstOrDefault(profile => profile.Id == activeId) is { } active
                ? DefaultProfiles.DisplayName(active)
                : null);
            return;
        }

        ProfileBox.Items.Clear();
        foreach (var profile in profiles)
        {
            var item = new ComboBoxItem
            {
                Content = OverflowText(DefaultProfiles.DisplayName(profile)),
                Tag = profile.Id,
                IsSelected = profile.Id == activeId
            };
            ApplyOverflow(item, profile.Name);
            ProfileBox.Items.Add(item);
        }

        ApplyOverflow(ProfileBox, profiles.FirstOrDefault(profile => profile.Id == activeId)?.Name);
    }

    private static string ComboName(ComboBoxItem item) =>
        item.Content as string ?? (item.Content as TextBlock)?.Text ?? string.Empty;

    private static TextBlock OverflowText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ApplyOverflow(block, text);
        return block;
    }

    private static void ApplyOverflow(FrameworkElement target, string? text)
    {
        target.ToolTip = string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string MediaGrantHint(AppSettings settings, string currentName)
    {
        if (!ProductFeatures.ProfilesEnabled)
        {
            return UiText.MediaHintOff;
        }

        var granted = settings.Profiles
            .Where(profile => profile.Permissions.Contains(PermissionIds.MediaRead, StringComparer.Ordinal))
            .Select(DefaultProfiles.DisplayName)
            .ToArray();
        if (granted.Length == 0)
        {
            return UiText.MediaHintGrant(currentName);
        }

        var joiner = UiLocale.IsChinese ? "、" : ", ";
        return UiText.MediaHintGranted(string.Join(joiner, granted));
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
            DockBackdrop.Frosted => UiText.BackdropFrostedShort,
            DockBackdrop.Glass => UiText.BackdropGlassShort,
            _ => UiText.BackdropMicaShort
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

    private void LanguageBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !IsLoaded || LanguageBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        _session.SetLanguage(Enum.Parse<UiLanguage>(tag));
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
            PreciseLocation = LocationCheck.IsChecked == true,
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
            RevealHotkeyHint.Text = UiText.HotkeyTaken;
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
        RevealHotkeyBox.Text = UiText.PressShortcut;
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
            RevealHotkeyHint.Text = UiText.HotkeyTaken;
            RevealHotkeyBox.Text = UiText.PressShortcut;
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
        if (!TextPrompt.TryAsk(this, UiText.RenameProfileTitle, UiText.DisplayNameLabel, current, out var name, _session.IsLightTheme()))
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
            UiText.DeleteProfileConfirm(name),
            UiText.DeleteProfileTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _session.RemoveProfile(id);
    }

    private void ApplyCopy()
    {
        Title = $"WinDock {UiText.Settings}";
        NavTitle.Text = UiText.Settings;
        NavModulesLabel.Text = UiText.Modules;
        System.Windows.Automation.AutomationProperties.SetName(NavModules, UiText.Modules);
        NavLayoutLabel.Text = UiText.Layout;
        System.Windows.Automation.AutomationProperties.SetName(NavLayout, UiText.Layout);
        NavGroupDisplay.Text = UiText.Display;
        NavAppearanceLabel.Text = UiText.Appearance;
        System.Windows.Automation.AutomationProperties.SetName(NavAppearance, UiText.Appearance);
        NavGroupSystem.Text = UiText.System;
        NavPrivacyLabel.Text = UiText.Privacy;
        System.Windows.Automation.AutomationProperties.SetName(NavPrivacy, UiText.Privacy);
        NavAboutLabel.Text = UiText.About;
        System.Windows.Automation.AutomationProperties.SetName(NavAbout, UiText.About);
        NavExitLabel.Text = UiText.ExitWinDock;
        AboutExitButton.Content = UiText.ExitWinDock;
        NavGroupAdvanced.Text = UiText.Advanced;
        NavProfilesLabel.Text = UiText.Profiles;
        System.Windows.Automation.AutomationProperties.SetName(NavProfiles, UiText.Profiles);

        ModulesTitle.Text = UiText.Modules;
        ModulesHelp.Text = UiText.ModulesIntro;
        LayoutTitle.Text = UiText.Layout;
        LayoutIntro.Text = UiText.LayoutIntro;
        EdgeLabel.Text = UiText.Edge;
        EdgeTop.Content = UiText.EdgeTop;
        EdgeLeft.Content = UiText.EdgeLeft;
        EdgeRight.Content = UiText.EdgeRight;
        MonitorLabel.Text = UiText.Monitor;
        AutoHideTitle.Text = UiText.AutoHide;
        AutoHideHelp.Text = UiText.AutoHideHelp;
        System.Windows.Automation.AutomationProperties.SetName(AutoHideCheck, UiText.AutoHide);
        PeekTitle.Text = UiText.Peek;
        PeekHelp.Text = UiText.PeekHelp;
        System.Windows.Automation.AutomationProperties.SetName(PeekCheck, UiText.Peek);
        RevealHotkeyTitle.Text = UiText.RevealHotkey;
        RevealHotkeyHelp.Text = UiText.RevealHotkeyHelp;
        System.Windows.Automation.AutomationProperties.SetName(RevealHotkeyCheck, UiText.RevealHotkey);
        System.Windows.Automation.AutomationProperties.SetName(RevealHotkeyBox, UiText.RevealHotkeyName);
        CollapseAppsTitle.Text = UiText.CollapseApps;
        CollapseAppsHelp.Text = UiText.CollapseAppsHelp;
        System.Windows.Automation.AutomationProperties.SetName(CollapseAppsCheck, UiText.CollapseApps);
        CollapseFoldersTitle.Text = UiText.CollapseFolders;
        CollapseFoldersHelp.Text = UiText.CollapseFoldersHelp;
        System.Windows.Automation.AutomationProperties.SetName(CollapseFoldersCheck, UiText.CollapseFolders);
        LayoutHelp.Text = UiText.LayoutFooter;

        AppearanceTitle.Text = UiText.Appearance;
        AppearanceIntro.Text = UiText.AppearanceIntro;
        ThemeLabel.Text = UiText.Theme;
        ThemeSystem.Content = UiText.ThemeSystem;
        ThemeLight.Content = UiText.ThemeLight;
        ThemeDark.Content = UiText.ThemeDark;
        BackdropLabel.Text = UiText.Backdrop;
        BackdropMica.Content = UiText.BackdropMica;
        BackdropFrosted.Content = UiText.BackdropFrosted;
        BackdropGlass.Content = UiText.BackdropGlass;
        LanguageLabel.Text = UiText.Language;
        LanguageSystem.Content = UiText.LanguageSystem;
        LanguageChinese.Content = UiText.LanguageChinese;
        LanguageEnglish.Content = UiText.LanguageEnglish;
        LanguageHelp.Text = UiText.LanguageHelp;
        TransparencyCaption.Text = UiText.IslandTransparency;
        AppearanceHelp.Text = UiText.AppearanceFooter;

        PrivacyTitle.Text = UiText.Privacy;
        PrivacyIntro.Text = UiText.PrivacyIntro;
        MediaPrivacyTitle.Text = UiText.MediaPrivacy;
        System.Windows.Automation.AutomationProperties.SetName(MediaCheck, UiText.MediaPrivacy);
        NetworkPrivacyTitle.Text = UiText.NetworkPrivacy;
        NetworkPrivacyHelp.Text = UiText.NetworkPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(NetworkCheck, UiText.NetworkPrivacy);
        BingPrivacyTitle.Text = UiText.BingPrivacy;
        BingPrivacyHelp.Text = UiText.BingPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(BingCheck, UiText.BingPrivacy);
        LocationPrivacyTitle.Text = UiText.LocationPrivacy;
        LocationPrivacyHelp.Text = UiText.LocationPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(LocationCheck, UiText.LocationPrivacy);
        PrivacyHelp.Text = UiText.PrivacyFooter;

        AboutTitle.Text = UiText.About;
        AboutBlurb.Text = UiText.AboutIntro;
        StartupTitle.Text = UiText.RunAtStartup;
        StartupHelp.Text = UiText.RunAtStartupHelp;
        System.Windows.Automation.AutomationProperties.SetName(StartupCheck, UiText.RunAtStartup);

        ProfilesTitle.Text = UiText.Profiles;
        ProfilesIntro.Text = UiText.ProfilesIntro;
        CurrentProfileLabel.Text = UiText.CurrentProfile;
        System.Windows.Automation.AutomationProperties.SetName(ProfileBox, UiText.CurrentProfile);
        UndoButton.Content = UiText.UndoLast;
        DuplicateProfileButton.Content = UiText.DuplicateProfile;
        RenameProfileButton.Content = UiText.Rename;
        DeleteProfileButton.Content = UiText.Delete;
        RecentSwitchesTitle.Text = UiText.RecentSwitches;
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

    private void NavExit_OnClick(object sender, RoutedEventArgs e)
    {
        _session.RequestExit();
    }
}
