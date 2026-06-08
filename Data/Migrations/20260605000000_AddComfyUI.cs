using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddComfyUI : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "comfyui_folder",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                folder_path = table.Column<string>(type: "TEXT", nullable: false),
                is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                added_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_comfyui_folder", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_comfyui_folder_path",
            table: "comfyui_folder",
            column: "folder_path",
            unique: true);

        migrationBuilder.CreateTable(
            name: "comfyui_scan_point",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                scanned_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                file_created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                file_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                linked_event_id = table.Column<string>(type: "TEXT", nullable: true),
                linked_event_type = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_comfyui_scan_point", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_comfyui_scan_point_dedup",
            table: "comfyui_scan_point",
            column: "file_modified_at",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "comfyui_scan_point");
        migrationBuilder.DropTable(name: "comfyui_folder");
    }
}
