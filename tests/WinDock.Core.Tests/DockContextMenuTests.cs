using System.Windows.Controls;
using WinDock.Core;
using WinDock.Shell;
using Xunit;

namespace WinDock.Core.Tests;

public sealed class DockContextMenuTests
{
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw new AggregateException(error);
        }
    }

    [Fact]
    public void DockQuickSettings_DefaultValues()
    {
        var settings = new DockQuickSettings();
        Assert.Equal(DockEdge.Top, settings.Edge);
        Assert.False(settings.AutoHide);
        Assert.True(settings.AvoidFullscreen);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(DockBackdrop.Mica, settings.Backdrop);
        Assert.False(settings.RunAtStartup);
    }

    [Fact]
    public void BuildProfileMenu_CreatesSettingsAndTogglesProperly()
    {
        RunSta(() =>
        {
            var quickSettings = new DockQuickSettings
            {
                Edge = DockEdge.Left,
                AutoHide = true,
                AvoidFullscreen = false,
                Theme = ThemePreference.Dark,
                Backdrop = DockBackdrop.Glass,
                RunAtStartup = true
            };

            var profiles = new List<Profile>
            {
                new() { Id = "general", Name = "General" },
                new() { Id = "work", Name = "Work" }
            };

            string? launchedSettingPage = null;
            DockEdge? requestedEdge = null;
            var autoHideToggled = false;
            var avoidFullscreenToggled = false;
            ThemePreference? requestedTheme = null;
            DockBackdrop? requestedBackdrop = null;
            var startupToggled = false;
            string? switchedProfile = null;

            var menu = DockContextMenuBuilder.BuildProfileMenu(
                profiles,
                "work",
                isLight: false,
                backdrop: DockBackdrop.Mica,
                backdropOk: true,
                quickSettings,
                onPinAdd: _ => { },
                onSettings: page => launchedSettingPage = page,
                onProfileSwitch: id => switchedProfile = id,
                onEdgeChange: edge => requestedEdge = edge,
                onAutoHideToggle: () => autoHideToggled = true,
                onAvoidFullscreenToggle: () => avoidFullscreenToggled = true,
                onThemeChange: theme => requestedTheme = theme,
                onBackdropChange: bd => requestedBackdrop = bd,
                onRunAtStartupToggle: () => startupToggled = true,
                onExit: () => { });

            Assert.NotNull(menu);

            // Find AddWidget item and test click
            var addWidget = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AddWidget);
            Assert.NotNull(addWidget);
            addWidget.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal("modules", launchedSettingPage);

            // Find Edge submenu
            var edgeMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.DockPosition);
            Assert.NotNull(edgeMenu);
            var edgeItems = edgeMenu.Items.OfType<MenuItem>().ToList();
            Assert.Equal(4, edgeItems.Count);

            var edgeTop = edgeItems.First(i => (string)i.Header == UiText.PositionTop);
            var edgeLeft = edgeItems.First(i => (string)i.Header == UiText.PositionLeft);
            Assert.False(edgeTop.IsChecked);
            Assert.True(edgeLeft.IsChecked);

            edgeTop.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(DockEdge.Top, requestedEdge);

            // Find Theme submenu
            var themeMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Theme);
            Assert.NotNull(themeMenu);
            var themeItems = themeMenu.Items.OfType<MenuItem>().ToList();
            Assert.Equal(3, themeItems.Count);

            var themeDark = themeItems.First(i => (string)i.Header == UiText.ThemeDark);
            Assert.True(themeDark.IsChecked);
            var themeLight = themeItems.First(i => (string)i.Header == UiText.ThemeLight);
            Assert.False(themeLight.IsChecked);

            themeLight.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(ThemePreference.Light, requestedTheme);

            // Find Backdrop submenu
            var backdropMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Backdrop);
            Assert.NotNull(backdropMenu);
            var backdropItems = backdropMenu.Items.OfType<MenuItem>().ToList();
            Assert.Equal(3, backdropItems.Count);

            var backdropGlass = backdropItems.First(i => (string)i.Header == UiText.BackdropGlassShort);
            Assert.True(backdropGlass.IsChecked);

            var backdropMica = backdropItems.First(i => (string)i.Header == UiText.BackdropMicaShort);
            backdropMica.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(DockBackdrop.Mica, requestedBackdrop);

            // Toggles
            var autoHideItem = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AutoHide);
            Assert.NotNull(autoHideItem);
            Assert.True(autoHideItem.IsChecked);
            autoHideItem.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(autoHideToggled);

            var avoidFullscreenItem = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AvoidFullscreen);
            Assert.NotNull(avoidFullscreenItem);
            Assert.False(avoidFullscreenItem.IsChecked);
            avoidFullscreenItem.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(avoidFullscreenToggled);

            var startupItem = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RunAtStartup);
            Assert.NotNull(startupItem);
            Assert.True(startupItem.IsChecked);
            startupItem.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(startupToggled);

            // Profiles submenu should not appear when ProductFeatures.ProfilesEnabled is false
            var profilesMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.ProfilesSection);
            Assert.Null(profilesMenu);

            var settingsItem = menu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.MoreSettings);
            Assert.NotNull(settingsItem);
        });
    }

    [Fact]
    public void BuildPinMenu_And_BuildWidgetMenu_DoNotIncludeGlobalDockSettings()
    {
        RunSta(() =>
        {
            var pinItem = new DockItem { Id = "pin-1", Type = DockItemType.App };
            var widgetItem = new DockItem { Id = "w-1", Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.Clock };

            var pinMenu = DockContextMenuBuilder.BuildPinMenu(
                pinItem,
                isLight: true,
                backdrop: DockBackdrop.Mica,
                backdropOk: true,
                onLaunch: _ => { },
                onRemove: _ => { },
                onSettings: () => { },
                onExit: () => { });

            Assert.NotNull(pinMenu);
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.DockPosition));
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AutoHide));
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Theme));
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Backdrop));
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AvoidFullscreen));
            Assert.Null(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RunAtStartup));
            Assert.NotNull(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.OpenNew));
            Assert.NotNull(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RunAsAdmin));
            Assert.NotNull(pinMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RemoveFromDock));

            var widgetMenu = DockContextMenuBuilder.BuildWidgetMenu(
                widgetItem,
                isLight: true,
                backdrop: DockBackdrop.Mica,
                backdropOk: true,
                onOpen: _ => { },
                onCustomize: _ => { },
                onDensity: _ => { },
                onRemove: _ => { });

            Assert.NotNull(widgetMenu);
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.DockPosition));
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AutoHide));
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Theme));
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.Backdrop));
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.AvoidFullscreen));
            Assert.Null(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RunAtStartup));
            Assert.NotNull(widgetMenu.Items.OfType<MenuItem>().FirstOrDefault(i => (string)i.Header == UiText.RemoveFromDock));
        });
    }

    [Fact]
    public void BuildProfileMenu_BackwardCompatibleOverload_StillWorks()
    {
        RunSta(() =>
        {
            var menu = DockContextMenuBuilder.BuildProfileMenu(
                [],
                "",
                isLight: true,
                backdrop: DockBackdrop.Mica,
                backdropOk: true,
                onPinAdd: _ => { },
                onSettings: () => { },
                onProfileSwitch: _ => { },
                onExit: () => { });

            Assert.NotNull(menu);
            Assert.NotEmpty(menu.Items);
        });
    }
}
