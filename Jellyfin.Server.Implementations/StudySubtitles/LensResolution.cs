using System.Text.Json.Serialization;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// The video resolution recorded in an enrichment file.
/// </summary>
internal sealed class LensResolution
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
