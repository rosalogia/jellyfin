using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.StudySubtitles;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Turns subtitle events and enrichment files into study subtitle lines. Free of I/O so it can be unit tested.
/// </summary>
internal static partial class StudySubtitleBuilder
{
    /// <summary>
    /// Builds lines from a Chinese track and an optional English track. Lines of Latin text inside the Chinese
    /// track (common in bilingual fansubs) are used as the translation when there is no English track.
    /// </summary>
    /// <param name="chineseEvents">The Chinese track events.</param>
    /// <param name="englishEvents">The English track events, if any.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The lines, ordered by start time.</returns>
    public static List<StudyLineDto> FromTracks(
        IEnumerable<SubtitleTrackEvent> chineseEvents,
        IEnumerable<SubtitleTrackEvent>? englishEvents,
        ChineseDictionary dictionary)
    {
        var english = (englishEvents ?? [])
            .Select(e => (e.StartPositionTicks, e.EndPositionTicks, Text: string.Join(' ', CleanText(e.Text).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))))
            .Where(e => e.Text.Length > 0)
            .OrderBy(e => e.StartPositionTicks)
            .ToList();

        var lines = new List<StudyLineDto>();
        var seen = new HashSet<(long Start, long End, string Text)>();
        foreach (var trackEvent in chineseEvents.OrderBy(e => e.StartPositionTicks))
        {
            var parts = CleanText(trackEvent.Text).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var chinese = string.Join(' ', parts.Where(ChineseSegmenter.ContainsHan));
            if (chinese.Length == 0 || !seen.Add((trackEvent.StartPositionTicks, trackEvent.EndPositionTicks, chinese)))
            {
                continue;
            }

            var inlineTranslation = string.Join(' ', parts.Where(p => !ChineseSegmenter.ContainsHan(p) && p.Any(char.IsLetter)));
            var translation = MatchTranslation(trackEvent.StartPositionTicks, trackEvent.EndPositionTicks, english);
            lines.Add(new StudyLineDto
            {
                StartPositionTicks = trackEvent.StartPositionTicks,
                EndPositionTicks = trackEvent.EndPositionTicks,
                Text = chinese,
                Translation = translation ?? (inlineTranslation.Length > 0 ? inlineTranslation : null),
                Words = BuildWords(chinese, dictionary)
            });
        }

