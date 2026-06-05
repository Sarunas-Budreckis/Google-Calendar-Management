using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class CallLogEntryConfiguration : IEntityTypeConfiguration<CallLogEntry>
{
    public void Configure(EntityTypeBuilder<CallLogEntry> builder)
    {
        builder.ToTable("call_log_entry");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ImportId).HasColumnName("import_id").IsRequired();
        builder.Property(e => e.CallType).HasColumnName("call_type").IsRequired();
        builder.Property(e => e.Date).HasColumnName("date").IsRequired();
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds").IsRequired();
        builder.Property(e => e.Number).HasColumnName("number");
        builder.Property(e => e.Contact).HasColumnName("contact");
        builder.Property(e => e.Location).HasColumnName("location");
        builder.Property(e => e.Service).HasColumnName("service").IsRequired();
        builder.Property(e => e.LinkedEventId).HasColumnName("linked_event_id");
        builder.Property(e => e.LinkedEventType).HasColumnName("linked_event_type");

        builder.HasOne(e => e.Import)
            .WithMany(i => i.Entries)
            .HasForeignKey(e => e.ImportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Date).HasDatabaseName("idx_call_log_entry_date");
        builder.HasIndex(e => new { e.Date, e.Number, e.DurationSeconds })
            .HasDatabaseName("idx_call_log_entry_dedup");
    }
}
