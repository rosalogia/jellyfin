using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Clips;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Clips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Clips;

/// <summary>
/// Manages video clip creation, storage, and retrieval.
/// </summary>
public class ClipManager : IClipManager
{
    private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;
    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<ClipManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipManager"/> class.
    /// </summary>
    /// <param name="dbProvider">The database context factory.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="appPaths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public ClipManager(
        IDbContextFactory<JellyfinDbContext> dbProvider,
        IMediaEncoder mediaEncoder,
        ILibraryManager libraryManager,
        IApplicationPaths appPaths,
        ILogger<ClipManager> logger)
    {
        _dbProvider = dbProvider;
        _mediaEncoder = mediaEncoder;
        _libraryManager = libraryManager;
        _appPaths = appPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClipDto> CreateClipAsync(Guid userId, Guid itemId, long startTicks, long endTicks, int? subtitleStreamIndex = null, int? audioStreamIndex = null)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            throw new ArgumentException("Item not found.", nameof(itemId));
        }

        var clip = new Clip(userId, itemId, startTicks, endTicks)
        {
            ItemName = item.Name,
            SubtitleStreamIndex = subtitleStreamIndex,
            AudioStreamIndex = audioStreamIndex
        };

        var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            db.Clips.Add(clip);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        var clipId = clip.Id;
        var sourcePath = item.Path;

        // Fire-and-forget background processing
        _ = Task.Run(() => ProcessClipAsync(clipId, sourcePath, startTicks, endTicks, subtitleStreamIndex, audioStreamIndex));

        return ConvertToDto(clip);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClipDto>> GetClipsAsync(Guid userId)
    {
        var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.Clips
                .Where(c => c.UserId.Equals(userId))
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => ConvertToDto(c))
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<ClipDto?> GetClipAsync(Guid clipId, Guid userId)
    {
        var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var clip = await db.Clips
                .FirstOrDefaultAsync(c => c.Id.Equals(clipId) && c.UserId.Equals(userId))
                .ConfigureAwait(false);

            return clip is null ? null : ConvertToDto(clip);
        }
    }

    /// <inheritdoc />
    public async Task DeleteClipAsync(Guid clipId, Guid userId)
    {
        var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var clip = await db.Clips
                .FirstOrDefaultAsync(c => c.Id.Equals(clipId) && c.UserId.Equals(userId))
                .ConfigureAwait(false);

            if (clip is null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(clip.FilePath) && File.Exists(clip.FilePath))
            {
                try
                {
                    File.Delete(clip.FilePath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to delete clip file: {FilePath}", clip.FilePath);
                }
            }

            db.Clips.Remove(clip);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetClipFilePathAsync(Guid clipId, Guid userId)
    {
        var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var clip = await db.Clips
                .FirstOrDefaultAsync(c => c.Id.Equals(clipId) && c.UserId.Equals(userId) && c.Status == ClipStatus.Ready)
                .ConfigureAwait(false);

            return clip?.FilePath;
        }
    }

    /// <inheritdoc />
    public async Task DeleteExpiredClipsAsync(CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var db = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var expiredClips = await db.Clips
                .Where(c => c.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var clip in expiredClips)
            {
                if (!string.IsNullOrEmpty(clip.FilePath) && File.Exists(clip.FilePath))
                {
                    try
                    {
                        File.Delete(clip.FilePath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete expired clip file: {FilePath}", clip.FilePath);
                    }
                }
            }

            db.Clips.RemoveRange(expiredClips);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted {Count} expired clips", expiredClips.Count);
        }
    }

    private async Task ProcessClipAsync(Guid clipId, string sourcePath, long startTicks, long endTicks, int? subtitleStreamIndex, int? audioStreamIndex)
    {
        try
        {
            await UpdateClipStatusAsync(clipId, ClipStatus.Processing).ConfigureAwait(false);

            var clipsDir = Path.Combine(_appPaths.ProgramDataPath, "clips");
            Directory.CreateDirectory(clipsDir);

            var outputPath = Path.Combine(clipsDir, $"{clipId}.mp4");
            var startSeconds = TimeSpan.FromTicks(startTicks).TotalSeconds;
            var durationSeconds = TimeSpan.FromTicks(endTicks - startTicks).TotalSeconds;

            var ffmpegPath = _mediaEncoder.EncoderPath;

            // Build audio stream mapping if user selected a specific audio track.
            var audioMap = audioStreamIndex.HasValue
                ? $"-map 0:v:0 -map 0:a:{audioStreamIndex.Value}"
                : string.Empty;

            // First try: burn in subtitles. Use the user-selected stream index if provided.
            // -ss is placed after -i so the subtitle filter sees original timestamps.
            var escapedPath = EscapeSubtitleFilterPath(sourcePath);
            var subtitleFilter = subtitleStreamIndex.HasValue
                ? $"subtitles='{escapedPath}':si={subtitleStreamIndex.Value}"
                : $"subtitles='{escapedPath}'";
            var argsWithSubs = $"-i \"{sourcePath}\" -ss {startSeconds:F3} -t {durationSeconds:F3} {audioMap} -vf \"{subtitleFilter}\" -c:v libx264 -preset fast -crf 22 -c:a aac -b:a 128k -sn \"{outputPath}\"";

            _logger.LogInformation("Processing clip {ClipId} with subtitles: {FfmpegPath} {Args}", clipId, ffmpegPath, argsWithSubs);

            var (exitCode, stderr) = await RunFfmpegAsync(ffmpegPath, argsWithSubs).ConfigureAwait(false);

            if (exitCode != 0)
            {
                // Subtitle burn-in failed (e.g. no subtitle streams, or bitmap-only subs).
                // Retry without subtitles using fast input seeking.
                _logger.LogInformation("Subtitle burn-in failed for clip {ClipId}, retrying without subtitles", clipId);

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                var argsNoSubs = $"-ss {startSeconds:F3} -i \"{sourcePath}\" -t {durationSeconds:F3} {audioMap} -c:v libx264 -preset fast -crf 22 -c:a aac -b:a 128k -sn \"{outputPath}\"";

                _logger.LogInformation("Processing clip {ClipId} without subtitles: {FfmpegPath} {Args}", clipId, ffmpegPath, argsNoSubs);

                (exitCode, stderr) = await RunFfmpegAsync(ffmpegPath, argsNoSubs).ConfigureAwait(false);

                if (exitCode != 0)
                {
                    _logger.LogError("ffmpeg failed for clip {ClipId} with exit code {ExitCode}: {StdErr}", clipId, exitCode, stderr);
                    await UpdateClipStatusAsync(clipId, ClipStatus.Failed).ConfigureAwait(false);
                    return;
                }
            }

            var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                var clip = await db.Clips.FindAsync(clipId).ConfigureAwait(false);
                if (clip is not null)
                {
                    clip.Status = ClipStatus.Ready;
                    clip.FilePath = outputPath;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Clip {ClipId} processed successfully", clipId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing clip {ClipId}", clipId);
            await UpdateClipStatusAsync(clipId, ClipStatus.Failed).ConfigureAwait(false);
        }
    }

    private static async Task<(int ExitCode, string StdErr)> RunFfmpegAsync(string ffmpegPath, string args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return (process.ExitCode, stderr);
    }

    private static string EscapeSubtitleFilterPath(string path)
    {
        return path
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    private async Task UpdateClipStatusAsync(Guid clipId, ClipStatus status)
    {
        try
        {
            var db = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                var clip = await db.Clips.FindAsync(clipId).ConfigureAwait(false);
                if (clip is not null)
                {
                    clip.Status = status;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update clip {ClipId} status to {Status}", clipId, status);
        }
    }

    private static ClipDto ConvertToDto(Clip clip)
    {
        return new ClipDto
        {
            Id = clip.Id,
            ItemId = clip.ItemId,
            ItemName = clip.ItemName,
            StartTicks = clip.StartTicks,
            EndTicks = clip.EndTicks,
            CreatedAt = clip.CreatedAt,
            SubtitleStreamIndex = clip.SubtitleStreamIndex,
            AudioStreamIndex = clip.AudioStreamIndex,
            Status = clip.Status
        };
    }
}
