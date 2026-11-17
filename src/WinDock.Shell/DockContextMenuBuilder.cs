using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinDock.Core;

namespace WinDock.Shell;

public static class DockContextMenuBuilder
{
    public static ContextMenu BuildProfileMenu(
        IReadOnlyList<Profile> profiles,
        string activeProfileId,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        DockQuickSettings quickSettings,
        Action<PinPickKind> onPinAdd,
        Action<string?> onSettings,
        Action<string> onProfileSwitch,
        Action<DockEdge> onEdgeChange,
        Action onAutoHideToggle,
        Action onAvoidFullscreenToggle,
        Action<ThemePreference> onThemeChange,
        Action<DockBackdrop> onBackdropChange,
        Action onRunAtStartupToggle,
        Action onExit)
    {
        var menu = DockMenu.Create(isLight, backdrop, backdropOk);
        menu.Placement = PlacementMode.MousePoint;

        var addApp = DockMenu.Item(UiText.AddApp);
        addApp.Click += (_, _) => onPinAdd(PinPickKind.InstalledApp);
        menu.Items.Add(addApp);

        var addShortcut = DockMenu.Item(UiText.AddShortcut);
        addShortcut.Click += (_, _) => onPinAdd(PinPickKind.File);
        menu.Items.Add(addShortcut);

        var addFolder = DockMenu.Item(UiText.AddFolder);
        addFolder.Click += (_, _) => onPinAdd(PinPickKind.Folder);
        menu.Items.Add(addFolder);

        var addWidget = DockMenu.Item(UiText.AddWidget);
        addWidget.Click += (_, _) => onSettings("modules");
        menu.Items.Add(addWidget);

        AppendCommonSettings(
            menu,
            isLight,
            quickSettings,
            onSettings,
            onEdgeChange,
            onAutoHideToggle,
            onAvoidFullscreenToggle,
            onThemeChange,
            onBackdropChange,
            onRunAtStartupToggle);

        if (ProductFeatures.ProfilesEnabled && profiles.Count > 0)
        {
            var profilesMenu = DockMenu.SubMenuItem(UiText.ProfilesSection);
            foreach (var profile in profiles)
            {
                var item = DockMenu.Item(DefaultProfiles.DisplayName(profile), checkable: true, isChecked: profile.Id == activeProfileId);
                var id = profile.Id;
                item.Click += (_, _) => onProfileSwitch(id);
                profilesMenu.Items.Add(item);
            }
            menu.Items.Add(profilesMenu);
        }

        menu.Items.Add(DockMenu.Divider(isLight));
        var exitItem = DockMenu.DangerItem(UiText.ExitWinDock);
        exitItem.Click += (_, _) => onExit();
        menu.Items.Add(exitItem);

        return menu;
    }

