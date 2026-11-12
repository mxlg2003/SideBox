using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Core;
using WinDock.Core.Permissions;
using WinDock.Infrastructure;
using WinDock.Widgets;

namespace WinDock.App;

public partial class SettingsWindow : Window
{
    private readonly DockSession _session;
    private readonly EventHandler _onChanged;
    private bool _suppress;
    private bool _allowClose;
    private bool _listening;
    private bool _pickerDirty = true;
    private string _page = "general";
    private bool _capturingRevealHotkey;
    private string _revealHotkeyBeforeCapture = "";
    private bool _capturingProfileHotkey;
    private string _profileHotkeyBeforeCapture = "";

    private static SettingsWindow? s_activeWindow;

    public SettingsWindow(DockSession session)
    {
        if (s_activeWindow is not null && !ReferenceEquals(s_activeWindow, this))
        {
            try
            {
                s_activeWindow.AllowClose();
                s_activeWindow.Close();
            }
            catch (Exception)
            {
            }
        }

        s_activeWindow = this;
        Closed += (_, _) =>
        {
            if (ReferenceEquals(s_activeWindow, this))
            {
                s_activeWindow = null;
            }
        };

        _session = session;
        _onChanged = (_, _) => Dispatcher.BeginInvoke(OnSessionChanged, DispatcherPriority.Background);
        InitializeComponent();
        NavColumn.Width = new GridLength(220);
        NavPane.Padding = new Thickness(DockTokens.Settings.NavPad);
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
        NavGeneral.IsChecked = true;
        ShowPage("general");
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
        UpdateEdgeCards(diff.Dock.Edge);
        ReloadMonitors(diff.Dock.Monitor);
        NavModulesBadge.Text = $"{BuiltInWidgets.All.Count}";
        MediaPrivacyRow.Visibility = ProductFeatures.NowPlayingEnabled ? Visibility.Visible : Visibility.Collapsed;
        ProcessPrivacyRow.Visibility = ProductFeatures.ProcessesEnabled ? Visibility.Visible : Visibility.Collapsed;
        MediaCheck.IsChecked = settings.Privacy.MediaRead;
        NetworkCheck.IsChecked = settings.Privacy.NetworkDetails;
        ProcessCheck.IsChecked = settings.Privacy.ProcessNames;
        LocationCheck.IsChecked = settings.Privacy.PreciseLocation;
        BingCheck.IsChecked = settings.Privacy.BingDownload;
        BingAutoDailyCheck.IsChecked = _session.Hub.Bing.AutoApplyDaily;
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

        if (!_capturingProfileHotkey)
        {
            NextProfileBox.Text = string.IsNullOrWhiteSpace(settings.Hotkeys.NextProfile)
                ? "Win+Shift+D"
                : settings.Hotkeys.NextProfile;
            PrevProfileBox.Text = settings.Hotkeys.PreviousProfile ?? "";
            UndoProfileBox.Text = string.IsNullOrWhiteSpace(settings.Hotkeys.UndoProfile)
                ? "Win+Shift+Z"
                : settings.Hotkeys.UndoProfile;
        }

        SyncAutoHideDependents();
        NavProfiles.Visibility = ProductFeatures.ProfilesEnabled ? Visibility.Visible : Visibility.Collapsed;
        ProfileHotkeysGroup.Visibility = ProductFeatures.ProfilesEnabled ? Visibility.Visible : Visibility.Collapsed;
        ProfileHotkeysHint.Visibility = ProductFeatures.ProfilesEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (ProductFeatures.ProfilesEnabled)
        {
            DeleteProfileButton.IsEnabled = diff.ProfileId != DefaultProfiles.GeneralId;
            DeleteProfileButton.ToolTip = diff.ProfileId == DefaultProfiles.GeneralId
                ? UiText.CannotDeleteGeneral
                : UiText.DeleteCurrentProfile;

            ReloadProfileBox(settings.Profiles, diff.ProfileId);
            ReloadProfileModules(settings.Profiles, diff.ProfileId);
        }

        StartupCheck.IsChecked = RunAtStartup.IsEnabled();
        AboutVersion.Text = $"{ProductInfo.Name} {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0"}";
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

