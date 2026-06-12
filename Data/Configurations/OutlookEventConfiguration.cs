using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class OutlookEventConfiguration : IEntityTypeConfiguration<OutlookEvent>
{
    public void Configure(EntityTypeBuilder<OutlookEvent> builder)
    {
        builder.ToTable("outlook_event");

        builder.HasKey(e => e.OutlookEventId);
        builder.Property(e => e.OutlookEventId).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Subject).HasColumnName("subject").IsRequired();
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime");
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.Organizer).HasColumnName("organizer");
        builder.Property(e => e.Location).HasColumnName("location");
        builder.Property(e => e.BodyPreview).HasColumnName("body_preview");
        builder.Property(e => e.IsRecurring).HasColumnName("is_recurring");
        builder.Property(e => e.SeriesMasterId).HasColumnName("series_master_id");
        builder.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(e => e.IsSuppressed).HasColumnName("is_suppressed").HasDefaultValue(false);

        builder.HasIndex(e => e.StartDatetime).HasDatabaseName("idx_outlook_event_start");
        builder.HasIndex(e => e.IsSuppressed).HasDatabaseName("idx_outlook_event_suppressed");
    }
}
