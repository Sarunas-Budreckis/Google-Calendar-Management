using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_point",
                columns: table => new
                {
                    data_point_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    source_ref = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    start_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_point", x => x.data_point_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_data_point_source_key_start_utc",
                table: "data_point",
                columns: new[] { "source_key", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_data_point_start_utc",
                table: "data_point",
                column: "start_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_point");
        }
    }
}
