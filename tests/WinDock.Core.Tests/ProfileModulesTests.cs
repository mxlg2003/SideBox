using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class ProfileModulesTests
{
    [Fact]
    public void Factory_profiles_list_their_dock_widgets()
    {
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            var settings = DefaultProfiles.CreateFactorySettings();
            var daily = settings.Profiles.Single(profile => profile.Id == DefaultProfiles.GeneralId);
            var deep = settings.Profiles.Single(profile => profile.Id == DefaultProfiles.DeepWorkId);

            Assert.Equal(
                [BuiltInWidgets.Clock, BuiltInWidgets.Notes],
                ProfileModules.OnDock(daily).Select(chip => chip.WidgetId));
            Assert.Equal(["时钟", "便笺"], ProfileModules.OnDock(daily).Select(chip => chip.Label));
            Assert.Equal(
                [BuiltInWidgets.FocusTimer, BuiltInWidgets.Notes],
                ProfileModules.OnDock(deep).Select(chip => chip.WidgetId));
        }
    }

    [Fact]
    public void Pins_and_hidden_widgets_are_omitted()
    {
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            var profile = DefaultProfiles.CreateGeneral() with
            {
                Dock = new DockLayout
                {
                    Items =
                    [
                        new DockItem { Type = DockItemType.App, Path = @"C:\Windows\notepad.exe", Title = "Notepad" },
                        new DockItem { Type = DockItemType.WidgetChip, WidgetId = "missing" },
                        new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.NowPlaying },
                        new DockItem { Type = DockItemType.WidgetChip, WidgetId = BuiltInWidgets.Notes }
                    ]
                }
            };

            var modules = ProfileModules.OnDock(profile);
            Assert.Equal([BuiltInWidgets.Notes], modules.Select(chip => chip.WidgetId));
        }
    }

    [Fact]
    public void Copies_keep_the_detail_that_tells_them_apart()
    {
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            var profile = DefaultProfiles.CreateGeneral() with
            {
                Dock = new DockLayout
                {
                    Items =
                    [
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = BuiltInWidgets.Alarm,
                            Alarm = new AlarmSettings { Hour = 7, Minute = 30, Label = "起床" }
                        },
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = BuiltInWidgets.Alarm,
                            Alarm = new AlarmSettings { Hour = 22, Minute = 0 }
                        },
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = BuiltInWidgets.Stocks,
                            Stocks = new StockWidgetSettings
                            {
                                Symbol = new StockSymbol { Market = "us", Symbol = "AAPL", Name = "Apple" }
                            }
                        },
                        new DockItem
                        {
                            Type = DockItemType.WidgetChip,
                            WidgetId = BuiltInWidgets.Weather,
                            Weather = new WeatherSettings { City = "上海" }
                        }
                    ]
                }
            };

            var labels = ProfileModules.OnDock(profile).Select(chip => chip.Label).ToArray();
            Assert.Equal(["闹钟 · 起床 07:30", "闹钟 · 22:00", "股票 · Apple", "天气 · 上海"], labels);
        }
    }
}
