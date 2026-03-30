using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class GcalEventVersionConfiguration : IEntityTypeConfiguration<GcalEventVersion>
{
    public void Configure(EntityTypeBuilder<GcalEventVersion> builder)
    {
        builder.ToTable("gcal_event_version");

        builder.HasKey(e => e.VersionId);
        builder.Property(e => e.VersionId).HasColumnName("version_id").ValueGeneratedOnAdd();
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id").IsRequired();
        builder.Property(e => e.GcalEtag).HasColumnName("gcal_etag");
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime");
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.ColorId).HasColumnName("color_id");
        builder.Property(e => e.GcalUpdatedAt).HasColumnName("gcal_updated_at");
        builder.Property(e => e.RecurringEventId).HasColumnName("recurring_event_id");
        builder.Property(e => e.IsRecurringInstance).HasColumnName("is_recurring_instance").HasDefaultValue(false);
        builder.Property(e => e.ChangedBy).HasColumnName("changed_by");
        builder.Property(e => e.ChangeReason).HasColumnName("change_reason");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => new { e.GcalEventId, e.CreatedAt }).HasDatabaseName("idx_version_event");
    }
}
