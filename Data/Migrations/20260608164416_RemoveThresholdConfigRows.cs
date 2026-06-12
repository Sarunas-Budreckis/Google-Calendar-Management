using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveThresholdConfigRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "call_min_duration_minutes");

            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "eight_fifteen_threshold");

            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "min_event_duration_minutes");

            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "phone_coalesce_gap_minutes");

            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "youtube_char_limit_short");

            migrationBuilder.DeleteData(
                table: "config",
                keyColumn: "config_key",
                keyValue: "youtube_coalesce_gap_minutes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
