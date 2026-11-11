using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class FreeEditionTests
{
    [Fact]
    public void All_offered_widgets_can_be_added_for_free()
    {
        foreach (var widgetId in BuiltInWidgets.All)
        {
            if (BuiltInWidgets.IsOffered(widgetId))
            {
                Assert.True(BuiltInWidgets.CanAdd(widgetId), $"Widget '{widgetId}' should be addable for free.");
                Assert.True(BuiltInWidgets.IsComplimentary(widgetId), $"Widget '{widgetId}' should be complimentary.");
            }
        }
    }

    [Fact]
    public void Previously_paid_widgets_are_now_free_and_addable()
    {
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Weather));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Stocks));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.WhiteNoise));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Disk));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Gpu));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.FocusTimer));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Countdown));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Alarm));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.TimeProgress));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.Wallpaper));
        Assert.True(BuiltInWidgets.CanAdd(BuiltInWidgets.BingWallpaper));
    }

    [Fact]
    public void Settings_editor_can_add_all_offered_widgets_without_license()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Weather);
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Stocks);
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.WhiteNoise);

        var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        Assert.Contains(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Weather);
        Assert.Contains(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.Stocks);
        Assert.Contains(general.Dock.Items, item => item.WidgetId == BuiltInWidgets.WhiteNoise);
    }

    [Fact]
    public void SettingsValidator_works_cleanly_without_license_code()
    {
        var settings = new AppSettings
        {
            License = new LicenseSettings()
        };

        var normalized = SettingsValidator.EnsureInvariants(settings);
        Assert.NotNull(normalized.License);
    }
}