    public static void AppendCommonSettings(
        ContextMenu menu,
        bool isLight,
        DockQuickSettings quickSettings,
        Action<string?> onSettings,
        Action<DockEdge> onEdgeChange,
        Action onAutoHideToggle,
        Action onAvoidFullscreenToggle,
        Action<ThemePreference> onThemeChange,
        Action<DockBackdrop> onBackdropChange,
        Action onRunAtStartupToggle)
    {
        menu.Items.Add(DockMenu.Divider(isLight));

        // 停靠位置
        var edgeMenu = DockMenu.SubMenuItem(UiText.DockPosition);
        var edgeTop = DockMenu.Item(UiText.PositionTop, checkable: true, isChecked: quickSettings.Edge == DockEdge.Top);
        edgeTop.Click += (_, _) => onEdgeChange(DockEdge.Top);
        edgeMenu.Items.Add(edgeTop);

        var edgeBottom = DockMenu.Item(UiText.PositionBottom, checkable: true, isChecked: quickSettings.Edge == DockEdge.Bottom);
        edgeBottom.Click += (_, _) => onEdgeChange(DockEdge.Bottom);
        edgeMenu.Items.Add(edgeBottom);

        var edgeLeft = DockMenu.Item(UiText.PositionLeft, checkable: true, isChecked: quickSettings.Edge == DockEdge.Left);
        edgeLeft.Click += (_, _) => onEdgeChange(DockEdge.Left);
        edgeMenu.Items.Add(edgeLeft);

        var edgeRight = DockMenu.Item(UiText.PositionRight, checkable: true, isChecked: quickSettings.Edge == DockEdge.Right);
        edgeRight.Click += (_, _) => onEdgeChange(DockEdge.Right);
        edgeMenu.Items.Add(edgeRight);

        menu.Items.Add(edgeMenu);

        // 主题外观
        var themeMenu = DockMenu.SubMenuItem(UiText.Theme);
        var themeSystem = DockMenu.Item(UiText.ThemeSystem, checkable: true, isChecked: quickSettings.Theme == ThemePreference.System);
        themeSystem.Click += (_, _) => onThemeChange(ThemePreference.System);
        themeMenu.Items.Add(themeSystem);

        var themeLight = DockMenu.Item(UiText.ThemeLight, checkable: true, isChecked: quickSettings.Theme == ThemePreference.Light);
        themeLight.Click += (_, _) => onThemeChange(ThemePreference.Light);
        themeMenu.Items.Add(themeLight);

        var themeDark = DockMenu.Item(UiText.ThemeDark, checkable: true, isChecked: quickSettings.Theme == ThemePreference.Dark);
        themeDark.Click += (_, _) => onThemeChange(ThemePreference.Dark);
        themeMenu.Items.Add(themeDark);

        menu.Items.Add(themeMenu);

        // 材质效果
        var backdropMenu = DockMenu.SubMenuItem(UiText.Backdrop);
        var backdropMica = DockMenu.Item(UiText.BackdropMicaShort, checkable: true, isChecked: quickSettings.Backdrop == DockBackdrop.Mica);
        backdropMica.Click += (_, _) => onBackdropChange(DockBackdrop.Mica);
        backdropMenu.Items.Add(backdropMica);

        var backdropFrosted = DockMenu.Item(UiText.BackdropFrostedShort, checkable: true, isChecked: quickSettings.Backdrop == DockBackdrop.Frosted);
        backdropFrosted.Click += (_, _) => onBackdropChange(DockBackdrop.Frosted);
        backdropMenu.Items.Add(backdropFrosted);

        var backdropGlass = DockMenu.Item(UiText.BackdropGlassShort, checkable: true, isChecked: quickSettings.Backdrop == DockBackdrop.Glass);
        backdropGlass.Click += (_, _) => onBackdropChange(DockBackdrop.Glass);
        backdropMenu.Items.Add(backdropGlass);

        menu.Items.Add(backdropMenu);

        // 常用开关项
        var autoHide = DockMenu.Item(UiText.AutoHide, checkable: true, isChecked: quickSettings.AutoHide);
        autoHide.Click += (_, _) => onAutoHideToggle();
        menu.Items.Add(autoHide);

        var avoidFullscreen = DockMenu.Item(UiText.AvoidFullscreen, checkable: true, isChecked: quickSettings.AvoidFullscreen);
        avoidFullscreen.Click += (_, _) => onAvoidFullscreenToggle();
        menu.Items.Add(avoidFullscreen);

        var runAtStartup = DockMenu.Item(UiText.RunAtStartup, checkable: true, isChecked: quickSettings.RunAtStartup);
        runAtStartup.Click += (_, _) => onRunAtStartupToggle();
        menu.Items.Add(runAtStartup);

        menu.Items.Add(DockMenu.Divider(isLight));
        var settingsItem = DockMenu.Item(UiText.MoreSettings);
        settingsItem.Click += (_, _) => onSettings(null);
        menu.Items.Add(settingsItem);
    }

    public static ContextMenu BuildProfileMenu(
        IReadOnlyList<Profile> profiles,
        string activeProfileId,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        Action<PinPickKind> onPinAdd,
        Action onSettings,
        Action<string> onProfileSwitch,
        Action onExit) =>
        BuildProfileMenu(
            profiles,
            activeProfileId,
            isLight,
            backdrop,
            backdropOk,
            new DockQuickSettings(),
            onPinAdd,
            _ => onSettings(),
            onProfileSwitch,
            _ => { },
            () => { },
            () => { },
            _ => { },
            _ => { },
            () => { },
            onExit);

