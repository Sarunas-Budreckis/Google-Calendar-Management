using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEventOperationTypeAndDeletionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "operation_type",
                table: "pending_event",
                type: "TEXT",
                nullable: false,
                defaultValue: "edit");

            migrationBuilder.CreateTable(
                name: "deleted_event",
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
                    recurring_event_id = table.Column<string>(type: "TEXT", nullable: true),
                    is_recurring_instance = table.Column<bool>(type: "INTEGER", nullable: true),
                    app_created = table.Column<bool>(type: "INTEGER", nullable: true),
                    source_system = table.Column<string>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    deletion_source = table.Column<string>(type: "TEXT", nullable: false),
                    original_created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    original_updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deleted_event", x => x.gcal_event_id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_event_series",
                columns: table => new
                {
                    series_id = table.Column<string>(type: "TEXT", nullable: false),
                    calendar_id = table.Column<string>(type: "TEXT", nullable: false),
                    recurrence = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    color_id = table.Column<string>(type: "TEXT", nullable: true),
                    is_all_day = table.Column<bool>(type: "INTEGER", nullable: true),
                    series_start_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    series_end_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    gcal_etag = table.Column<string>(type: "TEXT", nullable: true),
                    gcal_updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_event_series", x => x.series_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_deleted_event_date",
                table: "deleted_event",
                column: "start_datetime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deleted_event");

            migrationBuilder.DropTable(
                name: "recurring_event_series");

            migrationBuilder.DropColumn(
                name: "operation_type",
                table: "pending_event");
        }
    }
}
