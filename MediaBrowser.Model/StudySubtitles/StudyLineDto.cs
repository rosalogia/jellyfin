using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.StudySubtitles;

/// <summary>
/// One subtitle line.
/// </summary>
public class StudyLineDto
{
    /// <summary>
    /// Gets or sets the start position in ticks.
    /// </summary>
    public long StartPositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the end position in ticks.
    /// </summary>
    public long EndPositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the Chinese text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the English translation of the whole line, if available.
    /// </summary>
    public string? Translation { get; set; }

    /// <summary>
    /// Gets or sets grammar or usage notes, if available.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the line split into words and punctuation, in order. Concatenated they form <see cref="Text"/>.
    /// </summary>
    public IReadOnlyList<StudyWordDto> Words { get; set; } = Array.Empty<StudyWordDto>();
}
