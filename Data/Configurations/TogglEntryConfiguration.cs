using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GoogleCalendarManagement.Data.Configurations;

public class TogglEntryConfiguration : IEntityTypeConfiguration<TogglEntry>
{
    private static readonly ValueConverter<TogglDataType, string> TogglDataTypeConverter = new(
        v => ToDatabaseValue(v),
        s => FromDatabaseValue(s));

    private static string ToDatabaseValue(TogglDataType value) => value switch
    {
        TogglDataType.TogglSleep => "toggl_sleep",
        TogglDataType.TogglTransit => "toggl_transit",
        TogglDataType.TogglPhone => "toggl_phone",
        _ => throw new InvalidOperationException($"Unsupported Toggl data type: {value}")
    };

    private static TogglDataType FromDatabaseValue(string value) => value switch
    {
        "toggl_sleep" => TogglDataType.TogglSleep,
        "toggl_transit" => TogglDataType.TogglTransit,
        "toggl_phone" => TogglDataType.TogglPhone,
        _ => throw new InvalidOperationException($"Unsupported Toggl data type value: {value}")
    };

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

        builder.Property(e => e.TogglDataType)
            .HasColumnName("toggl_data_type")
            .HasConversion(TogglDataTypeConverter);
        builder.Property(e => e.LinkedEventId).HasColumnName("linked_event_id");
        builder.Property(e => e.LinkedEventType).HasColumnName("linked_event_type");

        builder.HasIndex(e => new { e.StartTime, e.EndTime }).HasDatabaseName("idx_toggl_date");
        builder.HasIndex(e => e.Description).HasDatabaseName("idx_toggl_description");
        builder.HasIndex(e => e.TogglDataType).HasDatabaseName("idx_toggl_type");

        // Story 8.2: the FK navigation from toggl_data to the curated event was removed. Mapping it
        // would force event.gcal_event_id to become a NOT NULL alternate key, which conflicts with
        // the nullable, filtered-UNIQUE gcal_event_id the unified event model requires. The column
        // published_gcal_event_id remains as a plain scalar; linking moves to the link table (8.7+).
    }
}
