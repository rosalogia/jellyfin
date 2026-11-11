using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.StudySubtitles;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.StudySubtitles;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.StudySubtitles;

/// <summary>
/// Builds Chinese study subtitles from enrichment files or from subtitle tracks plus CC-CEDICT.
/// </summary>
public class StudySubtitleManager : IStudySubtitleManager
{
    /// <summary>
    /// The suffix of enrichment files, which sit next to the media file: "Episode 1.mkv" → "Episode 1.study.json".
    /// </summary>
    public const string SidecarSuffix = ".study.json";

    private const int MaxCachedItems = 32;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<StudySubtitleManager> _logger;
    private readonly ConcurrentDictionary<string, StudySubtitlesDto> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="StudySubtitleManager"/> class.
    /// </summary>
    /// <param name="subtitleEncoder">The subtitle encoder.</param>
    /// <param name="mediaSourceManager">The media source manager.</param>
    /// <param name="logger">The logger.</param>
    public StudySubtitleManager(ISubtitleEncoder subtitleEncoder, IMediaSourceManager mediaSourceManager, ILogger<StudySubtitleManager> logger)
    {
        _subtitleEncoder = subtitleEncoder;
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StudySubtitlesDto?> GetStudySubtitlesAsync(
        BaseItem item,
        string? mediaSourceId,
        int? chineseStreamIndex,
        int? englishStreamIndex,
        bool useSidecar,
        CancellationToken cancellationToken)
    {
        if (useSidecar && !string.IsNullOrEmpty(item.Path))
        {
            var sidecarPath = Path.Combine(Path.GetDirectoryName(item.Path) ?? string.Empty, Path.GetFileNameWithoutExtension(item.Path) + SidecarSuffix);
            if (File.Exists(sidecarPath))
            {
                var sidecarResult = await GetFromSidecarAsync(sidecarPath, cancellationToken).ConfigureAwait(false);
                if (sidecarResult is not null)
                {
                    return sidecarResult;
                }
            }
        }

        var mediaSources = _mediaSourceManager.GetStaticMediaSources(item, false);
        var mediaSource = mediaSources.FirstOrDefault(m => string.Equals(m.Id, mediaSourceId, StringComparison.OrdinalIgnoreCase))
            ?? (mediaSources.Count > 0 ? mediaSources[0] : null);
        if (mediaSource is null)
        {
            return null;
        }

        var streams = mediaSource.MediaStreams;
        var chinese = chineseStreamIndex is null
            ? SubtitleStreamSelector.SelectChinese(streams)
            : streams.FirstOrDefault(s => s.Index == chineseStreamIndex && s.IsTextSubtitleStream);
        if (chinese is null)
        {
            return null;
        }

        var english = englishStreamIndex switch
        {
            null => SubtitleStreamSelector.SelectEnglish(streams),
            < 0 => null,
            _ => streams.FirstOrDefault(s => s.Index == englishStreamIndex && s.IsTextSubtitleStream)
        };

        var cacheKey = FormattableString.Invariant($"{item.Id:N}|{mediaSource.Id}|{chinese.Index}|{english?.Index}|{item.DateModified.Ticks}");
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var chineseEvents = await GetEventsAsync(item, mediaSource.Id, chinese.Index, cancellationToken).ConfigureAwait(false);
        SubtitleTrackInfo? englishTrack = null;
        if (english is not null)
        {
            try
            {
                englishTrack = await GetEventsAsync(item, mediaSource.Id, english.Index, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not read English subtitles {Index} for {Item}; continuing without them", english.Index, item.Id);
            }
        }

        var result = new StudySubtitlesDto
        {
            Source = StudySubtitlesSource.Generated,
            ChineseStreamIndex = chinese.Index,
            EnglishStreamIndex = englishTrack is null ? null : english?.Index,
            Lines = StudySubtitleBuilder.FromTracks(chineseEvents.TrackEvents, englishTrack?.TrackEvents, ChineseDictionary.Embedded)
        };
        AddToCache(cacheKey, result);
        return result;
    }

    private async Task<StudySubtitlesDto?> GetFromSidecarAsync(string path, CancellationToken cancellationToken)
    {
        var cacheKey = FormattableString.Invariant($"sidecar|{path}|{File.GetLastWriteTimeUtc(path).Ticks}");
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        LensSidecar? sidecar;
        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                sidecar = await JsonSerializer.DeserializeAsync<LensSidecar>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Ignoring unreadable study subtitle file {Path}", path);
            return null;
        }

        if (sidecar is null || sidecar.Subtitles.Count == 0)
        {
            return null;
        }

        var result = new StudySubtitlesDto
        {
            Source = StudySubtitlesSource.Sidecar,
            BurnedInSubtitleTop = sidecar is { CropY: > 0, Resolution.Height: > 0 } ? sidecar.CropY / sidecar.Resolution.Height : null,
            Lines = StudySubtitleBuilder.FromSidecar(sidecar, ChineseDictionary.Embedded)
        };
        AddToCache(cacheKey, result);
        return result;
    }

    private async Task<SubtitleTrackInfo> GetEventsAsync(BaseItem item, string mediaSourceId, int streamIndex, CancellationToken cancellationToken)
    {
        var stream = await _subtitleEncoder.GetSubtitles(item, mediaSourceId, streamIndex, "json", 0, 0, false, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<SubtitleTrackInfo>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false)
                ?? new SubtitleTrackInfo();
        }
    }

    private void AddToCache(string key, StudySubtitlesDto value)
    {
        if (_cache.Count >= MaxCachedItems)
        {
            _cache.Clear();
        }

        _cache[key] = value;
    }
}
