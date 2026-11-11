using System.Text.Json.Serialization;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// One word in an enrichment file.
/// </summary>
internal sealed class LensWord
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("pinyin")]
    public string? Pinyin { get; set; }

    [JsonPropertyName("english")]
    public string? English { get; set; }
}
