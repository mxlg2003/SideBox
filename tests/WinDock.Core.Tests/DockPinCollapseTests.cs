using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class DockPinCollapseTests
{
    [Fact]
    public void Project_leaves_items_alone_when_flags_are_off()
    {
        var items = Mixed();
        var slots = DockPinCollapse.Project(items, collapseApps: false, collapseFolders: false);
        Assert.Equal(items.Select(item => item.Id), slots.Select(slot => slot.Item?.Id));
        Assert.All(slots, slot => Assert.False(slot.IsStack));
    }

    [Fact]
    public void Project_does_not_stack_a_single_app_or_folder()
    {
        var items = new[]
        {
            Chip("clock"),
            App("chrome"),
            Folder("downloads")
        };
        var slots = DockPinCollapse.Project(items, collapseApps: true, collapseFolders: true);
        Assert.Equal(3, slots.Count);
        Assert.All(slots, slot => Assert.False(slot.IsStack));
    }

    [Fact]
    public void Project_stacks_apps_and_folders_at_first_pin_of_each_kind()
    {
        var items = Mixed();
        var slots = DockPinCollapse.Project(items, collapseApps: true, collapseFolders: true);
        Assert.Equal(4, slots.Count);
        Assert.Equal("clock", slots[0].Item?.Id);
        Assert.Equal(DockPinStackKind.Apps, slots[1].Stack);
        Assert.Equal(["chrome", "firefox"], slots[1].Members.Select(item => item.Id));
        Assert.Equal("notes", slots[2].Item?.Id);
        Assert.Equal(DockPinStackKind.Folders, slots[3].Stack);
        Assert.Equal(["downloads", "docs"], slots[3].Members.Select(item => item.Id));
    }

    [Fact]
    public void Reorder_moves_a_whole_app_stack()
    {
        var items = Mixed();
        var next = DockPinCollapse.Reorder(items, collapseApps: true, collapseFolders: true, fromVisible: 1, toVisible: 3);
        Assert.Equal(
            ["clock", "notes", "downloads", "docs", "chrome", "firefox"],
            next.Select(item => item.Id));
    }

    [Fact]
    public void InsertIndex_maps_visible_gap_onto_the_real_list()
    {
        var items = Mixed();
        Assert.Equal(0, DockPinCollapse.InsertIndex(items, true, true, 0));
        Assert.Equal(1, DockPinCollapse.InsertIndex(items, true, true, 1));
        Assert.Equal(2, DockPinCollapse.InsertIndex(items, true, true, 2));
        Assert.Equal(3, DockPinCollapse.InsertIndex(items, true, true, 3));
        Assert.Equal(6, DockPinCollapse.InsertIndex(items, true, true, 4));
    }

    [Fact]
    public void ReplaceDockItems_round_trips()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        var pin = DockPins.FromTarget(@"C:\Apps\Demo\app.exe", "Demo");
        var added = SettingsEditor.AddPin(settings, DefaultProfiles.GeneralId, pin);
        var general = added.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
        var reversed = general.Dock.Items.Reverse().ToArray();
        var next = SettingsEditor.ReplaceDockItems(added, DefaultProfiles.GeneralId, reversed);
        Assert.Equal(
            reversed.Select(item => item.Id),
            next.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items.Select(item => item.Id));
    }

    private static IReadOnlyList<DockItem> Mixed() =>
    [
        Chip("clock"),
        App("chrome"),
        Chip("notes"),
        Folder("downloads"),
        App("firefox"),
        Folder("docs")
    ];

    private static DockItem Chip(string id) => new()
    {
        Id = id,
        Type = DockItemType.WidgetChip,
        WidgetId = id,
        Title = id
    };

    private static DockItem App(string id) => new()
    {
        Id = id,
        Type = DockItemType.App,
        Path = $@"C:\Apps\{id}.exe",
        Title = id
    };

    private static DockItem Folder(string id) => new()
    {
        Id = id,
        Type = DockItemType.Folder,
        Path = $@"C:\Pins\{id}\",
        Title = id
    };
}
