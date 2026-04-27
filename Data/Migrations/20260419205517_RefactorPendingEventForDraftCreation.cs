using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPendingEventForDraftCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_event_v2",
                columns: table => new
                {
                    pending_event_id = table.Column<string>(type: "TEXT", nullable: false),
                    gcal_event_id = table.Column<string>(type: "TEXT", nullable: true),
                    calendar_id = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    start_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    end_datetime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_all_day = table.Column<bool>(type: "INTEGER", nullable: true),
                    color_id = table.Column<string>(type: "TEXT", nullable: true),
                    app_created = table.Column<bool>(type: "INTEGER", nullable: false),
                    source_system = table.Column<string>(type: "TEXT", nullable: true),
                    ready_to_publish = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_event_v2", x => x.pending_event_id);
                    table.ForeignKey(
                        name: "FK_pending_event_v2_gcal_event_gcal_event_id",
                        column: x => x.gcal_event_id,
                        principalTable: "gcal_event",
                        principalColumn: "gcal_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO pending_event_v2 (
                    pending_event_id,
                    gcal_event_id,
                    calendar_id,
                    summary,
                    description,
                    start_datetime,
                    end_datetime,
                    is_all_day,
                    color_id,
                    app_created,
                    source_system,
                    ready_to_publish,
                    created_at,
                    updated_at
                )
                SELECT
                    'pending_' || replace(id, '-', ''),
                    gcal_event_id,
                    'primary',
                    summary,
                    description,
                    start_datetime,
                    end_datetime,
                    NULL,
                    color_id,
                    0,
                    'google-overlay',
                    0,
                    created_at,
                    updated_at
                FROM pending_event;
                """);

            migrationBuilder.DropTable(
                name: "pending_event");

            migrationBuilder.RenameTable(
                name: "pending_event_v2",
                newName: "pending_event");

            migrationBuilder.CreateIndex(
                name: "idx_pending_event_date",
                table: "pending_event",
                columns: new[] { "start_datetime", "end_datetime" });

            migrationBuilder.CreateIndex(
                name: "idx_pending_event_gcal_event_id",
                table: "pending_event",
                column: "gcal_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_event_old",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    gcal_event_id = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    start_datetime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_datetime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    color_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_event_old", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_event_old_gcal_event_gcal_event_id",
                        column: x => x.gcal_event_id,
                        principalTable: "gcal_event",
                        principalColumn: "gcal_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO pending_event_old (
                    id,
                    gcal_event_id,
                    summary,
                    description,
                    start_datetime,
                    end_datetime,
                    color_id,
                    created_at,
                    updated_at
                )
                SELECT
                    substr(pending_event_id, 9),
                    gcal_event_id,
                    coalesce(summary, ''),
                    description,
                    coalesce(start_datetime, created_at),
                    coalesce(end_datetime, updated_at),
                    coalesce(color_id, 'azure'),
                    created_at,
                    updated_at
                FROM pending_event
                WHERE gcal_event_id IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "pending_event");

            migrationBuilder.RenameTable(
                name: "pending_event_old",
                newName: "pending_event");

            migrationBuilder.CreateIndex(
                name: "idx_pending_event_gcal_event_id",
                table: "pending_event",
                column: "gcal_event_id",
                unique: true);
        }
    }
}
