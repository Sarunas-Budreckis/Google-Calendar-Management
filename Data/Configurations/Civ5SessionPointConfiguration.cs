using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class Civ5SessionPointConfiguration : IEntityTypeConfiguration<Civ5SessionPoint>
{
    public void Configure(EntityTypeBuilder<Civ5SessionPoint> builder)
    {
        builder.ToTable("civ5_data");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ScannedAt).HasColumnName("scanned_at").IsRequired();
        builder.Property(e => e.FileModifiedAt).HasColumnName("file_modified_at").IsRequired();
        builder.Property(e => e.GameMode).HasColumnName("game_mode").IsRequired();
        builder.Property(e => e.LinkedEventId).HasColumnName("linked_event_id");
        builder.Property(e => e.LinkedEventType).HasColumnName("linked_event_type");

        builder.HasIndex(e => new { e.FileModifiedAt, e.GameMode })
            .IsUnique()
            .HasDatabaseName("idx_civ5_data_dedup");

        builder.HasIndex(e => e.FileModifiedAt)
            .HasDatabaseName("idx_civ5_data_file_modified_at");
    }
}
