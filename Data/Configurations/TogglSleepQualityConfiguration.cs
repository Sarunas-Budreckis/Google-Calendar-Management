using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class TogglSleepQualityConfiguration : IEntityTypeConfiguration<TogglSleepQuality>
{
    public void Configure(EntityTypeBuilder<TogglSleepQuality> builder)
    {
        builder.ToTable("toggl_sleep_quality", table =>
        {
            table.HasCheckConstraint(
                "CK_toggl_sleep_quality_quality_range",
                "quality IS NULL OR (quality >= 0 AND quality <= 10)");
        });

        builder.HasKey(e => e.Date);
        builder.Property(e => e.Date).HasColumnName("date").IsRequired();
        builder.Property(e => e.Quality).HasColumnName("quality");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
