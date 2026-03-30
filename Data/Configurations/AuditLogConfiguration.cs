using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.LogId);
        builder.Property(e => e.LogId).HasColumnName("log_id").ValueGeneratedOnAdd();
        builder.Property(e => e.Timestamp).HasColumnName("timestamp");
        builder.Property(e => e.OperationType).HasColumnName("operation_type").IsRequired();
        builder.Property(e => e.OperationDetails).HasColumnName("operation_details");
        builder.Property(e => e.AffectedDates).HasColumnName("affected_dates");
        builder.Property(e => e.AffectedEvents).HasColumnName("affected_events");
        builder.Property(e => e.UserAction).HasColumnName("user_action");
        builder.Property(e => e.Success).HasColumnName("success");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");

        builder.HasIndex(e => e.Timestamp).HasDatabaseName("idx_audit_timestamp");
        builder.HasIndex(e => e.OperationType).HasDatabaseName("idx_audit_operation");
    }
}
