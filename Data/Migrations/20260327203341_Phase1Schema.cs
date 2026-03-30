using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    log_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    operation_type = table.Column<string>(type: "TEXT", nullable: false),
                    operation_details = table.Column<string>(type: "TEXT", nullable: true),
                    affected_dates = table.Column<string>(type: "TEXT", nullable: true),
                    affected_events = table.Column<string>(type: "TEXT", nullable: true),
                    user_action = table.Column<bool>(type: "INTEGER", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "config",
                columns: table => new
                {
                    config_key = table.Column<string>(type: "TEXT", nullable: false),
                    config_value = table.Column<string>(type: "TEXT", nullable: true),
                    config_type = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config", x => x.config_key);
                });

            migrationBuilder.CreateTable(
                name: "data_source_refresh",
                columns: table => new
                {
                    refresh_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_name = table.Column<string>(type: "TEXT", nullable: false),
                    start_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    end_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_refreshed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    records_fetched = table.Column<int>(type: "INTEGER", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source_refresh", x => x.refresh_id);
                });

            migrationBuilder.CreateTable(
                name: "gcal_event",
                columns: table => new
                {
                    gcal_event_id = table.Column<string>(type: "TEXT", nullable: false),
                    calendar_id = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    start_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    end_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_all_day = table.Column<bool>(type: "INTEGER", nullable: true),
                    color_id = table.Column<string>(type: "TEXT", nullable: true),
                    gcal_etag = table.Column<string>(type: "TEXT", nullable: true),
                    gcal_updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    app_created = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    source_system = table.Column<string>(type: "TEXT", nullable: true),
                    app_published = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    app_published_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    app_last_modified_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    recurring_event_id = table.Column<string>(type: "TEXT", nullable: true),
                    is_recurring_instance = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    last_synced_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gcal_event", x => x.gcal_event_id);
                });

            migrationBuilder.CreateTable(
                name: "save_state",
                columns: table => new
                {
                    save_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    save_name = table.Column<string>(type: "TEXT", nullable: false),
                    save_description = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    snapshot_data = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_save_state", x => x.save_id);
                });

            migrationBuilder.CreateTable(
                name: "system_state",
                columns: table => new
                {
                    state_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    state_name = table.Column<string>(type: "TEXT", nullable: true),
                    state_value = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_state", x => x.state_id);
                });

            migrationBuilder.CreateTable(
                name: "gcal_event_version",
                columns: table => new
                {
                    version_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    gcal_event_id = table.Column<string>(type: "TEXT", nullable: false),
                    gcal_etag = table.Column<string>(type: "TEXT", nullable: true),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    start_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    end_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_all_day = table.Column<bool>(type: "INTEGER", nullable: true),
                    color_id = table.Column<string>(type: "TEXT", nullable: true),
                    changed_by = table.Column<string>(type: "TEXT", nullable: true),
                    change_reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gcal_event_version", x => x.version_id);
                    table.ForeignKey(
                        name: "FK_gcal_event_version_gcal_event_gcal_event_id",
                        column: x => x.gcal_event_id,
                        principalTable: "gcal_event",
                        principalColumn: "gcal_event_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "config",
                columns: new[] { "config_key", "config_type", "config_value", "description", "updated_at" },
                values: new object[,]
                {
                    { "call_min_duration_minutes", "integer", "3", "Minimum call duration to import", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "eight_fifteen_threshold", "integer", "8", "Minutes required in 15-min block", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "min_event_duration_minutes", "integer", "5", "Minimum duration to show events", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "phone_coalesce_gap_minutes", "integer", "15", "Max gap for phone coalescing", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "youtube_char_limit_short", "integer", "40", "Char limit for events <90min", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "youtube_coalesce_gap_minutes", "integer", "30", "Gap after video duration for YouTube", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_operation",
                table: "audit_log",
                column: "operation_type");

            migrationBuilder.CreateIndex(
                name: "idx_audit_timestamp",
                table: "audit_log",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_date",
                table: "data_source_refresh",
                columns: new[] { "source_name", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "idx_refresh_source",
                table: "data_source_refresh",
                columns: new[] { "source_name", "last_refreshed_at" });

            migrationBuilder.CreateIndex(
                name: "idx_gcal_app_created",
                table: "gcal_event",
                column: "app_created");

            migrationBuilder.CreateIndex(
                name: "idx_gcal_event_date",
                table: "gcal_event",
                columns: new[] { "start_datetime", "end_datetime" });

            migrationBuilder.CreateIndex(
                name: "idx_gcal_recurring",
                table: "gcal_event",
                column: "recurring_event_id");

            migrationBuilder.CreateIndex(
                name: "idx_gcal_source",
                table: "gcal_event",
                column: "source_system");

            migrationBuilder.CreateIndex(
                name: "idx_version_event",
                table: "gcal_event_version",
                columns: new[] { "gcal_event_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_system_state_state_name",
                table: "system_state",
                column: "state_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "config");

            migrationBuilder.DropTable(
                name: "data_source_refresh");

            migrationBuilder.DropTable(
                name: "gcal_event_version");

            migrationBuilder.DropTable(
                name: "save_state");

            migrationBuilder.DropTable(
                name: "system_state");

            migrationBuilder.DropTable(
                name: "gcal_event");
        }
    }
}
