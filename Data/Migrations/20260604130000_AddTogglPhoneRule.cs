using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddTogglPhoneRule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "toggl_phone_rule",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                date_from = table.Column<DateOnly>(type: "TEXT", nullable: true),
                date_to = table.Column<DateOnly>(type: "TEXT", nullable: true),
                description_pattern = table.Column<string>(type: "TEXT", nullable: false),
                max_duration_minutes = table.Column<int>(type: "INTEGER", nullable: true),
                is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                notes = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_toggl_phone_rule", x => x.id);
            });

        // Seed default phone classification rules (equivalent to the 7.4 hardcoded list)
        migrationBuilder.Sql(@"
INSERT INTO toggl_phone_rule (description_pattern, max_duration_minutes, is_active, notes)
VALUES
    ('ToDelete',          10, 1, 'iOS Shortcut early naming'),
    ('Phone',             10, 1, 'Generic phone tracking'),
    ('Phone - Reddit',    10, 1, 'Reddit session tracking'),
    ('Phone - Instagram', 10, 1, 'Instagram session tracking');
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "toggl_phone_rule");
    }
}
