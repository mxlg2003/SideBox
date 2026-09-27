namespace WinDock.Core;

public static class NotesText
{
    public const string TitleMarker = "###";

    public static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    public static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r", "", StringComparison.Ordinal);
        var nl = normalized.IndexOf('\n');
        var line = nl < 0 ? normalized : normalized[..nl];
        return line.Trim();
    }

    public static bool TryTitle(string? text, out string title)
    {
        var line = FirstLine(text);
        if (!line.StartsWith(TitleMarker, StringComparison.Ordinal))
        {
            title = string.Empty;
            return false;
        }

        title = line[TitleMarker.Length..].Trim();
        return true;
    }

    public static bool HasTitle(string? text) =>
        TryTitle(text, out var title) && !string.IsNullOrEmpty(title);

    public static string DockPreview(string? text, string? empty = null)
    {
        empty ??= UiText.NotesWriteSomething;
        if (IsBlank(text))
        {
            return empty;
        }

        if (HasTitle(text))
        {
            TryTitle(text, out var title);
            return title;
        }

        var (done, total, firstPending) = GetTodoStats(text);
        if (total > 0)
        {
            var summary = $"☑ {done}/{total}";
            if (!string.IsNullOrWhiteSpace(firstPending))
            {
                summary += $" · {firstPending}";
            }
            return summary;
        }

        var preview = TakeLines(Body(text), 2);
        return string.IsNullOrEmpty(preview) ? empty : preview;
    }

    public static bool IsTodoLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- [ ] ", StringComparison.Ordinal)
            || trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("* [ ] ", StringComparison.Ordinal)
            || trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("[ ] ", StringComparison.Ordinal)
            || trimmed.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCheckedTodo(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToggleTodoLine(string line)
    {
        var leadingSpaces = line.Length - line.TrimStart().Length;
        var indent = line[..leadingSpaces];
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal))
        {
            return indent + "- [x] " + trimmed[6..];
        }
        if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            return indent + "- [ ] " + trimmed[6..];
        }
        if (trimmed.StartsWith("* [ ] ", StringComparison.Ordinal))
        {
            return indent + "* [x] " + trimmed[6..];
        }
        if (trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
        {
            return indent + "* [ ] " + trimmed[6..];
        }
        if (trimmed.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            return indent + "[x] " + trimmed[4..];
        }
        if (trimmed.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            return indent + "[ ] " + trimmed[4..];
        }

        return indent + "- [ ] " + trimmed;
    }

    public static (int Done, int Total, string? FirstPending) GetTodoStats(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (0, 0, null);
        }

        var lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var done = 0;
        var total = 0;
        string? firstPending = null;

        foreach (var raw in lines)
        {
            if (!IsTodoLine(raw))
            {
                continue;
            }

            total++;
            if (IsCheckedTodo(raw))
            {
                done++;
            }
            else if (firstPending is null)
            {
                var content = raw.TrimStart();
                var prefixLen = content.StartsWith("- [ ] ", StringComparison.Ordinal) || content.StartsWith("* [ ] ", StringComparison.Ordinal) ? 6 : 4;
                firstPending = content[prefixLen..].Trim();
            }
        }

        return (done, total, firstPending);
    }

    public static string ListPreview(string? text, string? empty = null)
    {
        empty ??= UiText.WidgetNotes;
        if (HasTitle(text))
        {
            TryTitle(text, out var title);
            return title;
        }

        var line = FirstLine(Body(text));
        return string.IsNullOrEmpty(line) ? empty : line;
    }

    private static string Body(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !TryTitle(text, out _))
        {
            return text ?? string.Empty;
        }

        var normalized = text.Replace("\r", "", StringComparison.Ordinal);
        var nl = normalized.IndexOf('\n');
        return nl < 0 ? string.Empty : normalized[(nl + 1)..];
    }

    private static string TakeLines(string? text, int count)
    {
        if (string.IsNullOrWhiteSpace(text) || count <= 0)
        {
            return string.Empty;
        }

        var lines = text.Replace("\r", "", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(count);
        return string.Join('\n', lines);
    }
}
