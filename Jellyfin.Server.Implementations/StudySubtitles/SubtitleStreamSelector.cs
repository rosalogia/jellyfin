using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Picks the Chinese and English text subtitle tracks of a media source.
/// </summary>
internal static class SubtitleStreamSelector
{
    private static readonly string[] _chineseLanguages = ["chi", "zho", "zh", "cmn", "chs", "cht", "chinese"];
    private static readonly string[] _chineseTitleHints = ["中文", "简", "繁", "chinese", "mandarin", "chs", "cht"];
    private static readonly string[] _simplifiedHints = ["简", "simplified", "chs", "hans", "zh-cn", "zh-sg"];
    private static readonly string[] _englishLanguages = ["eng", "en", "english"];

    /// <summary>
    /// Picks the most suitable Chinese text subtitle stream.
    /// </summary>
    /// <param name="streams">The media streams.</param>
    /// <returns>The stream, or null if there is none.</returns>
    public static MediaStream? SelectChinese(IEnumerable<MediaStream> streams)
        => TextSubtitles(streams)
            .Where(IsChinese)
            .OrderByDescending(s => (Contains(s, _simplifiedHints) ? 4 : 0) + (s.IsDefault ? 1 : 0) - (s.IsForced ? 10 : 0))
            .FirstOrDefault();

    /// <summary>
    /// Picks the most suitable English text subtitle stream.
    /// </summary>
    /// <param name="streams">The media streams.</param>
    /// <returns>The stream, or null if there is none.</returns>
    public static MediaStream? SelectEnglish(IEnumerable<MediaStream> streams)
        => TextSubtitles(streams)
            .Where(s => !IsChinese(s) && (LanguageMatches(s, _englishLanguages) || Contains(s, ["english"])))
            .OrderByDescending(s => (s.IsDefault ? 1 : 0) - (s.IsForced ? 10 : 0) - (s.IsHearingImpaired ? 2 : 0))
            .FirstOrDefault();

    private static IEnumerable<MediaStream> TextSubtitles(IEnumerable<MediaStream> streams)
        => streams.Where(s => s.Type == MediaStreamType.Subtitle && s.IsTextSubtitleStream);

    private static bool IsChinese(MediaStream stream)
        => LanguageMatches(stream, _chineseLanguages) || Contains(stream, _chineseTitleHints);

    private static bool LanguageMatches(MediaStream stream, string[] languages)
    {
        var language = stream.Language;
        if (string.IsNullOrEmpty(language))
        {
            return false;
        }

        return languages.Any(l => string.Equals(language, l, StringComparison.OrdinalIgnoreCase)
            || language.StartsWith(l + "-", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Contains(MediaStream stream, string[] hints)
    {
        var text = string.Join(' ', stream.Title, stream.Language, stream.Path is null ? null : System.IO.Path.GetFileName(stream.Path));
        return hints.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));
    }
}
