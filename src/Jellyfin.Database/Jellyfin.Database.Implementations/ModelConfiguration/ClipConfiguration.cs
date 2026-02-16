using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration;

/// <summary>
/// FluentAPI configuration for the Clip entity.
/// </summary>
public class ClipConfiguration : IEntityTypeConfiguration<Clip>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Clip> builder)
    {
        builder.HasIndex(entity => entity.UserId);
        builder.HasIndex(entity => entity.CreatedAt);
    }
}
