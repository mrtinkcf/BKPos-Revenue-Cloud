using System.Text;

namespace BKPos.Mobile.App.Pages;

internal static class VietnameseTelexEngine
{
    private const string LowerA = "a\u00E1\u00E0\u1EA3\u00E3\u1EA1";
    private const string LowerBreveA = "\u0103\u1EAF\u1EB1\u1EB3\u1EB5\u1EB7";
    private const string LowerCircumflexA = "\u00E2\u1EA5\u1EA7\u1EA9\u1EAB\u1EAD";
    private const string LowerE = "e\u00E9\u00E8\u1EBB\u1EBD\u1EB9";
    private const string LowerCircumflexE = "\u00EA\u1EBF\u1EC1\u1EC3\u1EC5\u1EC7";
    private const string LowerI = "i\u00ED\u00EC\u1EC9\u0129\u1ECB";
    private const string LowerO = "o\u00F3\u00F2\u1ECF\u00F5\u1ECD";
    private const string LowerCircumflexO = "\u00F4\u1ED1\u1ED3\u1ED5\u1ED7\u1ED9";
    private const string LowerHornO = "\u01A1\u1EDB\u1EDD\u1EDF\u1EE1\u1EE3";
    private const string LowerU = "u\u00FA\u00F9\u1EE7\u0169\u1EE5";
    private const string LowerHornU = "\u01B0\u1EE9\u1EEB\u1EED\u1EEF\u1EF1";
    private const string LowerY = "y\u00FD\u1EF3\u1EF7\u1EF9\u1EF5";

    private const string UpperA = "A\u00C1\u00C0\u1EA2\u00C3\u1EA0";
    private const string UpperBreveA = "\u0102\u1EAE\u1EB0\u1EB2\u1EB4\u1EB6";
    private const string UpperCircumflexA = "\u00C2\u1EA4\u1EA6\u1EA8\u1EAA\u1EAC";
    private const string UpperE = "E\u00C9\u00C8\u1EBA\u1EBC\u1EB8";
    private const string UpperCircumflexE = "\u00CA\u1EBE\u1EC0\u1EC2\u1EC4\u1EC6";
    private const string UpperI = "I\u00CD\u00CC\u1EC8\u0128\u1ECA";
    private const string UpperO = "O\u00D3\u00D2\u1ECE\u00D5\u1ECC";
    private const string UpperCircumflexO = "\u00D4\u1ED0\u1ED2\u1ED4\u1ED6\u1ED8";
    private const string UpperHornO = "\u01A0\u1EDA\u1EDC\u1EDE\u1EE0\u1EE2";
    private const string UpperU = "U\u00DA\u00D9\u1EE6\u0168\u1EE4";
    private const string UpperHornU = "\u01AF\u1EE8\u1EEA\u1EEC\u1EEE\u1EF0";
    private const string UpperY = "Y\u00DD\u1EF2\u1EF6\u1EF8\u1EF4";

    private static readonly string[] LowerGroups =
    [
        LowerA,
        LowerBreveA,
        LowerCircumflexA,
        LowerE,
        LowerCircumflexE,
        LowerI,
        LowerO,
        LowerCircumflexO,
        LowerHornO,
        LowerU,
        LowerHornU,
        LowerY
    ];

    private static readonly string[] UpperGroups =
    [
        UpperA,
        UpperBreveA,
        UpperCircumflexA,
        UpperE,
        UpperCircumflexE,
        UpperI,
        UpperO,
        UpperCircumflexO,
        UpperHornO,
        UpperU,
        UpperHornU,
        UpperY
    ];

    public static TelexResult ProcessInsert(string text, int cursorPosition, string input)
    {
        if (input.Length != 1)
        {
            return InsertRaw(text, cursorPosition, input);
        }

        var key = char.ToLowerInvariant(input[0]);
        if (TryApplyD(text, cursorPosition, key, out var result)
            || TryApplyShape(text, cursorPosition, key, out result)
            || TryApplyTone(text, cursorPosition, key, out result))
        {
            return result;
        }

        return InsertRaw(text, cursorPosition, input);
    }

    private static bool TryApplyD(string text, int cursorPosition, char key, out TelexResult result)
    {
        result = default;
        if (key != 'd' || cursorPosition <= 0)
        {
            return false;
        }

        var previous = text[cursorPosition - 1];
        if (previous is not ('d' or 'D'))
        {
            return false;
        }

        result = ReplaceRange(text, cursorPosition - 1, 1, (previous == 'D' ? '\u0110' : '\u0111').ToString());
        return true;
    }

    private static bool TryApplyShape(string text, int cursorPosition, char key, out TelexResult result)
    {
        result = default;
        if (cursorPosition <= 0)
        {
            return false;
        }

        var previous = text[cursorPosition - 1];
        if (!TryGetVowel(previous, out var info))
        {
            return false;
        }

        char targetBase = '\0';
        if (key == 'a' && IsBase(info.Base, 'a')) targetBase = info.IsUpper ? '\u00C2' : '\u00E2';
        if (key == 'w' && IsBase(info.Base, 'a')) targetBase = info.IsUpper ? '\u0102' : '\u0103';
        if (key == 'e' && IsBase(info.Base, 'e')) targetBase = info.IsUpper ? '\u00CA' : '\u00EA';
        if (key == 'o' && IsBase(info.Base, 'o')) targetBase = info.IsUpper ? '\u00D4' : '\u00F4';
        if (key == 'w' && IsBase(info.Base, 'o')) targetBase = info.IsUpper ? '\u01A0' : '\u01A1';
        if (key == 'w' && IsBase(info.Base, 'u')) targetBase = info.IsUpper ? '\u01AF' : '\u01B0';

        if (targetBase == '\0')
        {
            return false;
        }

        result = ReplaceRange(text, cursorPosition - 1, 1, WithTone(targetBase, info.Tone).ToString());
        return true;
    }

