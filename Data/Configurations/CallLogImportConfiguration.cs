using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class CallLogImportConfiguration : IEntityTypeConfiguration<CallLogImport>
{
    public void Configure(EntityTypeBuilder<CallLogImport> builder)
    {
        builder.ToTable("call_log_import");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ImportedAt).HasColumnName("imported_at").IsRequired();
        builder.Property(e => e.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(e => e.RecordCount).HasColumnName("record_count").IsRequired();
        builder.Property(e => e.DateMin).HasColumnName("date_min").IsRequired();
        builder.Property(e => e.DateMax).HasColumnName("date_max").IsRequired();
    }
}
