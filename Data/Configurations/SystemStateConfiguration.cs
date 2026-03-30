using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class SystemStateConfiguration : IEntityTypeConfiguration<SystemState>
{
    public void Configure(EntityTypeBuilder<SystemState> builder)
    {
        builder.ToTable("system_state");

        builder.HasKey(e => e.StateId);
        builder.Property(e => e.StateId).HasColumnName("state_id").ValueGeneratedOnAdd();
        builder.Property(e => e.StateName).HasColumnName("state_name");
        builder.Property(e => e.StateValue).HasColumnName("state_value");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.StateName).IsUnique();
    }
}
