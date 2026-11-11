namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// A piece of segmented text.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="IsHan">Whether this is a Chinese word (otherwise punctuation, spaces or other text).</param>
internal sealed record SegmentToken(string Text, bool IsHan);
