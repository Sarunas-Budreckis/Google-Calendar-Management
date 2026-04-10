using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEventTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_pending_event", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_event_gcal_event_gcal_event_id",
                        column: x => x.gcal_event_id,
                        principalTable: "gcal_event",
                        principalColumn: "gcal_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_pending_event_gcal_event_id",
                table: "pending_event",
                column: "gcal_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_event");
        }
    }
}
