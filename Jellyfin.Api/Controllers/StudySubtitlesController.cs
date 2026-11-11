using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.StudySubtitles;
using MediaBrowser.Model.StudySubtitles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Chinese study subtitles: lines split into words with pinyin and English.
/// </summary>
[Route("Videos")]
[Authorize]
public class StudySubtitlesController : BaseJellyfinApiController
{
    private readonly IStudySubtitleManager _studySubtitleManager;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="StudySubtitlesController"/> class.
    /// </summary>
    /// <param name="studySubtitleManager">Instance of the <see cref="IStudySubtitleManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public StudySubtitlesController(IStudySubtitleManager studySubtitleManager, ILibraryManager libraryManager)
    {
        _studySubtitleManager = studySubtitleManager;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets study subtitles for a video. Uses an enrichment file ("{video name}.study.json") next to the video if
    /// there is one, otherwise builds them from the video's Chinese and English text subtitle tracks.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="mediaSourceId">Optional. The media source id.</param>
    /// <param name="chineseStreamIndex">Optional. The Chinese subtitle stream index; picked automatically if omitted.</param>
    /// <param name="englishStreamIndex">Optional. The English subtitle stream index; picked automatically if omitted, -1 for none.</param>
    /// <param name="useSidecar">Optional. Whether an enrichment file may be used. Defaults to true.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">Study subtitles returned.</response>
    /// <response code="404">Item not found, or it has no enrichment file and no Chinese text subtitles.</response>
    /// <returns>The study subtitles.</returns>
    [HttpGet("{itemId}/StudySubtitles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudySubtitlesDto>> GetStudySubtitles(
        [FromRoute, Required] Guid itemId,
        [FromQuery] string? mediaSourceId,
        [FromQuery] int? chineseStreamIndex,
        [FromQuery] int? englishStreamIndex,
        [FromQuery] bool useSidecar = true,
        CancellationToken cancellationToken = default)
    {
        var item = _libraryManager.GetItemById<BaseItem>(itemId, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        var result = await _studySubtitleManager.GetStudySubtitlesAsync(item, mediaSourceId, chineseStreamIndex, englishStreamIndex, useSidecar, cancellationToken)
            .ConfigureAwait(false);
        return result is null ? NotFound() : result;
    }
}
