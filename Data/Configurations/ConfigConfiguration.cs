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

        builder.HasData(
            new Config { ConfigKey = "min_event_duration_minutes", ConfigValue = "5", ConfigType = "integer", Description = "Minimum duration to show events", UpdatedAt = new DateTime(2026, 1, 1) },
            new Config { ConfigKey = "phone_coalesce_gap_minutes", ConfigValue = "15", ConfigType = "integer", Description = "Max gap for phone coalescing", UpdatedAt = new DateTime(2026, 1, 1) },
            new Config { ConfigKey = "youtube_coalesce_gap_minutes", ConfigValue = "30", ConfigType = "integer", Description = "Gap after video duration for YouTube", UpdatedAt = new DateTime(2026, 1, 1) },
            new Config { ConfigKey = "call_min_duration_minutes", ConfigValue = "3", ConfigType = "integer", Description = "Minimum call duration to import", UpdatedAt = new DateTime(2026, 1, 1) },
            new Config { ConfigKey = "youtube_char_limit_short", ConfigValue = "40", ConfigType = "integer", Description = "Char limit for events <90min", UpdatedAt = new DateTime(2026, 1, 1) },
            new Config { ConfigKey = "eight_fifteen_threshold", ConfigValue = "8", ConfigType = "integer", Description = "Minutes required in 15-min block", UpdatedAt = new DateTime(2026, 1, 1) }
        );
    }
}
