using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DataSourceImportLogConfiguration : IEntityTypeConfiguration<DataSourceImportLog>
{
    public void Configure(EntityTypeBuilder<DataSourceImportLog> builder)
    {
        builder.ToTable("data_source_import_log");

        builder.HasKey(e => e.ImportLogId);
        builder.Property(e => e.ImportLogId).HasColumnName("import_log_id").ValueGeneratedOnAdd();
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id").IsRequired();
        builder.Property(e => e.CoveredStartDate).HasColumnName("covered_start_date").IsRequired();
        builder.Property(e => e.CoveredEndDate).HasColumnName("covered_end_date").IsRequired();
        builder.Property(e => e.ImportedAt).HasColumnName("imported_at").IsRequired();
        builder.Property(e => e.RecordsFetched).HasColumnName("records_fetched");
        builder.Property(e => e.Success).HasColumnName("success").IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");

        builder.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
