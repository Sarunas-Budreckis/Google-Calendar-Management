using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class TogglEntryConfiguration : IEntityTypeConfiguration<TogglEntry>
{
    public void Configure(EntityTypeBuilder<TogglEntry> builder)
    {
        builder.ToTable("toggl_data");

        builder.HasKey(e => e.TogglId);
        builder.Property(e => e.TogglId).HasColumnName("toggl_id").ValueGeneratedNever();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(e => e.EndTime).HasColumnName("end_time");
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(e => e.ProjectName).HasColumnName("project_name");
        builder.Property(e => e.Tags).HasColumnName("tags");
        builder.Property(e => e.VisibleAsEvent).HasColumnName("visible_as_event").HasDefaultValue(true);
        builder.Property(e => e.PublishedToGcal).HasColumnName("published_to_gcal").HasDefaultValue(false);
        builder.Property(e => e.PublishedGcalEventId).HasColumnName("published_gcal_event_id");
        builder.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(e => new { e.StartTime, e.EndTime }).HasDatabaseName("idx_toggl_date");
        builder.HasIndex(e => e.Description).HasDatabaseName("idx_toggl_description");

        builder.HasOne(e => e.PublishedGcalEvent)
            .WithMany()
            .HasForeignKey(e => e.PublishedGcalEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
