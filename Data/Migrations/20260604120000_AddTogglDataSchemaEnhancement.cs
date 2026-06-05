using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddTogglDataSchemaEnhancement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new columns to toggl_data
        migrationBuilder.AddColumn<string>(
            name: "toggl_data_type",
            table: "toggl_data",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "linked_event_id",
            table: "toggl_data",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "linked_event_type",
            table: "toggl_data",
            type: "TEXT",
            nullable: true);

        // Index on toggl_data_type for efficient per-type queries
        migrationBuilder.CreateIndex(
            name: "idx_toggl_type",
            table: "toggl_data",
            column: "toggl_data_type");

        // Create toggl_sleep_quality table
        migrationBuilder.CreateTable(
            name: "toggl_sleep_quality",
            columns: table => new
            {
                date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                quality = table.Column<int>(type: "INTEGER", nullable: true),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_toggl_sleep_quality", x => x.date);
                table.CheckConstraint("CK_toggl_sleep_quality_quality_range",
                    "quality IS NULL OR (quality >= 0 AND quality <= 10)");
            });

        // Backfill toggl_data_type for existing sleep entries
        migrationBuilder.Sql(
            "UPDATE toggl_data SET toggl_data_type = 'toggl_sleep' " +
            "WHERE lower(description) = 'sleep';");

        // Seed Epic 7 data sources (INSERT OR IGNORE to be idempotent)
        var seedDate = "2026-06-04 00:00:00";
        migrationBuilder.Sql($@"
INSERT OR IGNORE INTO data_source (source_key, display_name, supports_no_data_hint, created_at)
VALUES
    ('toggl_transit',    'Toggl – Driving',              1, '{seedDate}'),
    ('toggl_phone',      'Toggl – Phone',                1, '{seedDate}'),
    ('call_log',         'iOS Call Log',                 0, '{seedDate}'),
    ('maps_timeline',    'Google Maps Timeline',         0, '{seedDate}'),
    ('outlook_calendar', 'Work Calendar (Outlook)',      0, '{seedDate}'),
    ('youtube',          'YouTube Watch History',        0, '{seedDate}'),
    ('spotify',          'Spotify (stats.fm)',           0, '{seedDate}'),
    ('civ5',             'Civilization 5',               0, '{seedDate}'),
    ('comfyui',          'ComfyUI',                      0, '{seedDate}'),
    ('voice_memos',      'Voice Memos',                  0, '{seedDate}'),
    ('chrome_history',   'Chrome Search History',        0, '{seedDate}');
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_toggl_type",
            table: "toggl_data");

        migrationBuilder.DropColumn(
            name: "toggl_data_type",
            table: "toggl_data");

        migrationBuilder.DropColumn(
            name: "linked_event_id",
            table: "toggl_data");

        migrationBuilder.DropColumn(
            name: "linked_event_type",
            table: "toggl_data");

        migrationBuilder.DropTable(
            name: "toggl_sleep_quality");

        // Note: seeded data_source rows are not removed on Down to avoid data loss
    }
}
