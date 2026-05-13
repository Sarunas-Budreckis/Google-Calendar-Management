using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTogglDataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "toggl_data",
                columns: table => new
                {
                    toggl_id = table.Column<long>(type: "INTEGER", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    duration_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    project_name = table.Column<string>(type: "TEXT", nullable: true),
                    tags = table.Column<string>(type: "TEXT", nullable: true),
                    visible_as_event = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    published_to_gcal = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    published_gcal_event_id = table.Column<string>(type: "TEXT", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toggl_data", x => x.toggl_id);
                    table.ForeignKey(
                        name: "FK_toggl_data_gcal_event_published_gcal_event_id",
                        column: x => x.published_gcal_event_id,
                        principalTable: "gcal_event",
                        principalColumn: "gcal_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_toggl_date",
                table: "toggl_data",
                columns: new[] { "start_time", "end_time" });

            migrationBuilder.CreateIndex(
                name: "idx_toggl_description",
                table: "toggl_data",
                column: "description");

            migrationBuilder.CreateIndex(
                name: "IX_toggl_data_published_gcal_event_id",
                table: "toggl_data",
                column: "published_gcal_event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "toggl_data");
        }
    }
}
