using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddSpotifyStream : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "spotify_stream",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                played_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                track_name = table.Column<string>(type: "TEXT", nullable: false),
                artist_name = table.Column<string>(type: "TEXT", nullable: false),
                album_name = table.Column<string>(type: "TEXT", nullable: true),
                duration_ms = table.Column<int>(type: "INTEGER", nullable: false),
                ms_played = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_spotify_stream", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_spotify_stream_dedup",
            table: "spotify_stream",
            columns: new[] { "played_at", "track_name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_spotify_stream_played_at",
            table: "spotify_stream",
            column: "played_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "spotify_stream");
    }
}
