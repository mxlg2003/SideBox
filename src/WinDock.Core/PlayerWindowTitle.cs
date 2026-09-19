namespace WinDock.Core;

/// <summary>Turns a player window caption into a media title.</summary>
public static class PlayerWindowTitle
{
    public static string? Sanitize(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return null;
        }

        var text = windowTitle.Trim();
        foreach (var marker in new[] { " - PotPlayer", " - 网易云音乐", " - Spotify" })
        {
            var index = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                var trimmed = text[..index].Trim();
                return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            }
        }

        return text;
    }

    public static bool LooksLikeMediaTitle(string? title, string? playerDisplayName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerDisplayName)
            && string.Equals(title, playerDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return title.Contains("potplayer", StringComparison.OrdinalIgnoreCase) is false;
    }
}