    private void ReloadProfileModules(IReadOnlyList<Profile> profiles, string activeId)
    {
        ProfileModulesHost.Children.Clear();
        var light = _session.IsLightTheme();
        var text = SettingsChrome.Brush(DockTokens.Settings.Text(light));
        var muted = SettingsChrome.Brush(DockTokens.Settings.Muted(light));
        var card = SettingsChrome.Brush(DockTokens.Settings.Card(light));
        var stroke = SettingsChrome.Brush(DockTokens.Settings.Stroke(light));
        var chipFill = SettingsChrome.Brush(DockTokens.Settings.Preview(light));
        var accent = SettingsChrome.Brush(DockTokens.Settings.Accent);

        var wrap = new WrapPanel { Margin = new Thickness(0, 0, -14, 0) };

        foreach (var profile in profiles)
        {
            var name = DefaultProfiles.DisplayName(profile);
            var current = profile.Id == activeId;
            var modules = ProfileModules.OnDock(profile);

            var iconName = profile.Id == DefaultProfiles.GeneralId
                ? PhosphorIcon.Desktop
                : (profile.Id == DefaultProfiles.DeepWorkId ? PhosphorIcon.Laptop : PhosphorIcon.Stack);
            var iconBg = profile.Id == DefaultProfiles.GeneralId
                ? SettingsChrome.Brush(0x268B5CF6)
                : (profile.Id == DefaultProfiles.DeepWorkId ? SettingsChrome.Brush(0x2610B981) : SettingsChrome.Brush(0x26F59E0B));
            var iconFg = profile.Id == DefaultProfiles.GeneralId
                ? SettingsChrome.Brush(0xFFA78BFA)
                : (profile.Id == DefaultProfiles.DeepWorkId ? SettingsChrome.Brush(0xFF34D399) : SettingsChrome.Brush(0xFFFBBF24));

            var iconBox = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = iconBg,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new PhosphorGlyph(iconName, 16)
                {
                    Foreground = iconFg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var titleBlock = new TextBlock
            {
                Text = name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = text,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var subtitleText = profile.Id == DefaultProfiles.GeneralId
                ? "系统默认情景 · 常驻主屏"
                : (profile.Id == DefaultProfiles.DeepWorkId ? "极简界面 · 屏蔽干扰" : "会议与演示定制布局");
            var subBlock = new TextBlock
            {
                Text = subtitleText,
                FontSize = 11,
                Foreground = muted,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(titleBlock);
            titleStack.Children.Add(subBlock);

            var header = new DockPanel();
            if (current)
            {
                var badge = new Border
                {
                    Background = accent,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock
                    {
                        Text = "当前生效中",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                };
                DockPanel.SetDock(badge, Dock.Right);
                header.Children.Add(badge);
            }

            var leftHeader = new StackPanel { Orientation = Orientation.Horizontal };
            leftHeader.Children.Add(iconBox);
            leftHeader.Children.Add(titleStack);
            header.Children.Add(leftHeader);

            var hintText = profile.Id == DefaultProfiles.GeneralId
                ? "触发感知：默认常驻，或外接双屏时自动激活"
                : (profile.Id == DefaultProfiles.DeepWorkId ? "触发感知：全屏工作或开启专注模式时切入" : "触发感知：投影或断开副屏时推荐使用");
            var triggerHint = new TextBlock
            {
                Text = hintText,
                FontSize = 11,
                Foreground = muted,
                Margin = new Thickness(0, 10, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };

            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            if (modules.Count == 0)
            {
                chipsWrap.Children.Add(new TextBlock { Text = UiText.ProfileNoModules, FontSize = 11, Foreground = muted });
            }
            else
            {
                foreach (var module in modules.Take(6))
                {
                    chipsWrap.Children.Add(ModuleChip(module, text, chipFill));
                }
            }

            var actions = new DockPanel();
            var profileId = profile.Id;

            if (current)
            {
                var activeBtn = new Button
                {
                    Content = "使用中",
                    IsEnabled = false,
                    Height = 26,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                if (TryFindResource("Settings.Button") is Style btnStyle) activeBtn.Style = btnStyle;
                actions.Children.Add(activeBtn);
            }
            else
            {
                var switchBtn = new Button
                {
                    Content = "切换到该情景",
                    Height = 26,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand
                };
                if (TryFindResource("Settings.PrimaryButton") is Style pStyle) switchBtn.Style = pStyle;
                switchBtn.Click += (_, _) => _session.SwitchProfile(profileId);
                actions.Children.Add(switchBtn);
            }

            var renameBtn = new Button
            {
                Content = UiText.Rename,
                Height = 26,
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            if (TryFindResource("Settings.Button") is Style rStyle) renameBtn.Style = rStyle;
            DockPanel.SetDock(renameBtn, Dock.Right);
            renameBtn.Click += (_, _) =>
            {
                if (TextPrompt.TryAsk(this, UiText.RenameProfileTitle, UiText.DisplayNameLabel, name, out var newName, _session.IsLightTheme()))
                {
                    _session.RenameProfile(profileId, newName);
                }
            };
            actions.Children.Add(renameBtn);

            if (!current && profileId != DefaultProfiles.GeneralId)
            {
                var delBtn = new Button
                {
                    Content = UiText.Delete,
                    Height = 26,
                    FontSize = 11,
                    Cursor = Cursors.Hand
                };
                if (TryFindResource("Settings.DestructiveButton") is Style dStyle) delBtn.Style = dStyle;
                DockPanel.SetDock(delBtn, Dock.Right);
                delBtn.Click += (_, _) =>
                {
                    var result = MessageBox.Show(this, UiText.DeleteProfileConfirm(name), UiText.DeleteProfileTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        _session.RemoveProfile(profileId);
                    }
                };
                actions.Children.Add(delBtn);
            }

            var cardBody = new StackPanel();
            cardBody.Children.Add(header);
            cardBody.Children.Add(triggerHint);
            cardBody.Children.Add(chipsWrap);
            cardBody.Children.Add(actions);

            var border = new Border
            {
                Width = 360,
                Background = card,
                BorderBrush = current ? accent : stroke,
                BorderThickness = new Thickness(current ? 2 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 14, 14),
                Child = cardBody
            };

            wrap.Children.Add(border);
        }

        ProfileModulesHost.Children.Add(wrap);
    }

    private static Border ModuleChip(ProfileModuleChip module, Brush text, Brush fill)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new PhosphorGlyph(module.Glyph, DockTokens.GlyphSize)
        {
            Width = DockTokens.GlyphSize,
            Height = DockTokens.GlyphSize,
            Margin = new Thickness(0, 0, DockTokens.Settings.FieldGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = text
        });
        row.Children.Add(new TextBlock
        {
            Text = module.Label,
            FontSize = 13,
            Foreground = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = DockTokens.FlyoutUserCopyMaxWidth
        });
        return new Border
        {
            Background = fill,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Padding = new Thickness(DockTokens.Settings.FieldGap, DockTokens.TilePaddingY, DockTokens.Settings.FieldGap, DockTokens.TilePaddingY),
            Margin = new Thickness(0, DockTokens.Settings.FieldGap, DockTokens.Settings.FieldGap, 0),
            Child = row,
            ToolTip = module.Label
        };
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
        IslandPreviewTile.Background = SettingsChrome.Brush(DockTokens.Settings.Accent);
        IslandPreviewLabel.Foreground = System.Windows.Media.Brushes.White;
        var materialName = backdrop switch
        {
            DockBackdrop.Frosted => "磨砂",
            DockBackdrop.Glass => "通透玻璃",
            _ => "云母"
        };
        IslandPreviewLabel.Text = $"{materialName} · {transparency}%";
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
        if (!ProductFeatures.ProfilesEnabled && id == "profiles")
        {
            id = "modules";
        }

        _page = id;
        PageGeneral.Visibility = VisibleIf(id == "general");
        PageLayout.Visibility = VisibleIf(id == "layout");
        PageAppearance.Visibility = VisibleIf(id == "appearance");
        PageModules.Visibility = VisibleIf(id == "modules");
        PageProfiles.Visibility = VisibleIf(id == "profiles");
        PageHotkeys.Visibility = VisibleIf(id == "hotkeys");
        PagePrivacy.Visibility = VisibleIf(id == "privacy");
        PageAbout.Visibility = VisibleIf(id == "about");

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
            ProcessNames = ProcessCheck.IsChecked == true,
            PreciseLocation = LocationCheck.IsChecked == true,
            BingDownload = BingCheck.IsChecked == true
        });
    }

    private void BingAutoDaily_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _session.SetBingWallpaperAutoApply(BingAutoDailyCheck.IsChecked == true);
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

    private void ExportSettings_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"sidebox-settings-{DateTime.Now:yyyyMMdd}.json",
                Title = UiText.ExportSettings
            };
            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, _session.ExportSettingsJson());
                MessageBox.Show(this, UiText.ExportSuccess, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportSettings_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
                Title = UiText.ImportSettings
            };
            if (dialog.ShowDialog(this) == true)
            {
                var json = File.ReadAllText(dialog.FileName);
                if (_session.ImportSettingsJson(json, out var err))
                {
                    MessageBox.Show(this, UiText.ImportSuccess, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                    Reload();
                }
                else
                {
                    MessageBox.Show(this, $"{UiText.ImportFailed}\n{err}", ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this, UiText.ResetFactoryConfirm, ProductInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            _session.ResetSettings();
            Reload();
        }
    }

    private void EdgeTopCard_OnClick(object sender, MouseButtonEventArgs e) => _session.SetDockEdge(DockEdge.Top);
    private void EdgeLeftCard_OnClick(object sender, MouseButtonEventArgs e) => _session.SetDockEdge(DockEdge.Left);
    private void EdgeRightCard_OnClick(object sender, MouseButtonEventArgs e) => _session.SetDockEdge(DockEdge.Right);

    private void UpdateEdgeCards(DockEdge edge)
    {
        var light = _session.IsLightTheme();
        var accentBrush = SettingsChrome.Brush(DockTokens.Settings.Accent);
        var strokeBrush = SettingsChrome.Brush(DockTokens.Settings.Stroke(light));
        var cardBrush = SettingsChrome.Brush(DockTokens.Settings.Card(light));
        var activeBrush = SettingsChrome.Brush(DockTokens.Settings.NavSelected(light));

        if (EdgeTopCard is not null)
        {
            EdgeTopCard.BorderBrush = edge == DockEdge.Top ? accentBrush : strokeBrush;
            EdgeTopCard.BorderThickness = new Thickness(edge == DockEdge.Top ? 2 : 1);
            EdgeTopCard.Background = edge == DockEdge.Top ? activeBrush : cardBrush;
        }

        if (EdgeLeftCard is not null)
        {
            EdgeLeftCard.BorderBrush = edge == DockEdge.Left ? accentBrush : strokeBrush;
            EdgeLeftCard.BorderThickness = new Thickness(edge == DockEdge.Left ? 2 : 1);
            EdgeLeftCard.Background = edge == DockEdge.Left ? activeBrush : cardBrush;
        }

        if (EdgeRightCard is not null)
        {
            EdgeRightCard.BorderBrush = edge == DockEdge.Right ? accentBrush : strokeBrush;
            EdgeRightCard.BorderThickness = new Thickness(edge == DockEdge.Right ? 2 : 1);
            EdgeRightCard.Background = edge == DockEdge.Right ? activeBrush : cardBrush;
        }
    }

    private void RecordReveal_OnClick(object sender, RoutedEventArgs e)
    {
        RevealHotkeyBox.Focus();
    }

    private void RecordProfileHotkey_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            var target = tag switch
            {
                "next" => NextProfileBox,
                "prev" => PrevProfileBox,
                "undo" => UndoProfileBox,
                _ => null
            };
            target?.Focus();
        }
    }

    private void OpenConfigFolder_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = AppPaths.RoamingDirectory;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
        }
        catch (Exception)
        {
        }
    }

