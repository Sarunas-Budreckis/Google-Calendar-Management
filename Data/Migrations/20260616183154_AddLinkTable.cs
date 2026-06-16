using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "link",
                columns: table => new
                {
                    link_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    data_point_id = table.Column<int>(type: "INTEGER", nullable: false),
                    event_id = table.Column<string>(type: "TEXT", nullable: true),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    rule_id = table.Column<string>(type: "TEXT", nullable: true),
                    action_group_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link", x => x.link_id);
                    table.CheckConstraint("CK_link_origin", "origin IN ('manual','auto_rule')");
                    table.CheckConstraint("CK_link_state", "state IN ('linked','ignored')");
                    table.ForeignKey(
                        name: "FK_link_data_point_data_point_id",
                        column: x => x.data_point_id,
                        principalTable: "data_point",
                        principalColumn: "data_point_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_link_event_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_link_action_group_id",
                table: "link",
                column: "action_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_link_data_point_id",
                table: "link",
                column: "data_point_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_link_event_id",
                table: "link",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link");
        }
    }
}
