using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using MediaBrowser.Controller.Clips;
using MediaBrowser.Model.Clips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Clips controller.
/// </summary>
[Route("Clips")]
[Authorize]
public class ClipsController : BaseJellyfinApiController
{
    private readonly IClipManager _clipManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipsController"/> class.
    /// </summary>
    /// <param name="clipManager">Instance of <see cref="IClipManager"/> interface.</param>
    public ClipsController(IClipManager clipManager)
    {
        _clipManager = clipManager;
    }

    /// <summary>
    /// Creates a new clip.
    /// </summary>
    /// <param name="itemId">The source item id.</param>
    /// <param name="startTicks">The start time in ticks.</param>
    /// <param name="endTicks">The end time in ticks.</param>
    /// <param name="subtitleStreamIndex">The subtitle stream index to burn in.</param>
    /// <response code="201">Clip created.</response>
    /// <returns>The created clip.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClipDto>> CreateClip(
        [FromQuery, Required] Guid itemId,
        [FromQuery, Required] long startTicks,
        [FromQuery, Required] long endTicks,
        [FromQuery] int? subtitleStreamIndex = null)
    {
        var userId = User.GetUserId();
        var clip = await _clipManager.CreateClipAsync(userId, itemId, startTicks, endTicks, subtitleStreamIndex)
            .ConfigureAwait(false);

        return Created($"/Clips/{clip.Id}", clip);
    }

    /// <summary>
    /// Gets the current user's clips.
    /// </summary>
    /// <response code="200">Clips returned.</response>
    /// <returns>A list of clips.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClipDto>>> GetClips()
    {
        var userId = User.GetUserId();
        var clips = await _clipManager.GetClipsAsync(userId).ConfigureAwait(false);
        return Ok(clips);
    }

    /// <summary>
    /// Downloads a clip.
    /// </summary>
    /// <param name="id">The clip id.</param>
    /// <response code="200">Clip file returned.</response>
    /// <response code="404">Clip not found or not ready.</response>
    /// <returns>The clip file.</returns>
    [HttpGet("{id}/Download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadClip([FromRoute, Required] Guid id)
    {
        var userId = User.GetUserId();
        var filePath = await _clipManager.GetClipFilePathAsync(id, userId).ConfigureAwait(false);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var clip = await _clipManager.GetClipAsync(id, userId).ConfigureAwait(false);
        var itemName = clip?.ItemName ?? "clip";
        var start = FormatTicks(clip?.StartTicks ?? 0);
        var end = FormatTicks(clip?.EndTicks ?? 0);
        var fileName = $"{SanitizeFileName(itemName)} - Clip {start}-{end}.mp4";

        return PhysicalFile(filePath, "video/mp4", fileName);
    }

    /// <summary>
    /// Streams a clip for inline playback.
    /// </summary>
    /// <param name="id">The clip id.</param>
    /// <response code="200">Clip file returned.</response>
    /// <response code="404">Clip not found or not ready.</response>
    /// <returns>The clip file stream.</returns>
    [HttpGet("{id}/Stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> StreamClip([FromRoute, Required] Guid id)
    {
        var userId = User.GetUserId();
        var filePath = await _clipManager.GetClipFilePathAsync(id, userId).ConfigureAwait(false);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        return PhysicalFile(filePath, "video/mp4", enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes a clip.
    /// </summary>
    /// <param name="id">The clip id.</param>
    /// <response code="204">Clip deleted.</response>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteClip([FromRoute, Required] Guid id)
    {
        var userId = User.GetUserId();
        await _clipManager.DeleteClipAsync(id, userId).ConfigureAwait(false);
        return NoContent();
    }

    private static string FormatTicks(long ticks)
    {
        var ts = TimeSpan.FromTicks(ticks);
        return ts.Hours > 0
            ? $"{ts.Hours}h{ts.Minutes:D2}m{ts.Seconds:D2}s"
            : $"{ts.Minutes}m{ts.Seconds:D2}s";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
    }
}