        return lines;
    }

    /// <summary>
    /// Builds lines from an enrichment file. Its pinyin and meanings are kept; anything missing is filled in
    /// from the dictionary.
    /// </summary>
    /// <param name="sidecar">The enrichment file.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The lines, ordered by start time.</returns>
    public static List<StudyLineDto> FromSidecar(LensSidecar sidecar, ChineseDictionary dictionary)
    {
        var lines = new List<StudyLineDto>();
        foreach (var subtitle in sidecar.Subtitles)
        {
            if (!TryParseTimestamp(subtitle.Start, out var start) || !TryParseTimestamp(subtitle.End, out var end))
            {
                continue;
            }

            var text = subtitle.Text.Trim();
            var sidecarWords = subtitle.Words?.Where(w => w.Word.Length > 0).ToList();
            var words = sidecarWords is { Count: > 0 } && string.Concat(sidecarWords.Select(w => w.Word)) == text
                ? BuildWordsFromSidecar(sidecarWords, dictionary)
                : BuildWords(text, dictionary);

            lines.Add(new StudyLineDto
            {
                StartPositionTicks = start,
                EndPositionTicks = end,
                Text = text,
                Translation = NullIfEmpty(subtitle.Translation),
                Notes = NullIfEmpty(subtitle.GrammarNotes),
                Words = words
            });
        }

        lines.Sort((a, b) => a.StartPositionTicks.CompareTo(b.StartPositionTicks));
        return lines;
    }

    /// <summary>
    /// Segments a line and attaches pinyin and dictionary entries to each word.
    /// </summary>
    /// <param name="text">The Chinese text.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The words.</returns>
    public static List<StudyWordDto> BuildWords(string text, ChineseDictionary dictionary)
    {
        var tokens = ChineseSegmenter.Segment(text, dictionary);
        var words = new List<StudyWordDto>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.IsHan)
            {
                words.Add(new StudyWordDto { Text = token.Text });
                continue;
            }

            var entries = ReadingSelector.Order(
                token.Text,
                dictionary.Lookup(token.Text),
                i > 0 ? tokens[i - 1] : null,
                i + 1 < tokens.Count ? tokens[i + 1] : null);
            words.Add(new StudyWordDto
            {
                Text = token.Text,
                IsWord = true,
                Pinyin = entries.Count > 0 ? Pinyin.ToToneMarks(entries[0].NumberedPinyin) : null,
                Entries = entries.Select(ToDto).ToList()
            });
        }

        return words;
    }

    /// <summary>
    /// Strips ASS override tags and HTML-style tags, and normalizes line breaks to '\n'.
    /// </summary>
    /// <param name="text">The subtitle event text.</param>
    /// <returns>The plain text.</returns>
    public static string CleanText(string text)
    {
        text = AssTagRegex().Replace(text, string.Empty);
        text = HtmlTagRegex().Replace(text, string.Empty);
        return text.Replace("\\N", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\h", " ", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    /// <summary>
    /// Parses "HH:MM:SS.mmm" (or with a comma) into ticks.
    /// </summary>
    /// <param name="value">The timestamp.</param>
    /// <param name="ticks">The ticks.</param>
    /// <returns>Whether parsing succeeded.</returns>
    public static bool TryParseTimestamp(string value, out long ticks)
    {
        if (TimeSpan.TryParse(value.Replace(',', '.'), CultureInfo.InvariantCulture, out var time))
        {
            ticks = time.Ticks;
            return true;
        }

        ticks = 0;
        return false;
    }

    private static List<StudyWordDto> BuildWordsFromSidecar(List<LensWord> sidecarWords, ChineseDictionary dictionary)
    {
        var words = new List<StudyWordDto>(sidecarWords.Count);
        foreach (var sidecarWord in sidecarWords)
        {
            if (!ChineseSegmenter.ContainsHan(sidecarWord.Word))
            {
                words.Add(new StudyWordDto { Text = sidecarWord.Word });
                continue;
            }

            var pinyin = NullIfEmpty(sidecarWord.Pinyin);
            var entries = ReadingSelector.Order(sidecarWord.Word, dictionary.Lookup(sidecarWord.Word), null, null);
            if (pinyin is not null)
            {
                // Put the entry matching the enrichment file's reading first.
                var key = NormalizeForComparison(pinyin);
                entries = entries.OrderByDescending(e => NormalizeForComparison(Pinyin.ToToneMarks(e.NumberedPinyin)) == key).ToList();
            }

            words.Add(new StudyWordDto
            {
                Text = sidecarWord.Word,
                IsWord = true,
                Pinyin = pinyin ?? (entries.Count > 0 ? Pinyin.ToToneMarks(entries[0].NumberedPinyin) : null),
                Gloss = NullIfEmpty(sidecarWord.English),
                Entries = entries.Select(ToDto).ToList()
            });
        }

        return words;
    }

    private static string? MatchTranslation(long start, long end, List<(long Start, long End, string Text)> english)
    {
        var duration = Math.Max(1, end - start);
        var matches = english
            .Where(e =>
            {
                var overlap = Math.Min(end, e.End) - Math.Max(start, e.Start);
                return overlap > 0 && overlap >= 0.5 * Math.Min(duration, Math.Max(1, e.End - e.Start));
            })
            .Select(e => e.Text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return matches.Count > 0 ? string.Join(' ', matches) : null;
    }

    private static DictionaryEntryDto ToDto(CedictEntry entry) => new()
    {
        Traditional = entry.Traditional,
        Simplified = entry.Simplified,
        Pinyin = Pinyin.ToToneMarks(entry.NumberedPinyin),
        Definitions = entry.Definitions
    };

    private static string NormalizeForComparison(string pinyin)
        => new string(pinyin.Where(char.IsLetter).ToArray()).ToLowerInvariant();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\{\\[^}]*\}")]
    private static partial Regex AssTagRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex HtmlTagRegex();
}
