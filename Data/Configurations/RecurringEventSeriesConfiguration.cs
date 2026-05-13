using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class RecurringEventSeriesConfiguration : IEntityTypeConfiguration<RecurringEventSeries>
{
    public void Configure(EntityTypeBuilder<RecurringEventSeries> builder)
    {
        builder.ToTable("recurring_event_series");

        builder.HasKey(e => e.SeriesId);
        builder.Property(e => e.SeriesId).HasColumnName("series_id").ValueGeneratedNever();
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.Recurrence).HasColumnName("recurrence").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.ColorId).HasColumnName("color_id");
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day");
        builder.Property(e => e.SeriesStartDatetime).HasColumnName("series_start_datetime");
        builder.Property(e => e.SeriesEndDatetime).HasColumnName("series_end_datetime");
        builder.Property(e => e.GcalEtag).HasColumnName("gcal_etag");
        builder.Property(e => e.GcalUpdatedAt).HasColumnName("gcal_updated_at");
        builder.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
