using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.StudySubtitles;

/// <summary>
/// Chinese study subtitles for an item: Chinese lines split into words, with pinyin and English.
/// </summary>
public class StudySubtitlesDto
{
    /// <summary>
    /// Gets or sets where the data came from.
    /// </summary>
    public StudySubtitlesSource Source { get; set; }

    /// <summary>
    /// Gets or sets the Chinese subtitle stream index used, if generated from a track.
    /// </summary>
    public int? ChineseStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the English subtitle stream index used, if any.
    /// </summary>
    public int? EnglishStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the top of the burned-in subtitle area as a fraction (0-1) of the video height, if known.
    /// Clients can cover the video below this line so burned-in subtitles do not show twice.
    /// </summary>
    public double? BurnedInSubtitleTop { get; set; }

    /// <summary>
    /// Gets or sets the lines, ordered by start time.
    /// </summary>
    public IReadOnlyList<StudyLineDto> Lines { get; set; } = Array.Empty<StudyLineDto>();
}
