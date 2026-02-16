namespace Jellyfin.Database.Implementations.Enums;

/// <summary>
/// Defines the processing status of a clip.
/// </summary>
public enum ClipStatus
{
    /// <summary>
    /// Clip is pending processing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Clip is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Clip is ready for download.
    /// </summary>
    Ready = 2,

    /// <summary>
    /// Clip processing failed.
    /// </summary>
    Failed = 3
}
