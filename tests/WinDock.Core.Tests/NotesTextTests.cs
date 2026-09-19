using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class NotesTextTests
{
    [Fact]
    public void First_line_with_title_marker_is_the_dock_preview()
    {
        var text = "### 购物\n牛奶\n鸡蛋";
        Assert.True(NotesText.TryTitle(text, out var title));
        Assert.Equal("购物", title);
        Assert.Equal("购物", NotesText.DockPreview(text));
    }

    [Fact]
    public void Without_title_marker_dock_shows_the_first_two_lines()
    {
        Assert.False(NotesText.TryTitle("牛奶\n鸡蛋\n面包", out _));
        Assert.Equal("牛奶\n鸡蛋", NotesText.DockPreview("牛奶\n鸡蛋\n面包"));
        Assert.Equal("牛奶", NotesText.ListPreview("牛奶\n鸡蛋"));
    }

    [Fact]
    public void Blank_note_uses_placeholder()
    {
        Assert.Equal("写点什么…", NotesText.DockPreview("  \n"));
        Assert.Equal("便笺", NotesText.DockPreview("###   ", "便笺"));
    }

    [Fact]
    public void Title_marker_without_title_shows_following_lines()
    {
        Assert.False(NotesText.HasTitle("###\n牛奶\n鸡蛋"));
        Assert.Equal("牛奶\n鸡蛋", NotesText.DockPreview("###\n牛奶\n鸡蛋"));
        Assert.Equal("牛奶", NotesText.ListPreview("###\n牛奶\n鸡蛋"));
    }

    [Fact]
    public void Has_title_requires_text_after_the_marker()
    {
        Assert.True(NotesText.HasTitle("### 购物"));
        Assert.False(NotesText.HasTitle("###   "));
        Assert.False(NotesText.HasTitle("牛奶"));
    }
}
