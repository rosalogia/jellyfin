using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// One CC-CEDICT entry.
/// </summary>
/// <param name="Traditional">The traditional form.</param>
/// <param name="Simplified">The simplified form.</param>
/// <param name="NumberedPinyin">The pinyin with tone numbers, e.g. "zhong1 wen2".</param>
/// <param name="RawDefinitions">The definitions as stored in the file, separated by '/'.</param>
internal sealed record CedictEntry(string Traditional, string Simplified, string NumberedPinyin, string RawDefinitions)
{
    private static readonly string[] _nonMeaningPrefixes =
    [
        "variant of", "old variant of", "archaic variant of", "Japanese variant of", "erhua variant of",
        "used in", "see ", "surname ", "abbr. for", "(Cantonese)", "(Tw)", "(dialect)"
    ];

    /// <summary>
    /// Gets the English definitions.
    /// </summary>
    public IReadOnlyList<string> Definitions => RawDefinitions.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Gets a value indicating whether the reading is a proper noun (CEDICT capitalizes those).
    /// </summary>
    public bool IsProperNoun => NumberedPinyin.Length > 0 && char.IsUpper(NumberedPinyin[0]);

    /// <summary>
    /// Gets a value indicating whether every definition is marked as slang (e.g. 我去 "oh my god!"). Such entries
    /// are poor segmentation candidates, since the same characters are usually just their literal parts.
    /// </summary>
    public bool IsSlangOnly => Definitions.All(d => d.StartsWith("(slang)", StringComparison.Ordinal) || d.StartsWith("(Internet slang)", StringComparison.Ordinal));

    /// <summary>
    /// Gets the number of definitions that carry an actual meaning (not "variant of", "used in", "surname" etc.).
    /// </summary>
    public int MeaningfulDefinitionCount => Definitions.Count(d => !_nonMeaningPrefixes.Any(p => d.StartsWith(p, StringComparison.Ordinal)));
}
