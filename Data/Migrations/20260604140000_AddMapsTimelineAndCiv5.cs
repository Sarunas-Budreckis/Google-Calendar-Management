using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddMapsTimelineAndCiv5 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "maps_timeline_raw",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                imported_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                file_name = table.Column<string>(type: "TEXT", nullable: false),
                file_size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                covered_date_min = table.Column<DateOnly>(type: "TEXT", nullable: true),
                covered_date_max = table.Column<DateOnly>(type: "TEXT", nullable: true),
                raw_json = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_maps_timeline_raw", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "civ5_session_point",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                scanned_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                file_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                game_mode = table.Column<string>(type: "TEXT", nullable: false),
                linked_event_id = table.Column<string>(type: "TEXT", nullable: true),
                linked_event_type = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_civ5_session_point", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_civ5_dedup",
            table: "civ5_session_point",
            columns: new[] { "file_modified_at", "game_mode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_civ5_file_modified_at",
            table: "civ5_session_point",
            column: "file_modified_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "maps_timeline_raw");
        migrationBuilder.DropTable(name: "civ5_session_point");
    }
}
