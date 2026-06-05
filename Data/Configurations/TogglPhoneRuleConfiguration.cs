using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class TogglPhoneRuleConfiguration : IEntityTypeConfiguration<TogglPhoneRule>
{
    public void Configure(EntityTypeBuilder<TogglPhoneRule> builder)
    {
        builder.ToTable("toggl_phone_rule");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.DateFrom).HasColumnName("date_from");
        builder.Property(e => e.DateTo).HasColumnName("date_to");
        builder.Property(e => e.DescriptionPattern).HasColumnName("description_pattern").IsRequired();
        builder.Property(e => e.MaxDurationMinutes).HasColumnName("max_duration_minutes");
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes");
    }
}
