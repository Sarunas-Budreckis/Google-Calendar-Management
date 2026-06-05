using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class SpotifyStreamConfiguration : IEntityTypeConfiguration<SpotifyStream>
{
    public void Configure(EntityTypeBuilder<SpotifyStream> builder)
    {
        builder.ToTable("spotify_stream");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.PlayedAt).HasColumnName("played_at").IsRequired();
        builder.Property(e => e.TrackName).HasColumnName("track_name").IsRequired();
        builder.Property(e => e.ArtistName).HasColumnName("artist_name").IsRequired();
        builder.Property(e => e.AlbumName).HasColumnName("album_name");
        builder.Property(e => e.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(e => e.MsPlayed).HasColumnName("ms_played").IsRequired();

        builder.HasIndex(e => new { e.PlayedAt, e.TrackName })
            .IsUnique()
            .HasDatabaseName("idx_spotify_stream_dedup");
        builder.HasIndex(e => e.PlayedAt)
            .HasDatabaseName("idx_spotify_stream_played_at");
    }
}
