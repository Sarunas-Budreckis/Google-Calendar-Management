using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotifyStreamNaturalKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "natural_key",
                table: "spotify_data",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE spotify_data SET natural_key = played_at || '|' || track_name");

            // Existing upsert logic already deduplicates on (played_at, track_name);
            // if old data violates that assumption, this unique index creation fails loudly.
            migrationBuilder.CreateIndex(
                name: "idx_spotify_natural_key",
                table: "spotify_data",
                column: "natural_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_spotify_natural_key",
                table: "spotify_data");

            migrationBuilder.DropColumn(
                name: "natural_key",
                table: "spotify_data");
        }
    }
}
