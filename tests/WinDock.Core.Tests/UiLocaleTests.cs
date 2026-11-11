using System.Globalization;
using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class UiLocaleTests
{
    [Fact]
    public void Tests_pin_chinese()
    {
        Assert.True(UiLocale.IsChinese);
        Assert.Equal("设置", UiText.Settings);
        Assert.Equal("便笺", BuiltInWidgets.DisplayName(BuiltInWidgets.Notes));
    }

    [Fact]
    public void Push_overrides_copy_without_changing_preference()
    {
        var preference = UiLocale.Preference;
        using (UiLocale.Push(UiLanguage.English))
        {
            Assert.False(UiLocale.IsChinese);
            Assert.Equal("Settings", UiText.Settings);
            Assert.Equal("Notes", BuiltInWidgets.DisplayName(BuiltInWidgets.Notes));
        }

        Assert.Equal(preference, UiLocale.Preference);
        Assert.Equal("设置", UiText.Settings);
    }

    [Fact]
    public void Search_matches_both_languages()
    {
        Assert.True(BuiltInWidgets.MatchesSearch(BuiltInWidgets.Clock, "clock"));
        Assert.True(BuiltInWidgets.MatchesSearch(BuiltInWidgets.Clock, "时钟"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Stopwatch, "stopwatch"));
        Assert.True(WidgetCatalog.Matches(BuiltInWidgets.Stopwatch, "秒表"));
    }

    [Fact]
    public void Apply_english_then_restore()
    {
        try
        {
            UiLocale.Apply(UiLanguage.English);
            Assert.Equal("Widgets", UiText.Modules);
            Assert.Equal("Ready", StopwatchFormat.Ready);
        }
        finally
        {
            UiLocale.Apply(UiLanguage.Chinese);
        }

        Assert.Equal("模块", UiText.Modules);
        Assert.Equal("就绪", StopwatchFormat.Ready);
    }

    [Fact]
    public void Navigation_and_settings_groups_localized()
    {
        Assert.Equal("基础设置", UiText.NavGroupBasic);
        Assert.Equal("核心功能", UiText.NavGroupCore);
        Assert.Equal("系统与支持", UiText.NavGroupSystemAndSupport);
        Assert.Equal("模块广场", UiText.NavModules);
        Assert.Equal("情景模式", UiText.NavProfiles);
        Assert.Equal("快捷键中心", UiText.NavHotkeys);

        using (UiLocale.Push(UiLanguage.English))
        {
            Assert.Equal("Basic Settings", UiText.NavGroupBasic);
            Assert.Equal("Core Features", UiText.NavGroupCore);
            Assert.Equal("System & Support", UiText.NavGroupSystemAndSupport);
            Assert.Equal("Widgets", UiText.NavModules);
            Assert.Equal("Profiles", UiText.NavProfiles);
            Assert.Equal("Shortcuts", UiText.NavHotkeys);
        }
    }

    [Fact]
    public void IsChineseCulture_reads_zh_prefix()
    {
        Assert.True(UiLocale.IsChineseCulture(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.True(UiLocale.IsChineseCulture(CultureInfo.GetCultureInfo("zh-Hans")));
        Assert.False(UiLocale.IsChineseCulture(CultureInfo.GetCultureInfo("en-US")));
    }
}