    [Obsolete("Undo profile has been removed from dock context menu.")]
    public static ContextMenu BuildProfileMenu(
        IReadOnlyList<Profile> profiles,
        string activeProfileId,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        Action<PinPickKind> onPinAdd,
        Action onSettings,
        Action<string> onProfileSwitch,
        Action? onUndo,
        bool canUndo,
        Action onExit) =>
        BuildProfileMenu(
            profiles,
            activeProfileId,
            isLight,
            backdrop,
            backdropOk,
            onPinAdd,
            onSettings,
            onProfileSwitch,
            onExit);

    public static ContextMenu BuildWidgetMenu(
        DockItem item,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        Action<string> onOpen,
        Action<string> onCustomize,
        Action<WidgetDensityRequest> onDensity,
        Action<string> onRemove,
        Action? onSettings = null,
        Action? onExit = null)
    {
        var widgetId = item.WidgetId ?? string.Empty;
        var menu = DockMenu.Create(isLight, backdrop, backdropOk);
        menu.Placement = PlacementMode.MousePoint;

        if (BuiltInWidgets.CanOpen(widgetId))
        {
            var open = DockMenu.Item(UiText.OpenWidget);
            open.Click += (_, _) => onOpen(item.Id);
            menu.Items.Add(open);
        }

        if (BuiltInWidgets.CanCustomize(widgetId))
        {
            var customize = DockMenu.Item(UiText.Customize);
            customize.Click += (_, _) => onCustomize(item.Id);
            menu.Items.Add(customize);
        }

        if (BuiltInWidgets.HasDensityModes(widgetId))
        {
            var compactOn = item.Density != WidgetDensity.Expanded;
            var compact = DockMenu.Item(UiText.Compact, isChecked: compactOn);
            var expanded = DockMenu.Item(UiText.Expanded, isChecked: !compactOn);
            compact.Click += (_, _) =>
            {
                compact.IsChecked = true;
                expanded.IsChecked = false;
                onDensity(new WidgetDensityRequest
                {
                    ItemId = item.Id,
                    Density = WidgetDensity.Compact
                });
            };
            expanded.Click += (_, _) =>
            {
                expanded.IsChecked = true;
                compact.IsChecked = false;
                onDensity(new WidgetDensityRequest
                {
                    ItemId = item.Id,
                    Density = WidgetDensity.Expanded
                });
            };
            menu.Items.Add(compact);
            menu.Items.Add(expanded);
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(DockMenu.Divider(isLight));
        }

        var remove = DockMenu.DangerItem(
            string.Equals(widgetId, BuiltInWidgets.Notes, StringComparison.Ordinal) ? UiText.HideNotes : UiText.RemoveFromDock);
        remove.Click += (_, _) => onRemove(item.Id);
        menu.Items.Add(remove);

        if (onSettings is not null || onExit is not null)
        {
            menu.Items.Add(DockMenu.Divider(isLight));
            if (onSettings is not null)
            {
                var settingsItem = DockMenu.Item(UiText.MoreSettings);
                settingsItem.Click += (_, _) => onSettings();
                menu.Items.Add(settingsItem);
            }

            if (onExit is not null)
            {
                var exitApp = DockMenu.DangerItem(UiText.ExitWinDock);
                exitApp.Click += (_, _) => onExit();
                menu.Items.Add(exitApp);
            }
        }

        return menu;
    }

    public static ContextMenu BuildWidgetMenu(
        DockItem item,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        DockQuickSettings quickSettings,
        Action<string> onOpen,
        Action<string> onCustomize,
        Action<WidgetDensityRequest> onDensity,
        Action<string> onRemove,
        Action<string?> onSettings,
        Action<DockEdge> onEdgeChange,
        Action onAutoHideToggle,
        Action onAvoidFullscreenToggle,
        Action<ThemePreference> onThemeChange,
        Action<DockBackdrop> onBackdropChange,
        Action onRunAtStartupToggle,
        Action onExit) =>
        BuildWidgetMenu(
            item,
            isLight,
            backdrop,
            backdropOk,
            onOpen,
            onCustomize,
            onDensity,
            onRemove,
            () => onSettings(null),
            onExit);

