using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DataPointConfiguration : IEntityTypeConfiguration<DataPoint>
{
    public void Configure(EntityTypeBuilder<DataPoint> builder)
    {
        builder.ToTable("data_point");

        builder.HasKey(e => e.DataPointId);
        builder.Property(e => e.DataPointId).HasColumnName("data_point_id").ValueGeneratedOnAdd();
        builder.Property(e => e.SourceKey).HasColumnName("source_key").IsRequired().HasMaxLength(100);
        builder.Property(e => e.SourceRef).HasColumnName("source_ref").IsRequired().HasMaxLength(500);
        builder.Property(e => e.StartUtc).HasColumnName("start_utc").IsRequired();
        builder.Property(e => e.EndUtc).HasColumnName("end_utc").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.StartUtc)
            .HasDatabaseName("idx_data_point_start_utc");
        builder.HasIndex(e => new { e.SourceKey, e.StartUtc })
            .HasDatabaseName("idx_data_point_source_key_start_utc");
    }
}
