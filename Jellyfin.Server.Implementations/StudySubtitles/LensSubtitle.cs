using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// One line in an enrichment file.
/// </summary>
internal sealed class LensSubtitle
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("translation")]
    public string? Translation { get; set; }

    [JsonPropertyName("grammar_notes")]
    public string? GrammarNotes { get; set; }

    [JsonPropertyName("words")]
    public List<LensWord>? Words { get; set; }
}
