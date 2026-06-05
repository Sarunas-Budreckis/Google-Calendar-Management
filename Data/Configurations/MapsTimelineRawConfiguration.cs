using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class MapsTimelineRawConfiguration : IEntityTypeConfiguration<MapsTimelineRaw>
{
    public void Configure(EntityTypeBuilder<MapsTimelineRaw> builder)
    {
        builder.ToTable("maps_timeline_raw");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ImportedAt).HasColumnName("imported_at").IsRequired();
        builder.Property(e => e.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(e => e.CoveredDateMin).HasColumnName("covered_date_min");
        builder.Property(e => e.CoveredDateMax).HasColumnName("covered_date_max");
        builder.Property(e => e.RawJson).HasColumnName("raw_json").IsRequired();
    }
}
