using System.Windows.Media;

#if SHELL
namespace WinDock.Shell;
#else
namespace WinDock.Widgets;
#endif

/// <summary>
/// Latin stays on Segoe UI Variable. Chinese is listed explicitly so WPF does not
/// use Segoe's font link, which draws 关 with overlapping strokes.
/// </summary>
#if SHELL
internal static class UiFont
#else
public static class UiFont
#endif
{
    public const string FamilyName = "Segoe UI Variable, Microsoft YaHei UI, Segoe UI";

    public static readonly FontFamily Family = new(FamilyName);
}
