using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenGcalEventVersionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gcal_event_version_gcal_event_gcal_event_id",
                table: "gcal_event_version");

            migrationBuilder.AddColumn<DateTime>(
                name: "gcal_updated_at",
                table: "gcal_event_version",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_recurring_instance",
                table: "gcal_event_version",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recurring_event_id",
                table: "gcal_event_version",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gcal_event_version_gcal_event_gcal_event_id",
                table: "gcal_event_version",
                column: "gcal_event_id",
                principalTable: "gcal_event",
                principalColumn: "gcal_event_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gcal_event_version_gcal_event_gcal_event_id",
                table: "gcal_event_version");

            migrationBuilder.DropColumn(
                name: "gcal_updated_at",
                table: "gcal_event_version");

            migrationBuilder.DropColumn(
                name: "is_recurring_instance",
                table: "gcal_event_version");

            migrationBuilder.DropColumn(
                name: "recurring_event_id",
                table: "gcal_event_version");

            migrationBuilder.AddForeignKey(
                name: "FK_gcal_event_version_gcal_event_gcal_event_id",
                table: "gcal_event_version",
                column: "gcal_event_id",
                principalTable: "gcal_event",
                principalColumn: "gcal_event_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
