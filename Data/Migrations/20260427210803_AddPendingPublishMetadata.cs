using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPublishMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "publish_attempted_at",
                table: "pending_event",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publish_error",
                table: "pending_event",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "publish_attempted_at",
                table: "pending_event");

            migrationBuilder.DropColumn(
                name: "publish_error",
                table: "pending_event");
        }
    }
}
