using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DeletedEventConfiguration : IEntityTypeConfiguration<DeletedEvent>
{
    public void Configure(EntityTypeBuilder<DeletedEvent> builder)
    {
        builder.ToTable("deleted_event");

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
        builder.Property(e => e.RecurringEventId).HasColumnName("recurring_event_id");
        builder.Property(e => e.IsRecurringInstance).HasColumnName("is_recurring_instance");
        builder.Property(e => e.AppCreated).HasColumnName("app_created");
        builder.Property(e => e.SourceSystem).HasColumnName("source_system");
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at").IsRequired();
        builder.Property(e => e.DeletionSource).HasColumnName("deletion_source").IsRequired();
        builder.Property(e => e.OriginalCreatedAt).HasColumnName("original_created_at");
        builder.Property(e => e.OriginalUpdatedAt).HasColumnName("original_updated_at");

        builder.HasIndex(e => e.StartDatetime).HasDatabaseName("idx_deleted_event_date");
    }
}
