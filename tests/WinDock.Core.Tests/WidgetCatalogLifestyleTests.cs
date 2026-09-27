using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class WidgetCatalogLifestyleTests
{
    [Fact]
    public void Catalog_contains_lifestyle_group()
    {
        var group = WidgetCatalog.Groups.FirstOrDefault(g => g.Id == "lifestyle");
        Assert.NotNull(group);
        Assert.Contains(BuiltInWidgets.Weather, group.WidgetIds);
        Assert.Contains(BuiltInWidgets.Schedule, group.WidgetIds);
    }

    [Theory]
    [InlineData("天气", true, false)]
    [InlineData("气温", true, false)]
    [InlineData("weather", true, false)]
    [InlineData("日程", false, true)]
    [InlineData("倒数", false, true)]
    [InlineData("todo", false, true)]
    [InlineData("calendar", false, true)]
    public void Keyword_matching_finds_new_widgets(string query, bool matchesWeather, bool matchesSchedule)
    {
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal(matchesWeather, WidgetCatalog.Matches(BuiltInWidgets.Weather, query));
            Assert.Equal(matchesSchedule, WidgetCatalog.Matches(BuiltInWidgets.Schedule, query));
        }
    }
}
