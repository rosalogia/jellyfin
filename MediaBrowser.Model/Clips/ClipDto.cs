using System;
using Jellyfin.Database.Implementations.Enums;

namespace MediaBrowser.Model.Clips;

/// <summary>
/// A clip data transfer object.
/// </summary>
public class ClipDto
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the source item identifier.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the cached item name.
    /// </summary>
    public string? ItemName { get; set; }

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
    /// Gets or sets the subtitle stream index used for burn-in, or null if none.
    /// </summary>
    public int? SubtitleStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the audio stream index used, or null for default.
    /// </summary>
    public int? AudioStreamIndex { get; set; }

    /// <summary>
    /// Gets or sets the processing status.
    /// </summary>
    public ClipStatus Status { get; set; }
}
