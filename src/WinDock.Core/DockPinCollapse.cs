namespace WinDock.Core;

public enum DockPinStackKind
{
    Apps,
    Folders
}

public sealed record DockVisibleSlot
{
    public DockItem? Item { get; init; }

    public DockPinStackKind? Stack { get; init; }

    public IReadOnlyList<DockItem> Members { get; init; } = [];

    public bool IsStack => Stack is not null;

    public string Key => IsStack ? "stack:" + Stack : Item?.Id ?? string.Empty;

    public static DockVisibleSlot FromItem(DockItem item) => new() { Item = item };

    public static DockVisibleSlot FromStack(DockPinStackKind kind, IReadOnlyList<DockItem> members) =>
        new() { Stack = kind, Members = members };
}

public static class DockPinCollapse
{
    public static bool IsAppPin(DockItem item) =>
        DockPins.IsPin(item.Type) && item.Type != DockItemType.Folder;

    public static bool IsFolderPin(DockItem item) => item.Type == DockItemType.Folder;

    public static string StackTitle(DockPinStackKind kind, int count) =>
        kind == DockPinStackKind.Apps ? UiText.AppsStack(count) : UiText.FoldersStack(count);

    public static IReadOnlyList<DockVisibleSlot> Project(
        IReadOnlyList<DockItem> items,
        bool collapseApps,
        bool collapseFolders)
    {
        items ??= [];
        var foldApps = collapseApps && items.Count(IsAppPin) >= 2;
        var foldFolders = collapseFolders && items.Count(IsFolderPin) >= 2;
        if (!foldApps && !foldFolders)
        {
            return items.Select(DockVisibleSlot.FromItem).ToArray();
        }

        var slots = new List<DockVisibleSlot>(items.Count);
        var appsEmitted = false;
        var foldersEmitted = false;
        foreach (var item in items)
        {
            if (foldApps && IsAppPin(item))
            {
                if (!appsEmitted)
                {
                    slots.Add(DockVisibleSlot.FromStack(DockPinStackKind.Apps, items.Where(IsAppPin).ToArray()));
                    appsEmitted = true;
                }

                continue;
            }

            if (foldFolders && IsFolderPin(item))
            {
                if (!foldersEmitted)
                {
                    slots.Add(DockVisibleSlot.FromStack(DockPinStackKind.Folders, items.Where(IsFolderPin).ToArray()));
                    foldersEmitted = true;
                }

                continue;
            }

            slots.Add(DockVisibleSlot.FromItem(item));
        }

        return slots;
    }

    public static IReadOnlyList<DockItem> Reorder(
        IReadOnlyList<DockItem> items,
        bool collapseApps,
        bool collapseFolders,
        int fromVisible,
        int toVisible)
    {
        items ??= [];
        var slots = Project(items, collapseApps, collapseFolders).ToList();
        if (fromVisible < 0 || fromVisible >= slots.Count || toVisible < 0 || toVisible >= slots.Count
            || fromVisible == toVisible)
        {
            return items;
        }

        var moved = slots[fromVisible];
        slots.RemoveAt(fromVisible);
        slots.Insert(toVisible, moved);
        return Expand(slots);
    }

    public static int InsertIndex(
        IReadOnlyList<DockItem> items,
        bool collapseApps,
        bool collapseFolders,
        int visibleInsertAt)
    {
        items ??= [];
        var slots = Project(items, collapseApps, collapseFolders);
        if (slots.Count == 0 || visibleInsertAt <= 0)
        {
            return 0;
        }

        if (visibleInsertAt >= slots.Count)
        {
            return items.Count;
        }

        var before = slots[visibleInsertAt];
        var anchor = before.IsStack ? before.Members[0] : before.Item;
        if (anchor is null)
        {
            return items.Count;
        }

        var index = items.ToList().FindIndex(item => item.Id == anchor.Id);
        return index < 0 ? items.Count : index;
    }

    private static IReadOnlyList<DockItem> Expand(IEnumerable<DockVisibleSlot> slots)
    {
        var items = new List<DockItem>();
        foreach (var slot in slots)
        {
            if (slot.IsStack)
            {
                items.AddRange(slot.Members);
            }
            else if (slot.Item is not null)
            {
                items.Add(slot.Item);
            }
        }

        return items;
    }
}
