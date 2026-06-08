using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class RenameSpotifyStreamToSpotifyData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"spotify_stream\" RENAME TO \"spotify_data\"");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"idx_spotify_stream_dedup\"");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"idx_spotify_stream_played_at\"");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"idx_spotify_data_dedup\" ON \"spotify_data\" (\"played_at\", \"track_name\")");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"idx_spotify_data_played_at\" ON \"spotify_data\" (\"played_at\")");
        migrationBuilder.Sql("INSERT OR IGNORE INTO data_source (source_key, display_name, supports_no_data_hint, created_at) VALUES ('spotify', 'Spotify (stats.fm)', 0, '2026-06-05 00:00:00')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"idx_spotify_data_dedup\"");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"idx_spotify_data_played_at\"");
        migrationBuilder.Sql("ALTER TABLE \"spotify_data\" RENAME TO \"spotify_stream\"");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"idx_spotify_stream_dedup\" ON \"spotify_stream\" (\"played_at\", \"track_name\")");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"idx_spotify_stream_played_at\" ON \"spotify_stream\" (\"played_at\")");
    }
}
