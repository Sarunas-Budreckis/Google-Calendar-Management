using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceTier3Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_source",
                columns: table => new
                {
                    data_source_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    supports_no_data_hint = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source", x => x.data_source_id);
                });

            migrationBuilder.CreateTable(
                name: "data_source_import_log",
                columns: table => new
                {
                    import_log_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data_source_id = table.Column<int>(type: "INTEGER", nullable: false),
                    covered_start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    covered_end_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    imported_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    records_fetched = table.Column<int>(type: "INTEGER", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source_import_log", x => x.import_log_id);
                    table.ForeignKey(
                        name: "FK_data_source_import_log_data_source_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_source",
                        principalColumn: "data_source_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "date_source_integration",
                columns: table => new
                {
                    integration_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    data_source_id = table.Column<int>(type: "INTEGER", nullable: false),
                    integrated = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    integrated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_date_source_integration", x => x.integration_id);
                    table.ForeignKey(
                        name: "FK_date_source_integration_data_source_data_source_id",
                        column: x => x.data_source_id,
                        principalTable: "data_source",
                        principalColumn: "data_source_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_data_source_key",
                table: "data_source",
                column: "source_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_source_import_log_data_source_id",
                table: "data_source_import_log",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "idx_date_source_integration_date_source",
                table: "date_source_integration",
                columns: new[] { "date", "data_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_date_source_integration_data_source_id",
                table: "date_source_integration",
                column: "data_source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_source_import_log");

            migrationBuilder.DropTable(
                name: "date_source_integration");

            migrationBuilder.DropTable(
                name: "data_source");
        }
    }
}
