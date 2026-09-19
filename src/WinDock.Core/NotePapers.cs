namespace WinDock.Core;

/// <summary>Dockset-style sticky-note paper colors.</summary>
public static class NotePapers
{
    public const string Yellow = "yellow";
    public const string Orange = "orange";
    public const string Pink = "pink";
    public const string Magenta = "magenta";
    public const string Blue = "blue";
    public const string Green = "green";
    public const string Default = Yellow;

    public static readonly IReadOnlyList<string> All =
    [
        Yellow,
        Orange,
        Pink,
        Magenta,
        Blue,
        Green
    ];

    public static bool IsKnown(string? id) =>
        id is not null && All.Contains(id, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Default;
        }

        return All.FirstOrDefault(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    public static uint Fill(string? id) => Normalize(id) switch
    {
        Orange => 0xFFF5A24A,
        Pink => 0xFFEE6B7A,
        Magenta => 0xFFD45EC8,
        Blue => 0xFF4BA6F5,
        Green => 0xFF4FCF7C,
        _ => 0xFFF6D15C
    };

    public const uint Ink = 0xFF1A1A1A;
}
