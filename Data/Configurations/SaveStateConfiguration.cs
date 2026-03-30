using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class SaveStateConfiguration : IEntityTypeConfiguration<SaveState>
{
    public void Configure(EntityTypeBuilder<SaveState> builder)
    {
        builder.ToTable("save_state");

        builder.HasKey(e => e.SaveId);
        builder.Property(e => e.SaveId).HasColumnName("save_id").ValueGeneratedOnAdd();
        builder.Property(e => e.SaveName).HasColumnName("save_name").IsRequired();
        builder.Property(e => e.SaveDescription).HasColumnName("save_description");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.SnapshotData).HasColumnName("snapshot_data");
    }
}
