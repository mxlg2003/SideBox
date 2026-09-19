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

    public static string DockPreview(string? text, string empty = "写点什么…")
    {
        if (IsBlank(text))
        {
            return empty;
        }

        if (HasTitle(text))
        {
            TryTitle(text, out var title);
            return title;
        }

        var preview = TakeLines(Body(text), 2);
        return string.IsNullOrEmpty(preview) ? empty : preview;
    }

    public static string ListPreview(string? text, string empty = "便笺")
    {
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
