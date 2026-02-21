using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Database.Implementations.Interfaces;

namespace Jellyfin.Database.Implementations.Entities;

/// <summary>
/// An entity representing a user-created video clip.
/// </summary>
public class Clip : IHasConcurrencyToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Clip"/> class.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="itemId">The source item id.</param>
    /// <param name="startTicks">The start time in ticks.</param>
    /// <param name="endTicks">The end time in ticks.</param>
    public Clip(Guid userId, Guid itemId, long startTicks, long endTicks)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ItemId = itemId;
        StartTicks = startTicks;
        EndTicks = endTicks;
        CreatedAt = DateTime.UtcNow;
        Status = ClipStatus.Pending;
    }

    /// <summary>
    /// Gets the clip identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the source item identifier.
    /// </summary>
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Gets or sets the start time in ticks.
    /// </summary>
    public long StartTicks { get; set; }

    /// <summary>
    /// Gets or sets the end time in ticks.
    /// </summary>
    public long EndTicks { get; set; }

    /// <summary>
    /// Gets or sets the date created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the file path of the generated clip.
    /// </summary>
    [MaxLength(1024)]
    [StringLength(1024)]
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the processing status of the clip.
    /// </summary>
    public ClipStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the subtitle stream index to burn in, or null for default/none.
    /// </summary>
    public int? SubtitleStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the audio stream index to use, or null for default.
    /// </summary>
    public int? AudioStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the cached item name for display.
    /// </summary>
    [MaxLength(512)]
    [StringLength(512)]
    public string? ItemName { get; set; }

    /// <inheritdoc />
    [ConcurrencyCheck]
    public uint RowVersion { get; private set; }

    /// <inheritdoc />
    public void OnSavingChanges()
    {
        RowVersion++;
    }
}
