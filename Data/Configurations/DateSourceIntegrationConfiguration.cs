using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DateSourceIntegrationConfiguration : IEntityTypeConfiguration<DateSourceIntegration>
{
    public void Configure(EntityTypeBuilder<DateSourceIntegration> builder)
    {
        builder.ToTable("date_source_integration");

        builder.HasKey(e => e.IntegrationId);
        builder.Property(e => e.IntegrationId).HasColumnName("integration_id").ValueGeneratedOnAdd();
        builder.Property(e => e.Date).HasColumnName("date").IsRequired();
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id").IsRequired();
        builder.Property(e => e.Integrated).HasColumnName("integrated").HasDefaultValue(false);
        builder.Property(e => e.IntegratedAt).HasColumnName("integrated_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.Date, e.DataSourceId })
            .IsUnique()
            .HasDatabaseName("idx_date_source_integration_date_source");

        builder.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
