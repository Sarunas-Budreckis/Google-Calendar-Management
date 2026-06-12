using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event", t =>
        {
            t.HasCheckConstraint("CK_event_lifecycle", "lifecycle IN ('candidate','approved')");
            t.HasCheckConstraint("CK_event_publish", "publish IN ('local_only','published')");
        });

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id");
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime");
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.ColorId).HasColumnName("color_id");
        builder.Property(e => e.Lifecycle).HasColumnName("lifecycle").IsRequired().HasDefaultValue("approved");
        builder.Property(e => e.Publish).HasColumnName("publish").IsRequired().HasDefaultValue("local_only");
        builder.Property(e => e.HasUnpublishedChanges).HasColumnName("has_unpublished_changes").HasDefaultValue(false);
        builder.Property(e => e.SourceSystem).HasColumnName("source_system");
        builder.Property(e => e.RecurringEventId).HasColumnName("recurring_event_id");
        builder.Property(e => e.IsRecurringInstance).HasColumnName("is_recurring_instance").HasDefaultValue(false);
        builder.Property(e => e.GcalEtag).HasColumnName("gcal_etag");
        builder.Property(e => e.GcalUpdatedAt).HasColumnName("gcal_updated_at");
        builder.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(e => e.AppLastModifiedAt).HasColumnName("app_last_modified_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // gcal_event_id is unique only when set (a local-only event has no gcal id).
        builder.HasIndex(e => e.GcalEventId)
            .IsUnique()
            .HasFilter("gcal_event_id IS NOT NULL")
            .HasDatabaseName("idx_event_gcal_event_id");

        builder.HasIndex(e => new { e.StartDatetime, e.EndDatetime }).HasDatabaseName("idx_event_date");
        builder.HasIndex(e => e.Lifecycle).HasDatabaseName("idx_event_lifecycle");
        builder.HasIndex(e => e.SourceSystem).HasDatabaseName("idx_event_source");
        builder.HasIndex(e => e.RecurringEventId).HasDatabaseName("idx_event_recurring");

        // Preserves the old day_name uniqueness guard (one 'day_name' event per calendar day).
        builder.HasIndex(e => new { e.StartDatetime, e.SourceSystem })
            .IsUnique()
            .HasFilter("source_system = 'day_name'")
            .HasDatabaseName("idx_event_day_name_unique");

        builder.HasMany(e => e.Versions)
               .WithOne(v => v.Event)
               .HasForeignKey(v => v.EventId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
