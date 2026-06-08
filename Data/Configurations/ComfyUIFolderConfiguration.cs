using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class ComfyUIFolderConfiguration : IEntityTypeConfiguration<ComfyUIFolder>
{
    public void Configure(EntityTypeBuilder<ComfyUIFolder> builder)
    {
        builder.ToTable("comfyui_folder");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.FolderPath).HasColumnName("folder_path").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.AddedAt).HasColumnName("added_at").IsRequired();

        builder.HasIndex(e => e.FolderPath)
            .IsUnique()
            .HasDatabaseName("idx_comfyui_folder_path");
    }
}
