using System;
using System.Buffers;
using System.Linq;
using System.Text;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Pinyin helpers.
/// </summary>
internal static class Pinyin
{
    private const string Vowels = "aeiouü";

    private static readonly SearchValues<char> _vowelSearch = SearchValues.Create(Vowels);

    // Index: vowel position in Vowels, then tone 1-4.
    private static readonly string[] _lowerMarks = ["āáǎà", "ēéěè", "īíǐì", "ōóǒò", "ūúǔù", "ǖǘǚǜ"];
    private static readonly string[] _upperMarks = ["ĀÁǍÀ", "ĒÉĚÈ", "ĪÍǏÌ", "ŌÓǑÒ", "ŪÚǓÙ", "ǕǗǙǛ"];

    /// <summary>
    /// Converts space-separated numbered pinyin ("zhong1 wen2") to tone marks ("zhōng wén").
    /// </summary>
    /// <param name="numbered">The numbered pinyin.</param>
    /// <returns>The pinyin with tone marks.</returns>
    public static string ToToneMarks(string numbered)
    {
        return string.Join(' ', numbered.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(SyllableToToneMarks));
    }

    /// <summary>
    /// Converts one numbered syllable ("lu:4") to tone marks ("lǜ").
    /// </summary>
    /// <param name="syllable">The syllable.</param>
    /// <returns>The syllable with a tone mark.</returns>
    public static string SyllableToToneMarks(string syllable)
    {
        var tone = 0;
        if (syllable.Length > 1 && syllable[^1] is >= '1' and <= '5')
        {
            tone = syllable[^1] - '0';
            syllable = syllable[..^1];
        }

        syllable = syllable.Replace("u:", "ü", StringComparison.Ordinal)
            .Replace("U:", "Ü", StringComparison.Ordinal);
        if (syllable.Length >= 2 && syllable[1] is 'v' or 'V' && syllable[0] is 'l' or 'L' or 'n' or 'N')
        {
            // A few sources write ü as "v" (lv4 = lǜ).
            syllable = string.Concat(syllable.AsSpan(0, 1), char.IsUpper(syllable[1]) ? "Ü" : "ü", syllable.AsSpan(2));
        }

        if (tone is 0 or 5)
        {
            return syllable;
        }

        var lower = syllable.ToLowerInvariant();
        int index;
        if ((index = lower.IndexOf('a', StringComparison.Ordinal)) < 0
            && (index = lower.IndexOf('e', StringComparison.Ordinal)) < 0
            && (index = lower.IndexOf("ou", StringComparison.Ordinal)) < 0)
        {
            index = lower.AsSpan().LastIndexOfAny(_vowelSearch);
        }

        if (index < 0)
        {
            // e.g. "m2", "ng4", "r5": no vowel to mark.
            return syllable;
        }

        var vowel = Vowels.IndexOf(lower[index], StringComparison.Ordinal);
        var marks = char.IsUpper(syllable[index]) ? _upperMarks : _lowerMarks;
        var builder = new StringBuilder(syllable);
        builder[index] = marks[vowel][tone - 1];
        return builder.ToString();
    }
}
