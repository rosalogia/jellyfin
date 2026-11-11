namespace MediaBrowser.Model.StudySubtitles;

/// <summary>
/// Where study subtitle data came from.
/// </summary>
public enum StudySubtitlesSource
{
    /// <summary>
    /// Pre-generated enrichment file stored next to the media (e.g. produced with Claude).
    /// </summary>
    Sidecar = 0,

    /// <summary>
    /// Generated on the fly from the item's text subtitle tracks and the bundled dictionary.
    /// </summary>
    Generated = 1
}
