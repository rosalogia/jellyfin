using System;
using System.Collections.Generic;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Splits Chinese text into dictionary words.
/// </summary>
internal static class ChineseSegmenter
{
    /// <summary>
    /// Gets a value indicating whether a character is a CJK ideograph.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is a Han character.</returns>
    public static bool IsHan(char c)
        => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿') or '〇';

    /// <summary>
    /// Gets a value indicating whether the text contains any Han characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether it contains Han characters.</returns>
    public static bool ContainsHan(string text)
    {
        foreach (var c in text)
        {
            if (IsHan(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits text into tokens: Chinese words, and runs of anything else (punctuation, spaces, Latin text).
    /// Concatenating the tokens gives back the original text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The tokens in order.</returns>
    public static IReadOnlyList<SegmentToken> Segment(string text, ChineseDictionary dictionary)
    {
        var tokens = new List<SegmentToken>();
        var runStart = 0;
        while (runStart < text.Length)
        {
            var isHan = IsHan(text[runStart]);
            var runEnd = runStart + 1;
            while (runEnd < text.Length && IsHan(text[runEnd]) == isHan)
            {
                runEnd++;
            }

            var run = text[runStart..runEnd];
            if (isHan)
            {
                foreach (var word in SegmentHanRun(run, dictionary))
                {
                    tokens.Add(new SegmentToken(word, true));
                }
            }
            else
            {
                tokens.Add(new SegmentToken(run, false));
            }

            runStart = runEnd;
        }

        return tokens;
    }

    /// <summary>
    /// Segments a run of Han characters. Picks the split with the fewest words; among those, the one whose word
    /// lengths are most even (smallest sum of squared lengths), which resolves most overlapping-word ambiguities
    /// (研究/生命 rather than 研究生/命).
    /// </summary>
    private static List<string> SegmentHanRun(string run, ChineseDictionary dictionary)
    {
        var n = run.Length;
        var wordCount = new int[n + 1];
        var squareSum = new int[n + 1];
        var previous = new int[n + 1];
        Array.Fill(wordCount, int.MaxValue);
        wordCount[0] = 0;

        var maxLength = Math.Max(1, dictionary.MaxWordLength);
        for (var start = 0; start < n; start++)
        {
            if (wordCount[start] == int.MaxValue)
            {
                continue;
            }

            for (var length = 1; length <= maxLength && start + length <= n; length++)
            {
                // Single characters are always allowed, so unknown characters still segment.
                if (length > 1 && !dictionary.IsSegmentationWord(run.Substring(start, length)))
                {
                    continue;
                }

                var end = start + length;
                var count = wordCount[start] + 1;
                var squares = squareSum[start] + (length * length);
                if (count < wordCount[end] || (count == wordCount[end] && squares < squareSum[end]))
                {
                    wordCount[end] = count;
                    squareSum[end] = squares;
                    previous[end] = start;
                }
            }
        }

        var words = new List<string>(wordCount[n]);
        for (var end = n; end > 0; end = previous[end])
        {
            words.Add(run[previous[end]..end]);
        }

        words.Reverse();
        return words;
    }
}
