using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// An enrichment file in the format written by the Lens subtitle pipeline (luo-xiaohei-subs).
/// </summary>
internal sealed class LensSidecar
{
    [JsonPropertyName("resolution")]
    public LensResolution? Resolution { get; set; }

    [JsonPropertyName("crop_y")]
    public double? CropY { get; set; }

    [JsonPropertyName("subtitles")]
    public List<LensSubtitle> Subtitles { get; set; } = [];
}
