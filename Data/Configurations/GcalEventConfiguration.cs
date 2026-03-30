using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class GcalEventConfiguration : IEntityTypeConfiguration<GcalEvent>
{
    public void Configure(EntityTypeBuilder<GcalEvent> builder)
    {
        builder.ToTable("gcal_event");

        builder.HasKey(e => e.GcalEventId);
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id").ValueGeneratedNever();
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime");
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.ColorId).HasColumnName("color_id");
        builder.Property(e => e.GcalEtag).HasColumnName("gcal_etag");
        builder.Property(e => e.GcalUpdatedAt).HasColumnName("gcal_updated_at");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.AppCreated).HasColumnName("app_created").HasDefaultValue(false);
        builder.Property(e => e.SourceSystem).HasColumnName("source_system");
        builder.Property(e => e.AppPublished).HasColumnName("app_published").HasDefaultValue(false);
        builder.Property(e => e.AppPublishedAt).HasColumnName("app_published_at");
        builder.Property(e => e.AppLastModifiedAt).HasColumnName("app_last_modified_at");
        builder.Property(e => e.RecurringEventId).HasColumnName("recurring_event_id");
        builder.Property(e => e.IsRecurringInstance).HasColumnName("is_recurring_instance").HasDefaultValue(false);
        builder.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.StartDatetime, e.EndDatetime }).HasDatabaseName("idx_gcal_event_date");
        builder.HasIndex(e => e.RecurringEventId).HasDatabaseName("idx_gcal_recurring");
        builder.HasIndex(e => e.SourceSystem).HasDatabaseName("idx_gcal_source");
        builder.HasIndex(e => e.AppCreated).HasDatabaseName("idx_gcal_app_created");

        builder.HasMany(e => e.Versions)
               .WithOne(v => v.GcalEvent)
               .HasForeignKey(v => v.GcalEventId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
