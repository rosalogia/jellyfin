using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.StudySubtitles;

namespace MediaBrowser.Controller.StudySubtitles;

/// <summary>
/// Builds Chinese study subtitles (words, pinyin and English) for media items.
/// </summary>
public interface IStudySubtitleManager
{
    /// <summary>
    /// Gets study subtitles for an item. An enrichment file next to the media is preferred; otherwise
    /// they are generated from the item's Chinese (and, if present, English) text subtitle tracks.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="mediaSourceId">The media source id, or null for the default source.</param>
    /// <param name="chineseStreamIndex">The Chinese subtitle stream to use, or null to pick one automatically.</param>
    /// <param name="englishStreamIndex">The English subtitle stream to use, or null to pick one automatically. -1 disables English.</param>
    /// <param name="useSidecar">Whether an enrichment file may be used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The study subtitles, or null if the item has neither an enrichment file nor a Chinese text subtitle track.</returns>
    Task<StudySubtitlesDto?> GetStudySubtitlesAsync(
        BaseItem item,
        string? mediaSourceId,
        int? chineseStreamIndex,
        int? englishStreamIndex,
        bool useSidecar,
        CancellationToken cancellationToken);
}
