using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddCiv5SessionPoint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // maps_timeline_raw: entity added to DbContext in a prior work session without a migration
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

        // toggl_phone_rule: classification rules for toggl phone entries
        migrationBuilder.CreateTable(
            name: "toggl_phone_rule",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                date_from = table.Column<DateOnly>(type: "TEXT", nullable: true),
                date_to = table.Column<DateOnly>(type: "TEXT", nullable: true),
                description_pattern = table.Column<string>(type: "TEXT", nullable: false),
                max_duration_minutes = table.Column<int>(type: "INTEGER", nullable: true),
                is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                notes = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_toggl_phone_rule", x => x.id);
            });

        // call_log_import: header record for an imported call log file
        migrationBuilder.CreateTable(
            name: "call_log_import",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                imported_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                file_name = table.Column<string>(type: "TEXT", nullable: false),
                record_count = table.Column<int>(type: "INTEGER", nullable: false),
                date_min = table.Column<DateOnly>(type: "TEXT", nullable: false),
                date_max = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_call_log_import", x => x.id);
            });

        // call_log_entry: individual call records within an import
        migrationBuilder.CreateTable(
            name: "call_log_entry",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                import_id = table.Column<int>(type: "INTEGER", nullable: false),
                call_type = table.Column<string>(type: "TEXT", nullable: false),
                date = table.Column<DateTime>(type: "TEXT", nullable: false),
                duration_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                number = table.Column<string>(type: "TEXT", nullable: true),
                contact = table.Column<string>(type: "TEXT", nullable: true),
                location = table.Column<string>(type: "TEXT", nullable: true),
                service = table.Column<string>(type: "TEXT", nullable: false),
                linked_event_id = table.Column<string>(type: "TEXT", nullable: true),
                linked_event_type = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_call_log_entry", x => x.id);
                table.ForeignKey(
                    name: "FK_call_log_entry_call_log_import_import_id",
                    column: x => x.import_id,
                    principalTable: "call_log_import",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_call_log_entry_date",
            table: "call_log_entry",
            column: "date");

        migrationBuilder.CreateIndex(
            name: "idx_call_log_entry_dedup",
            table: "call_log_entry",
            columns: ["date", "number", "duration_seconds"]);

        migrationBuilder.CreateIndex(
            name: "IX_call_log_entry_import_id",
            table: "call_log_entry",
            column: "import_id");

        // civ5_session_point: save-file scan points for Civilization 5
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
            columns: ["file_modified_at", "game_mode"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_civ5_file_modified_at",
            table: "civ5_session_point",
            column: "file_modified_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "civ5_session_point");
        migrationBuilder.DropTable(name: "call_log_entry");
        migrationBuilder.DropTable(name: "call_log_import");
        migrationBuilder.DropTable(name: "toggl_phone_rule");
        migrationBuilder.DropTable(name: "maps_timeline_raw");
    }
}
