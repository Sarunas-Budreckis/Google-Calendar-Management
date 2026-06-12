using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddOutlookEvent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""outlook_event"" (
    ""id""               TEXT    NOT NULL PRIMARY KEY,
    ""subject""          TEXT    NOT NULL,
    ""start_datetime""   TEXT    NOT NULL,
    ""end_datetime""     TEXT    NOT NULL,
    ""is_all_day""       INTEGER NOT NULL DEFAULT 0,
    ""organizer""        TEXT    NULL,
    ""location""         TEXT    NULL,
    ""body_preview""     TEXT    NULL,
    ""is_recurring""     INTEGER NOT NULL DEFAULT 0,
    ""series_master_id"" TEXT    NULL,
    ""last_synced_at""   TEXT    NOT NULL,
    ""is_suppressed""    INTEGER NOT NULL DEFAULT 0
)");

        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""idx_outlook_event_start"" ON ""outlook_event"" (""start_datetime"")");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""idx_outlook_event_suppressed"" ON ""outlook_event"" (""is_suppressed"")");

        migrationBuilder.Sql(@"INSERT OR IGNORE INTO data_source (source_key, display_name, description, supports_no_data_hint, created_at)
VALUES ('outlook', 'Outlook Work Calendar', 'Mayo Clinic Outlook work calendar events via Microsoft Graph API', 0, '2026-06-08 00:00:00')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""idx_outlook_event_suppressed""");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""idx_outlook_event_start""");
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""outlook_event""");
        migrationBuilder.Sql(@"DELETE FROM data_source WHERE source_key = 'outlook'");
    }
}
