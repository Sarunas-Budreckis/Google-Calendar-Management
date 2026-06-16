using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("link", table =>
        {
            table.HasCheckConstraint("CK_link_state", "state IN ('linked','ignored')");
            table.HasCheckConstraint("CK_link_origin", "origin IN ('manual','auto_rule')");
        });
        builder.HasKey(x => x.LinkId);
        builder.Property(x => x.LinkId).HasColumnName("link_id").ValueGeneratedOnAdd();
        builder.Property(x => x.DataPointId).HasColumnName("data_point_id").IsRequired();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.Origin).HasColumnName("origin").IsRequired();
        builder.Property(x => x.RuleId).HasColumnName("rule_id");
        builder.Property(x => x.ActionGroupId).HasColumnName("action_group_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // One link row per datapoint (max)
        builder.HasIndex(x => x.DataPointId).IsUnique().HasDatabaseName("idx_link_data_point_id");
        builder.HasIndex(x => x.EventId).HasDatabaseName("idx_link_event_id");
        builder.HasIndex(x => x.ActionGroupId).HasDatabaseName("idx_link_action_group_id");

        // Cascade from datapoint — if raw data is removed, its resolution is removed too
        builder.HasOne(x => x.DataPoint)
            .WithMany(dp => dp.Links)
            .HasForeignKey(x => x.DataPointId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on event — service must clean up links before deleting an event
        builder.HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
