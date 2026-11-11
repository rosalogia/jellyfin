using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.StudySubtitles;

/// <summary>
/// One word (or run of punctuation / non-Chinese text) in a line.
/// </summary>
public class StudyWordDto
{
    /// <summary>
    /// Gets or sets the text of the word as it appears in the line.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is a Chinese word (as opposed to punctuation or other text).
    /// </summary>
    public bool IsWord { get; set; }

    /// <summary>
    /// Gets or sets the pinyin with tone marks, one space-separated syllable per character where possible.
    /// </summary>
    public string? Pinyin { get; set; }

    /// <summary>
    /// Gets or sets a short English meaning for this word in context, if available (from an enrichment file).
    /// </summary>
    public string? Gloss { get; set; }

    /// <summary>
    /// Gets or sets the dictionary entries for this word, the reading used for <see cref="Pinyin"/> first.
    /// </summary>
    public IReadOnlyList<DictionaryEntryDto> Entries { get; set; } = Array.Empty<DictionaryEntryDto>();
}
