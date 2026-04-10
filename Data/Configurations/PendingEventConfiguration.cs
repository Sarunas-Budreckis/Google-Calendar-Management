using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class PendingEventConfiguration : IEntityTypeConfiguration<PendingEvent>
{
    public void Configure(EntityTypeBuilder<PendingEvent> builder)
    {
        builder.ToTable("pending_event");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.StartDatetime).HasColumnName("start_datetime").IsRequired();
        builder.Property(e => e.EndDatetime).HasColumnName("end_datetime").IsRequired();
        builder.Property(e => e.ColorId).HasColumnName("color_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.GcalEventId)
            .IsUnique()
            .HasDatabaseName("idx_pending_event_gcal_event_id");

        builder.HasOne(e => e.GcalEvent)
            .WithOne(e => e.PendingEvent)
            .HasForeignKey<PendingEvent>(e => e.GcalEventId)
            .HasPrincipalKey<GcalEvent>(e => e.GcalEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
