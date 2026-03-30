using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class DataSourceRefreshConfiguration : IEntityTypeConfiguration<DataSourceRefresh>
{
    public void Configure(EntityTypeBuilder<DataSourceRefresh> builder)
    {
        builder.ToTable("data_source_refresh");

        builder.HasKey(e => e.RefreshId);
        builder.Property(e => e.RefreshId).HasColumnName("refresh_id").ValueGeneratedOnAdd();
        builder.Property(e => e.SourceName).HasColumnName("source_name").IsRequired();
        builder.Property(e => e.StartDate).HasColumnName("start_date");
        builder.Property(e => e.EndDate).HasColumnName("end_date");
        builder.Property(e => e.LastRefreshedAt).HasColumnName("last_refreshed_at");
        builder.Property(e => e.RecordsFetched).HasColumnName("records_fetched");
        builder.Property(e => e.Success).HasColumnName("success");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.SyncToken).HasColumnName("sync_token");

        builder.HasIndex(e => new { e.SourceName, e.LastRefreshedAt }).HasDatabaseName("idx_refresh_source");
        builder.HasIndex(e => new { e.SourceName, e.StartDate, e.EndDate }).HasDatabaseName("idx_refresh_date");
    }
}
