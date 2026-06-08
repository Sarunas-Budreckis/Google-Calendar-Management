using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class ComfyUIScanPointConfiguration : IEntityTypeConfiguration<ComfyUIScanPoint>
{
    public const string CreatedEventType = "created";
    public const string ModifiedEventType = "modified";

    public void Configure(EntityTypeBuilder<ComfyUIScanPoint> builder)
    {
        builder.ToTable("comfyui_data");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ScannedAt).HasColumnName("scanned_at").IsRequired();
        builder.Property(e => e.Timestamp).HasColumnName("timestamp").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(e => e.LinkedEventId).HasColumnName("linked_event_id");
        builder.Property(e => e.LinkedEventType).HasColumnName("linked_event_type");

        builder.HasIndex(e => new { e.Timestamp, e.EventType })
            .IsUnique()
            .HasDatabaseName("idx_comfyui_data_dedup");

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("idx_comfyui_data_timestamp");
    }
}
