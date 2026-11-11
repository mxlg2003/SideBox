using System.Globalization;
using System.Text;

namespace WinDock.Core;

/// <summary>Sorts display names by Latin or pinyin initial, with non-letters after Z.</summary>
public static class NameIndex
{
    public const string Rail = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#";

    private static readonly StringComparer NameComparer = StringComparer.Create(new CultureInfo("zh-CN"), true);

    static NameIndex()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Letter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "#";
        }

        var c = name.Trim()[0];
        if (c is >= 'A' and <= 'Z')
        {
            return c.ToString();
        }

        if (c is >= 'a' and <= 'z')
        {
            return char.ToUpperInvariant(c).ToString();
        }

        if (c is >= 'Ａ' and <= 'Ｚ')
        {
            return ((char)('A' + (c - 'Ａ'))).ToString();
        }

        if (c is >= 'ａ' and <= 'ｚ')
        {
            return ((char)('A' + (c - 'ａ'))).ToString();
        }

        if (c is >= '一' and <= '龥')
        {
            var initial = PinyinInitial(c);
            return initial is >= 'A' and <= 'Z' ? initial.ToString() : "#";
        }

        return "#";
    }

    public static int Compare(string? left, string? right)
    {
        var rank = Rank(Letter(left)).CompareTo(Rank(Letter(right)));
        return rank != 0 ? rank : NameComparer.Compare(left, right);
    }

    private static int Rank(string letter) => letter.Length == 1 && letter[0] is >= 'A' and <= 'Z' ? letter[0] - 'A' : 26;

    private static char PinyinInitial(char character)
    {
        var bytes = Encoding.GetEncoding("GB2312").GetBytes(character.ToString());
        if (bytes.Length < 2)
        {
            return '#';
        }

        var code = ((bytes[0] << 8) + bytes[1]) - 65536;
        if (code is >= -20319 and <= -20284) return 'A';
        if (code is >= -20283 and <= -19776) return 'B';
        if (code is >= -19775 and <= -19219) return 'C';
        if (code is >= -19218 and <= -18711) return 'D';
        if (code is >= -18710 and <= -18527) return 'E';
        if (code is >= -18526 and <= -18240) return 'F';
        if (code is >= -18239 and <= -17923) return 'G';
        if (code is >= -17922 and <= -17418) return 'H';
        if (code is >= -17417 and <= -16475) return 'J';
        if (code is >= -16474 and <= -16213) return 'K';
        if (code is >= -16212 and <= -15641) return 'L';
        if (code is >= -15640 and <= -15166) return 'M';
        if (code is >= -15165 and <= -14923) return 'N';
        if (code is >= -14922 and <= -14915) return 'O';
        if (code is >= -14914 and <= -14631) return 'P';
        if (code is >= -14630 and <= -14150) return 'Q';
        if (code is >= -14149 and <= -14091) return 'R';
        if (code is >= -14090 and <= -13319) return 'S';
        if (code is >= -13318 and <= -12839) return 'T';
        if (code is >= -12838 and <= -12557) return 'W';
        if (code is >= -12556 and <= -11848) return 'X';
        if (code is >= -11847 and <= -11056) return 'Y';
        if (code is >= -11055 and <= -10247) return 'Z';
        return '#';
    }
}
