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
        Action<PinPickKind> onPinAdd,
        Action onSettings,
        Action<string> onProfileSwitch,
        Action onExit)
    {
        var menu = DockMenu.Create(isLight, backdrop, backdropOk);
        var addApp = DockMenu.Item(UiText.AddApp);
        addApp.Click += (_, _) => onPinAdd(PinPickKind.InstalledApp);
        menu.Items.Add(addApp);

        var addShortcut = DockMenu.Item(UiText.AddShortcut);
        addShortcut.Click += (_, _) => onPinAdd(PinPickKind.File);
        menu.Items.Add(addShortcut);

        var addFolder = DockMenu.Item(UiText.AddFolder);
        addFolder.Click += (_, _) => onPinAdd(PinPickKind.Folder);
        menu.Items.Add(addFolder);

        menu.Items.Add(DockMenu.Divider(isLight));
        var settingsItem = DockMenu.Item(UiText.Settings);
        settingsItem.Click += (_, _) => onSettings();
        menu.Items.Add(settingsItem);

        if (profiles.Count > 0)
        {
            menu.Items.Add(DockMenu.Divider(isLight));
            var section = DockMenu.Item(UiText.ProfilesSection);
            section.IsEnabled = false;
            menu.Items.Add(section);
            foreach (var profile in profiles)
            {
                var item = DockMenu.Item(DefaultProfiles.DisplayName(profile), isChecked: profile.Id == activeProfileId);
                var id = profile.Id;
                item.Click += (_, _) => onProfileSwitch(id);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(DockMenu.Divider(isLight));
        var exitItem = DockMenu.DangerItem(UiText.ExitWinDock);
        exitItem.Click += (_, _) => onExit();
        menu.Items.Add(exitItem);

        return menu;
    }

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
                var settingsItem = DockMenu.Item(UiText.Settings);
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
        Action<nint>? onCloseWindow = null)
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
                var settingsItem = DockMenu.Item(UiText.Settings);
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
}