    private void OpenLogsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = AppPaths.LogsDirectory;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
        }
        catch (Exception)
        {
        }
    }

    private void OpenGitHub_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProductInfo.GitHubRepositoryUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private void OpenWebsite_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://sidebox.microsoftware.app") { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private void ProfileHotkeyBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppress || sender is not TextBox box) return;
        _capturingProfileHotkey = true;
        _profileHotkeyBeforeCapture = box.Text;
        box.Text = UiText.PressShortcut;
        ProfileHotkeysHint.Text = "";
    }

    private void ProfileHotkeyBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_capturingProfileHotkey || sender is not TextBox box) return;
        _capturingProfileHotkey = false;
        var tag = box.Tag as string;
        box.Text = tag switch
        {
            "next" => _session.Settings.Hotkeys.NextProfile ?? "Win+Shift+D",
            "prev" => _session.Settings.Hotkeys.PreviousProfile ?? "",
            "undo" => _session.Settings.Hotkeys.UndoProfile ?? "Win+Shift+Z",
            _ => ""
        };
    }

    private void ProfileHotkeyBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingProfileHotkey || sender is not TextBox box) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Escape)
        {
            _capturingProfileHotkey = false;
            box.Text = _profileHotkeyBeforeCapture;
            Keyboard.ClearFocus();
            return;
        }

        var tag = box.Tag as string;
        if (key is Key.Back or Key.Delete)
        {
            _capturingProfileHotkey = false;
            box.Text = "";
            UpdateProfileHotkeys(tag, null);
            Keyboard.ClearFocus();
            return;
        }

        if (!TryChordFromKeyEvent(e, out var chord)) return;

        if (!UpdateProfileHotkeys(tag, chord))
        {
            ProfileHotkeysHint.Text = UiText.HotkeyTaken;
            box.Text = UiText.PressShortcut;
            return;
        }

        _capturingProfileHotkey = false;
        ProfileHotkeysHint.Text = "";
        box.Text = chord;
        Keyboard.ClearFocus();
    }

    private bool UpdateProfileHotkeys(string? tag, string? chord)
    {
        var next = tag == "next" ? chord : _session.Settings.Hotkeys.NextProfile;
        var prev = tag == "prev" ? chord : _session.Settings.Hotkeys.PreviousProfile;
        var undo = tag == "undo" ? chord : _session.Settings.Hotkeys.UndoProfile;
        return _session.TrySetProfileHotkeys(next, prev, undo);
    }

    private void ApplyCopy()
    {
        Title = $"{ProductInfo.Name} {UiText.Settings}";
        NavTitle.Text = UiText.Settings;
        NavGroupBasic.Text = UiText.NavGroupBasic;
        NavGeneralLabel.Text = UiText.NavGeneral;
        System.Windows.Automation.AutomationProperties.SetName(NavGeneral, UiText.NavGeneral);
        NavLayoutLabel.Text = UiText.NavLayout;
        System.Windows.Automation.AutomationProperties.SetName(NavLayout, UiText.NavLayout);
        NavAppearanceLabel.Text = UiText.NavAppearance;
        System.Windows.Automation.AutomationProperties.SetName(NavAppearance, UiText.NavAppearance);

        NavGroupCore.Text = UiText.NavGroupCore;
        NavModulesLabel.Text = UiText.NavModules;
        System.Windows.Automation.AutomationProperties.SetName(NavModules, UiText.NavModules);
        NavProfilesLabel.Text = UiText.NavProfiles;
        System.Windows.Automation.AutomationProperties.SetName(NavProfiles, UiText.NavProfiles);
        NavHotkeysLabel.Text = UiText.NavHotkeys;
        System.Windows.Automation.AutomationProperties.SetName(NavHotkeys, UiText.NavHotkeys);

        NavGroupSystem.Text = UiText.NavGroupSystemAndSupport;
        NavPrivacyLabel.Text = UiText.NavPrivacy;
        System.Windows.Automation.AutomationProperties.SetName(NavPrivacy, UiText.NavPrivacy);
        NavAboutLabel.Text = UiText.NavAbout;
        System.Windows.Automation.AutomationProperties.SetName(NavAbout, UiText.NavAbout);
        NavExitLabel.Text = UiText.ExitWinDock;

        GeneralTitle.Text = UiText.General;
        GeneralIntro.Text = UiText.GeneralIntro;
        SystemAndStartupLabel.Text = UiText.SystemAndStartup;
        StartupTitle.Text = UiText.RunAtStartup;
        StartupHelp.Text = UiText.RunAtStartupHelp;
        System.Windows.Automation.AutomationProperties.SetName(StartupCheck, UiText.RunAtStartup);
        LanguageLabel.Text = UiText.Language;
        LanguageSystem.Content = UiText.LanguageSystem;
        LanguageChinese.Content = UiText.LanguageChinese;
        LanguageEnglish.Content = UiText.LanguageEnglish;
        LanguageHelp.Text = UiText.LanguageHelp;
        BackupSectionTitle.Text = UiText.BackupSection;
        BackupSectionHelp.Text = UiText.BackupHelp;
        ExportSettingsButton.Content = UiText.ExportSettings;
        ImportSettingsButton.Content = UiText.ImportSettings;
        ResetSettingsButton.Content = UiText.ResetFactory;

        ModulesTitle.Text = UiText.NavModules;
        ModulesHelp.Text = UiText.ModulesIntro;
        LayoutTitle.Text = UiText.NavLayout;
        LayoutIntro.Text = UiText.LayoutIntro;
        EdgeLabel.Text = UiText.Edge;
        EdgeTop.Text = UiText.EdgeTop;
        EdgeLeft.Text = UiText.EdgeLeft;
        EdgeRight.Text = UiText.EdgeRight;
        MonitorLabel.Text = UiText.Monitor;
        AutoHideTitle.Text = UiText.AutoHide;
        AutoHideHelp.Text = UiText.AutoHideHelp;
        System.Windows.Automation.AutomationProperties.SetName(AutoHideCheck, UiText.AutoHide);
        PeekTitle.Text = UiText.Peek;
        PeekHelp.Text = UiText.PeekHelp;
        System.Windows.Automation.AutomationProperties.SetName(PeekCheck, UiText.Peek);
        CollapseAppsTitle.Text = UiText.CollapseApps;
        CollapseAppsHelp.Text = UiText.CollapseAppsHelp;
        System.Windows.Automation.AutomationProperties.SetName(CollapseAppsCheck, UiText.CollapseApps);
        CollapseFoldersTitle.Text = UiText.CollapseFolders;
        CollapseFoldersHelp.Text = UiText.CollapseFoldersHelp;
        System.Windows.Automation.AutomationProperties.SetName(CollapseFoldersCheck, UiText.CollapseFolders);
        LayoutHelp.Text = UiText.LayoutFooter;

        AppearanceTitle.Text = UiText.NavAppearance;
        AppearanceIntro.Text = UiText.AppearanceIntro;
        ThemeLabel.Text = UiText.Theme;
        ThemeSystem.Content = UiText.ThemeSystem;
        ThemeLight.Content = UiText.ThemeLight;
        ThemeDark.Content = UiText.ThemeDark;
        BackdropLabel.Text = UiText.Backdrop;
        BackdropMica.Content = UiText.BackdropMica;
        BackdropFrosted.Content = UiText.BackdropFrosted;
        BackdropGlass.Content = UiText.BackdropGlass;
        TransparencyCaption.Text = UiText.IslandTransparency;
        AppearanceHelp.Text = UiText.AppearanceFooter;

        HotkeysTitle.Text = UiText.NavHotkeys;
        HotkeysIntro.Text = UiText.HotkeysIntro;
        RevealHotkeyTitle.Text = UiText.RevealHotkey;
        RevealHotkeyHelp.Text = UiText.RevealHotkeyHelp;
        System.Windows.Automation.AutomationProperties.SetName(RevealHotkeyCheck, UiText.RevealHotkey);
        System.Windows.Automation.AutomationProperties.SetName(RevealHotkeyBox, UiText.RevealHotkeyName);
        NextProfileLabel.Text = UiText.NextProfileShortcut;
        PrevProfileLabel.Text = UiText.PrevProfileShortcut;
        UndoProfileLabel.Text = UiText.UndoProfileShortcut;

        PrivacyTitle.Text = UiText.NavPrivacy;
        PrivacyIntro.Text = UiText.PrivacyIntro;
        MediaPrivacyTitle.Text = UiText.MediaPrivacy;
        System.Windows.Automation.AutomationProperties.SetName(MediaCheck, UiText.MediaPrivacy);
        NetworkPrivacyTitle.Text = UiText.NetworkPrivacy;
        NetworkPrivacyHelp.Text = UiText.NetworkPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(NetworkCheck, UiText.NetworkPrivacy);
        ProcessPrivacyTitle.Text = UiText.ProcessNamesPrivacy;
        ProcessPrivacyHelp.Text = UiText.ProcessNamesPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(ProcessCheck, UiText.ProcessNamesPrivacy);
        BingPrivacyTitle.Text = UiText.BingPrivacy;
        BingPrivacyHelp.Text = UiText.BingPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(BingCheck, UiText.BingPrivacy);
        BingAutoDailyTitle.Text = UiText.BingAutoDaily;
        BingAutoDailyHelp.Text = UiText.BingAutoDailyHint;
        System.Windows.Automation.AutomationProperties.SetName(BingAutoDailyCheck, UiText.BingAutoDaily);
        LocationPrivacyTitle.Text = UiText.LocationPrivacy;
        LocationPrivacyHelp.Text = UiText.LocationPrivacyHelp;
        System.Windows.Automation.AutomationProperties.SetName(LocationCheck, UiText.LocationPrivacy);
        PrivacyHelp.Text = UiText.PrivacyFooter;

        AboutTitle.Text = UiText.NavAbout;
        AboutBlurb.Text = UiText.AboutIntro;
        AboutCheckUpdateButton.Content = UiText.CheckForUpdates;
        AboutLicenseLabel.Text = UiText.LicenseFree;
        UpdateNowButton.Content = UiText.UpdateNow;
        UpdateChangelogTitle.Text = $"{UiText.UpdateChangelog}：";
        OpenConfigFolderButton.Content = UiText.OpenConfigFolder;
        OpenLogsFolderButton.Content = UiText.OpenLogsFolder;
        CommunitySectionTitle.Text = UiText.CommunitySection;
        OpenGitHubButton.Content = UiText.GitHubRepo;
        OpenWebsiteButton.Content = UiText.OfficialWebsite;

        ProfilesTitle.Text = UiText.NavProfiles;
        ProfilesIntro.Text = UiText.ProfilesIntro;
        CurrentProfileLabel.Text = UiText.CurrentProfile;
        System.Windows.Automation.AutomationProperties.SetName(ProfileBox, UiText.CurrentProfile);
        DuplicateProfileButton.Content = UiText.DuplicateProfile;
        RenameProfileButton.Content = UiText.Rename;
        DeleteProfileButton.Content = UiText.Delete;
        ProfileModulesTitle.Text = UiText.ProfileModulesTitle;
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
