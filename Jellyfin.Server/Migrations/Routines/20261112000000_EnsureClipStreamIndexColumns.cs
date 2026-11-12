using System;
using System.Linq;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Migrations.Routines;

/// <summary>
/// Adds the Clips stream index columns where they are missing. Early builds of the clips feature created
/// the table without them and some databases gained one of them later, which a plain EF AddColumn
/// migration would fail on. New databases get both columns from AddClips and never run this.
/// </summary>
[JellyfinMigration("2026-11-12T00:00:00", nameof(EnsureClipStreamIndexColumns))]
internal class EnsureClipStreamIndexColumns : IDatabaseMigrationRoutine
{
    private readonly ILogger<EnsureClipStreamIndexColumns> _logger;
    private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

    public EnsureClipStreamIndexColumns(
        ILoggerFactory loggerFactory,
        IDbContextFactory<JellyfinDbContext> dbProvider)
    {
        _logger = loggerFactory.CreateLogger<EnsureClipStreamIndexColumns>();
        _dbProvider = dbProvider;
    }

    /// <inheritdoc/>
    public void Perform()
    {
        using var context = _dbProvider.CreateDbContext();

        var columns = context.Database
            .SqlQueryRaw<string>("""SELECT "name" AS "Value" FROM pragma_table_info('Clips')""")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("SubtitleStreamIndex"))
        {
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Clips" ADD "SubtitleStreamIndex" INTEGER NULL""");
            _logger.LogInformation("Added Clips.SubtitleStreamIndex");
        }

        if (!columns.Contains("AudioStreamIndex"))
        {
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Clips" ADD "AudioStreamIndex" INTEGER NULL""");
            _logger.LogInformation("Added Clips.AudioStreamIndex");
        }
    }
}