    private static bool TryApplyTone(string text, int cursorPosition, char key, out TelexResult result)
    {
        result = default;
        var tone = key switch
        {
            's' => 1,
            'f' => 2,
            'r' => 3,
            'x' => 4,
            'j' => 5,
            'z' => 0,
            _ => -1
        };

        if (tone < 0)
        {
            return false;
        }

        var (start, length) = FindCurrentSyllable(text, cursorPosition);
        if (length <= 0)
        {
            return false;
        }

        var syllable = text.Substring(start, length).ToCharArray();
        var vowelIndexes = new List<int>();
        for (var i = 0; i < syllable.Length; i++)
        {
            if (TryGetVowel(syllable[i], out var info))
            {
                syllable[i] = WithTone(info.Base, 0);
                vowelIndexes.Add(i);
            }
        }

        if (vowelIndexes.Count == 0)
        {
            return false;
        }

        if (tone > 0)
        {
            var target = ChooseToneTarget(syllable, vowelIndexes);
            if (TryGetVowel(syllable[target], out var targetInfo))
            {
                syllable[target] = WithTone(targetInfo.Base, tone);
            }
        }

        result = ReplaceRange(text, start, length, new string(syllable)) with { CursorPosition = cursorPosition };
        return true;
    }

    private static (int Start, int Length) FindCurrentSyllable(string text, int cursorPosition)
    {
        var start = Math.Clamp(cursorPosition, 0, text.Length);
        while (start > 0 && IsSyllableChar(text[start - 1]))
        {
            start--;
        }

        return (start, cursorPosition - start);
    }

    private static int ChooseToneTarget(char[] syllable, IReadOnlyList<int> vowelIndexes)
    {
        if (vowelIndexes.Count == 1)
        {
            return vowelIndexes[0];
        }

        for (var i = vowelIndexes.Count - 1; i >= 0; i--)
        {
            var idx = vowelIndexes[i];
            if (TryGetVowel(syllable[idx], out var info) && IsMarkedVowelBase(info.Base))
            {
                return idx;
            }
        }

        if (vowelIndexes.Count == 2)
        {
            var first = syllable[vowelIndexes[0]];
            var second = syllable[vowelIndexes[1]];
            if (IsBase(first, 'u') && IsBase(second, 'y'))
            {
                return vowelIndexes[1];
            }

            if (IsBase(second, 'i') || IsBase(second, 'u') || IsBase(second, 'y'))
            {
                return vowelIndexes[0];
            }
        }

        return vowelIndexes[^1];
    }

    private static bool IsSyllableChar(char value)
        => char.IsLetter(value) || TryGetVowel(value, out _);

    private static bool IsMarkedVowelBase(char value)
        => value is '\u0103' or '\u00E2' or '\u00EA' or '\u00F4' or '\u01A1' or '\u01B0'
            or '\u0102' or '\u00C2' or '\u00CA' or '\u00D4' or '\u01A0' or '\u01AF';

    private static bool IsBase(char value, char lowerBase)
        => char.ToLowerInvariant(RemoveTone(value)) == lowerBase;

    private static char RemoveTone(char value)
        => TryGetVowel(value, out var info) ? info.Base : value;

    private static bool TryGetVowel(char value, out VowelInfo info)
    {
        for (var groupIndex = 0; groupIndex < LowerGroups.Length; groupIndex++)
        {
            var tone = LowerGroups[groupIndex].IndexOf(value);
            if (tone >= 0)
            {
                info = new VowelInfo(LowerGroups[groupIndex][0], tone, false);
                return true;
            }

            tone = UpperGroups[groupIndex].IndexOf(value);
            if (tone >= 0)
            {
                info = new VowelInfo(UpperGroups[groupIndex][0], tone, true);
                return true;
            }
        }

        info = default;
        return false;
    }

    private static char WithTone(char baseChar, int tone)
    {
        var groups = char.IsUpper(baseChar) ? UpperGroups : LowerGroups;
        foreach (var group in groups)
        {
            if (group[0] == baseChar)
            {
                return group[Math.Clamp(tone, 0, 5)];
            }
        }

        return baseChar;
    }

    private static TelexResult InsertRaw(string text, int cursorPosition, string input)
        => ReplaceRange(text, cursorPosition, 0, input);

    private static TelexResult ReplaceRange(string text, int start, int length, string replacement)
    {
        var newText = text[..start] + replacement + text[(start + length)..];
        return new TelexResult(newText.Normalize(NormalizationForm.FormC), start + replacement.Length);
    }

    private readonly record struct VowelInfo(char Base, int Tone, bool IsUpper);
}

internal readonly record struct TelexResult(string Text, int CursorPosition);
