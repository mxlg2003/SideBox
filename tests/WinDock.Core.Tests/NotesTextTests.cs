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

    [Theory]
    [InlineData("- [ ] 买咖啡", true, false)]
    [InlineData("- [x] 买咖啡", true, true)]
    [InlineData("- [X] 买咖啡", true, true)]
    [InlineData("* [ ] 买咖啡", true, false)]
    [InlineData("* [x] 买咖啡", true, true)]
    [InlineData("[ ] 买咖啡", true, false)]
    [InlineData("[x] 买咖啡", true, true)]
    [InlineData("普通的文字", false, false)]
    public void Todo_line_identification(string line, bool isTodo, bool isChecked)
    {
        Assert.Equal(isTodo, NotesText.IsTodoLine(line));
        Assert.Equal(isChecked, NotesText.IsCheckedTodo(line));
    }

    [Fact]
    public void Toggle_todo_line_cycles_state()
    {
        var original = "  - [ ] 提交代码";
        var checkedLine = NotesText.ToggleTodoLine(original);
        Assert.Equal("  - [x] 提交代码", checkedLine);

        var uncheckedAgain = NotesText.ToggleTodoLine(checkedLine);
        Assert.Equal("  - [ ] 提交代码", uncheckedAgain);

        var plain = "普通任务";
        Assert.Equal("- [ ] 普通任务", NotesText.ToggleTodoLine(plain));
    }

    [Fact]
    public void Todo_stats_and_dock_preview_format()
    {
        var notes = "- [x] 任务一\n- [ ] 任务二\n- [ ] 任务三";
        var (done, total, firstPending) = NotesText.GetTodoStats(notes);
        Assert.Equal(1, done);
        Assert.Equal(3, total);
        Assert.Equal("任务二", firstPending);

        Assert.Equal("☑ 1/3 · 任务二", NotesText.DockPreview(notes));
    }

    [Fact]
    public void Title_takes_precedence_over_todo_preview()
    {
        var notes = "### 今日待办\n- [x] 任务一\n- [ ] 任务二";
        Assert.Equal("今日待办", NotesText.DockPreview(notes));
    }
}
