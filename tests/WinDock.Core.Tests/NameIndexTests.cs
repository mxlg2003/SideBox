using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class NameIndexTests
{
    [Theory]
    [InlineData("Chrome", "C")]
    [InlineData("visual studio", "V")]
    [InlineData("微信", "W")]
    [InlineData("记事本", "J")]
    [InlineData("钉钉", "D")]
    [InlineData("7-Zip", "#")]
    public void Letter_uses_latin_or_pinyin_initial(string name, string letter) =>
        Assert.Equal(letter, NameIndex.Letter(name));

    [Fact]
    public void Compare_orders_by_initial_then_name_and_puts_symbols_last()
    {
        string[] names = ["微信", "记事本", "Chrome", "7-Zip", "Calculator"];
        var ordered = names.OrderBy(name => name, Comparer<string>.Create(NameIndex.Compare)).ToArray();
        Assert.Equal(["Calculator", "Chrome", "记事本", "微信", "7-Zip"], ordered);
    }
}
