using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class PendingEventConfiguration : IEntityTypeConfiguration<PendingEvent>
{
    public void Configure(EntityTypeBuilder<PendingEvent> builder)
    {
        builder.ToTable("pending_event");

        builder.HasKey(e => e.PendingEventId);
        builder.Property(e => e.PendingEventId).HasColumnName("pending_event_id").ValueGeneratedNever();
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id");
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime");
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.ColorId).HasColumnName("color_id");
        builder.Property(e => e.AppCreated).HasColumnName("app_created").ValueGeneratedNever();
        builder.Property(e => e.SourceSystem).HasColumnName("source_system");
        builder.Property(e => e.ReadyToPublish).HasColumnName("ready_to_publish").ValueGeneratedNever();
        builder.Property(e => e.PublishAttemptedAt).HasColumnName("publish_attempted_at");
        builder.Property(e => e.PublishError).HasColumnName("publish_error");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.GcalEventId)
            .IsUnique()
            .HasDatabaseName("idx_pending_event_gcal_event_id");

        builder.HasIndex(e => new { e.StartDatetime, e.EndDatetime })
            .HasDatabaseName("idx_pending_event_date");

        builder.HasOne(e => e.GcalEvent)
            .WithOne(e => e.PendingEvent)
            .HasForeignKey<PendingEvent>(e => e.GcalEventId)
            .HasPrincipalKey<GcalEvent>(e => e.GcalEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
