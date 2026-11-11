using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

#if SHELL
namespace WinDock.Shell;
#else
namespace WinDock.Widgets;
#endif

/// <summary>
/// Folder names sit on a transparent badge over the icon. Chinese uses the hinted UI face
/// (same fallback as notes) with grayscale antialiasing so strokes stay crisp.
/// </summary>
internal static class FolderCaption
{
    private static readonly FontFamily Latin = new("Tahoma");
    private static readonly FontFamily Cjk = new("Microsoft YaHei UI, Microsoft YaHei");

    public static void Apply(TextBlock label, string text)
    {
        label.Inlines.Clear();
        foreach (var rune in text.EnumerateRunes())
        {
            label.Inlines.Add(new Run(rune.ToString())
            {
                FontFamily = IsCjk(rune) ? Cjk : Latin
            });
        }

        label.UseLayoutRounding = true;
        label.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(label, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(label, TextHintingMode.Fixed);
    }

    private static bool IsCjk(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x3400 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF
            or >= 0x3040 and <= 0x30FF
            or >= 0xAC00 and <= 0xD7AF
            or >= 0x20000 and <= 0x2CEAF;
    }
}
