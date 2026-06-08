using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class UpdateComfyUIDataEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "comfyui_data",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                scanned_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                event_type = table.Column<string>(type: "TEXT", nullable: false),
                linked_event_id = table.Column<string>(type: "TEXT", nullable: true),
                linked_event_type = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_comfyui_data", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_comfyui_data_dedup",
            table: "comfyui_data",
            columns: ["timestamp", "event_type"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_comfyui_data_timestamp",
            table: "comfyui_data",
            column: "timestamp");

        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO comfyui_data (scanned_at, timestamp, event_type, linked_event_id, linked_event_type)
            SELECT scanned_at, file_created_at, 'created', linked_event_id, linked_event_type
            FROM comfyui_scan_point
            WHERE file_created_at IS NOT NULL
            """);

        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO comfyui_data (scanned_at, timestamp, event_type, linked_event_id, linked_event_type)
            SELECT scanned_at, file_modified_at, 'modified', linked_event_id, linked_event_type
            FROM comfyui_scan_point
            """);

        migrationBuilder.DropTable(name: "comfyui_scan_point");

        migrationBuilder.Sql("""
            UPDATE data_source
            SET source_key = 'comfyui_data'
            WHERE source_key = 'comfyui'
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "comfyui_scan_point",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                scanned_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                file_created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                file_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                linked_event_id = table.Column<string>(type: "TEXT", nullable: true),
                linked_event_type = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_comfyui_scan_point", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_comfyui_scan_point_dedup",
            table: "comfyui_scan_point",
            column: "file_modified_at",
            unique: true);

        migrationBuilder.Sql("""
            INSERT OR IGNORE INTO comfyui_scan_point (scanned_at, file_created_at, file_modified_at, linked_event_id, linked_event_type)
            SELECT m.scanned_at, c.timestamp, m.timestamp, m.linked_event_id, m.linked_event_type
            FROM comfyui_data m
            LEFT JOIN comfyui_data c
                ON c.timestamp = m.timestamp
                AND c.event_type = 'created'
            WHERE m.event_type = 'modified'
            """);

        migrationBuilder.DropTable(name: "comfyui_data");

        migrationBuilder.Sql("""
            UPDATE data_source
            SET source_key = 'comfyui'
            WHERE source_key = 'comfyui_data'
            """);
    }
}