    public static ContextMenu BuildPinMenu(
        DockItem item,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        Action<string> onLaunch,
        Action<string> onRemove,
        Action? onSettings = null,
        Action? onExit = null,
        IReadOnlyList<DockWindowEntry>? windows = null,
        Action<nint>? onActivateWindow = null,
        Action<nint>? onCloseWindow = null,
        Action<string>? onLaunchAdmin = null)
    {
        var menu = DockMenu.Create(isLight, backdrop, backdropOk);
        menu.Placement = PlacementMode.MousePoint;

        if (windows is { Count: > 0 })
        {
            foreach (var win in windows)
            {
                var winItem = DockMenu.Item(win.Title, isChecked: win.IsActive);
                var hwnd = win.Hwnd;
                winItem.Click += (_, _) => onActivateWindow?.Invoke(hwnd);
                menu.Items.Add(winItem);
            }

            if (windows.Count > 1 && onCloseWindow is not null)
            {
                var closeAll = DockMenu.DangerItem(UiText.CloseAllWindows);
                closeAll.Click += (_, _) =>
                {
                    var owner = Window.GetWindow(menu);
                    var answer = owner is null
                        ? MessageBox.Show(
                            UiText.ConfirmCloseAllWindows(windows.Count),
                            ProductInfo.Name,
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Warning)
                        : MessageBox.Show(
                            owner,
                            UiText.ConfirmCloseAllWindows(windows.Count),
                            ProductInfo.Name,
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.OK)
                    {
                        return;
                    }

                    foreach (var win in windows)
                    {
                        onCloseWindow(win.Hwnd);
                    }
                };
                menu.Items.Add(closeAll);
            }

            menu.Items.Add(DockMenu.Divider(isLight));
        }

        var open = DockMenu.Item(UiText.OpenNew);
        open.Click += (_, _) => onLaunch(item.Id);
        menu.Items.Add(open);

        if (item.Type is DockItemType.App or DockItemType.File)
        {
            var runAsAdmin = DockMenu.Item(UiText.RunAsAdmin);
            runAsAdmin.Click += (_, _) => (onLaunchAdmin ?? onLaunch)(item.Id);
            menu.Items.Add(runAsAdmin);
        }

        if (windows is not { Count: > 0 })
        {
            var hint = DockMenu.Item(UiText.SwitchExisting);
            hint.IsEnabled = false;
            menu.Items.Add(hint);
        }

        menu.Items.Add(DockMenu.Divider(isLight));
        var remove = DockMenu.DangerItem(UiText.RemoveFromDock);
        remove.Click += (_, _) => onRemove(item.Id);
        menu.Items.Add(remove);

        if (onSettings is not null || onExit is not null)
        {
            menu.Items.Add(DockMenu.Divider(isLight));
            if (onSettings is not null)
            {
                var settingsItem = DockMenu.Item(UiText.MoreSettings);
                settingsItem.Click += (_, _) => onSettings();
                menu.Items.Add(settingsItem);
            }

            if (onExit is not null)
            {
                var exitItem = DockMenu.DangerItem(UiText.ExitWinDock);
                exitItem.Click += (_, _) => onExit();
                menu.Items.Add(exitItem);
            }
        }

        return menu;
    }

    public static ContextMenu BuildPinMenu(
        DockItem item,
        bool isLight,
        DockBackdrop backdrop,
        bool backdropOk,
        DockQuickSettings quickSettings,
        Action<string> onLaunch,
        Action<string> onRemove,
        Action<string?> onSettings,
        Action<DockEdge> onEdgeChange,
        Action onAutoHideToggle,
        Action onAvoidFullscreenToggle,
        Action<ThemePreference> onThemeChange,
        Action<DockBackdrop> onBackdropChange,
        Action onRunAtStartupToggle,
        Action onExit,
        IReadOnlyList<DockWindowEntry>? windows = null,
        Action<nint>? onActivateWindow = null,
        Action<nint>? onCloseWindow = null,
        Action<string>? onLaunchAdmin = null) =>
        BuildPinMenu(
            item,
            isLight,
            backdrop,
            backdropOk,
            onLaunch,
            onRemove,
            () => onSettings(null),
            onExit,
            windows,
            onActivateWindow,
            onCloseWindow,
            onLaunchAdmin);
}
