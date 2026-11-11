using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// An in-memory CC-CEDICT dictionary, indexed by both traditional and simplified forms.
/// </summary>
internal sealed class ChineseDictionary
{
    private const string ResourceName = "Jellyfin.Server.Implementations.StudySubtitles.cedict_ts.u8.gz";

    private static readonly Lazy<ChineseDictionary> _embedded = new(LoadEmbedded);

    private readonly Dictionary<string, List<CedictEntry>> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _segmentationWords = new(StringComparer.Ordinal);

    private ChineseDictionary()
    {
    }

    /// <summary>
    /// Gets the dictionary bundled with the server. Loaded on first use.
    /// </summary>
    public static ChineseDictionary Embedded => _embedded.Value;

    /// <summary>
    /// Gets the length of the longest headword, in UTF-16 code units.
    /// </summary>
    public int MaxWordLength { get; private set; }

    /// <summary>
    /// Parses a dictionary in CC-CEDICT format.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The dictionary.</returns>
    public static ChineseDictionary Parse(TextReader reader)
    {
        var dictionary = new ChineseDictionary();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParseLine(line, out var entry))
            {
                dictionary.Add(entry.Traditional, entry);
                if (!string.Equals(entry.Simplified, entry.Traditional, StringComparison.Ordinal))
                {
                    dictionary.Add(entry.Simplified, entry);
                }
            }
        }

        return dictionary;
    }

    /// <summary>
    /// Looks up a word.
    /// </summary>
    /// <param name="word">The word in traditional or simplified characters.</param>
    /// <returns>The matching entries, possibly empty.</returns>
    public IReadOnlyList<CedictEntry> Lookup(string word)
        => _entries.TryGetValue(word, out var list) ? list : Array.Empty<CedictEntry>();

    /// <summary>
    /// Gets a value indicating whether the word should be kept together when segmenting: it is a headword with at
    /// least one entry that is not slang-only.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>Whether the word is a segmentation candidate.</returns>
    public bool IsSegmentationWord(string word) => _segmentationWords.Contains(word);

    private static ChineseDictionary LoadEmbedded()
    {
        using var resource = typeof(ChineseDictionary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return Parse(reader);
    }

    // Format: "Traditional Simplified [pin1 yin1] /definition/definition/"
    private static bool TryParseLine(string line, out CedictEntry entry)
    {
        entry = null!;
        if (line.Length == 0 || line[0] == '#')
        {
            return false;
        }

        var firstSpace = line.IndexOf(' ', StringComparison.Ordinal);
        var secondSpace = firstSpace < 0 ? -1 : line.IndexOf(' ', firstSpace + 1);
        var pinyinStart = line.IndexOf('[', StringComparison.Ordinal);
        var pinyinEnd = line.IndexOf(']', StringComparison.Ordinal);
        var definitionsStart = line.IndexOf('/', StringComparison.Ordinal);
        if (secondSpace < 0 || pinyinStart < secondSpace || pinyinEnd < pinyinStart || definitionsStart < pinyinEnd)
        {
            return false;
        }

        entry = new CedictEntry(
            line[..firstSpace],
            line[(firstSpace + 1)..secondSpace],
            line[(pinyinStart + 1)..pinyinEnd],
            line[definitionsStart..]);
        return true;
    }

    private void Add(string headword, CedictEntry entry)
    {
        if (!_entries.TryGetValue(headword, out var list))
        {
            list = new List<CedictEntry>(1);
            _entries[headword] = list;
            MaxWordLength = Math.Max(MaxWordLength, headword.Length);
        }

        list.Add(entry);
        if (!entry.IsSlangOnly)
        {
            _segmentationWords.Add(headword);
        }
    }
}
