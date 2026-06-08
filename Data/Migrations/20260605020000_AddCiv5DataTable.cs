using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddCiv5DataTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS civ5_session_point (
                id INTEGER NOT NULL CONSTRAINT PK_civ5_session_point PRIMARY KEY AUTOINCREMENT,
                scanned_at TEXT NOT NULL,
                file_modified_at TEXT NOT NULL,
                game_mode TEXT NOT NULL,
                linked_event_id TEXT NULL,
                linked_event_type TEXT NULL
            )
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS civ5_data (
                id INTEGER NOT NULL CONSTRAINT PK_civ5_data PRIMARY KEY AUTOINCREMENT,
                scanned_at TEXT NOT NULL,
                file_modified_at TEXT NOT NULL,
                game_mode TEXT NOT NULL,
                linked_event_id TEXT NULL,
                linked_event_type TEXT NULL
            )
            """);

        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO civ5_data (
                id,
                scanned_at,
                file_modified_at,
                game_mode,
                linked_event_id,
                linked_event_type)
            SELECT
                id,
                scanned_at,
                file_modified_at,
                game_mode,
                linked_event_id,
                linked_event_type
            FROM civ5_session_point
            """);

        migrationBuilder.DropTable(name: "civ5_session_point");

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS idx_civ5_data_dedup
            ON civ5_data (file_modified_at, game_mode)
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS idx_civ5_data_file_modified_at
            ON civ5_data (file_modified_at)
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS civ5_session_point (
                id INTEGER NOT NULL CONSTRAINT PK_civ5_session_point PRIMARY KEY AUTOINCREMENT,
                scanned_at TEXT NOT NULL,
                file_modified_at TEXT NOT NULL,
                game_mode TEXT NOT NULL,
                linked_event_id TEXT NULL,
                linked_event_type TEXT NULL
            )
            """);

        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO civ5_session_point (
                id,
                scanned_at,
                file_modified_at,
                game_mode,
                linked_event_id,
                linked_event_type)
            SELECT
                id,
                scanned_at,
                file_modified_at,
                game_mode,
                linked_event_id,
                linked_event_type
            FROM civ5_data
            """);

        migrationBuilder.DropTable(name: "civ5_data");

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS idx_civ5_dedup
            ON civ5_session_point (file_modified_at, game_mode)
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS idx_civ5_file_modified_at
            ON civ5_session_point (file_modified_at)
            """);
    }
}
