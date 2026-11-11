using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.StudySubtitles;

/// <summary>
/// A dictionary entry for a Chinese word (from CC-CEDICT).
/// </summary>
public class DictionaryEntryDto
{
    /// <summary>
    /// Gets or sets the traditional form.
    /// </summary>
    public string Traditional { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the simplified form.
    /// </summary>
    public string Simplified { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pinyin with tone marks.
    /// </summary>
    public string Pinyin { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the English definitions.
    /// </summary>
    public IReadOnlyList<string> Definitions { get; set; } = Array.Empty<string>();
}
