using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Clips;

namespace MediaBrowser.Controller.Clips;

/// <summary>
/// Defines methods for managing video clips.
/// </summary>
public interface IClipManager
{
    /// <summary>
    /// Creates a new clip from a video item.
    /// </summary>
    /// <param name="userId">The user requesting the clip.</param>
    /// <param name="itemId">The source item id.</param>
    /// <param name="startTicks">The start time in ticks.</param>
    /// <param name="endTicks">The end time in ticks.</param>
    /// <param name="subtitleStreamIndex">The subtitle stream index to burn in, or null for default.</param>
    /// <returns>The created clip DTO.</returns>
    Task<ClipDto> CreateClipAsync(Guid userId, Guid itemId, long startTicks, long endTicks, int? subtitleStreamIndex = null);

    /// <summary>
    /// Gets all clips for a user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>A list of clip DTOs.</returns>
    Task<IReadOnlyList<ClipDto>> GetClipsAsync(Guid userId);

    /// <summary>
    /// Gets a specific clip.
    /// </summary>
    /// <param name="clipId">The clip id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The clip DTO, or null if not found.</returns>
    Task<ClipDto?> GetClipAsync(Guid clipId, Guid userId);

    /// <summary>
    /// Deletes a clip.
    /// </summary>
    /// <param name="clipId">The clip id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteClipAsync(Guid clipId, Guid userId);

    /// <summary>
    /// Gets the file path for a clip.
    /// </summary>
    /// <param name="clipId">The clip id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The file path, or null if not found or not ready.</returns>
    Task<string?> GetClipFilePathAsync(Guid clipId, Guid userId);

    /// <summary>
    /// Deletes clips older than 30 days.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteExpiredClipsAsync(CancellationToken cancellationToken);
}
