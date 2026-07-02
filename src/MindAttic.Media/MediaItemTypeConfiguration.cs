using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MindAttic.Media;

public sealed class MediaItemTypeConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Uid).IsRequired();
        builder.HasIndex(m => m.Uid).IsUnique();

        builder.Property(m => m.MediaType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Folder).HasMaxLength(400).IsRequired();
        builder.Property(m => m.FileName).HasMaxLength(400).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(m => m.BlobUri).HasMaxLength(1024);
        builder.Property(m => m.Notes).HasMaxLength(2000);

        builder.Property(m => m.Bytes).HasColumnType("varbinary(max)");
        builder.Property(m => m.Extra).HasColumnType("nvarchar(max)");

        builder.Property(m => m.RowVersion).IsRowVersion();

        builder.HasIndex(m => m.Sha256);
        builder.HasIndex(m => new { m.TenantId, m.Folder, m.FileName });
    }
}
