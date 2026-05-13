using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("data_source");

        builder.HasKey(e => e.DataSourceId);
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id").ValueGeneratedOnAdd();
        builder.Property(e => e.SourceKey).HasColumnName("source_key").IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.SupportsNoDataHint).HasColumnName("supports_no_data_hint").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.SourceKey).IsUnique().HasDatabaseName("idx_data_source_key");
    }
}
