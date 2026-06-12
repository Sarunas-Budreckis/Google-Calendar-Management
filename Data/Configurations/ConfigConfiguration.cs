using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class ConfigConfiguration : IEntityTypeConfiguration<Config>
{
    public void Configure(EntityTypeBuilder<Config> builder)
    {
        builder.ToTable("config");

        builder.HasKey(e => e.ConfigKey);
        builder.Property(e => e.ConfigKey).HasColumnName("config_key").ValueGeneratedNever();
        builder.Property(e => e.ConfigValue).HasColumnName("config_value");
        builder.Property(e => e.ConfigType).HasColumnName("config_type");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

    }
}
