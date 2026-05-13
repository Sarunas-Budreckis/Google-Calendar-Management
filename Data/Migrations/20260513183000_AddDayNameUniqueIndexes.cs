using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations;

public partial class AddDayNameUniqueIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_pending_event_day_name_unique " +
            "ON pending_event (date(start_datetime), source_system) " +
            "WHERE source_system = 'day_name';");

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_gcal_event_day_name_unique " +
            "ON gcal_event (date(start_datetime), source_system) " +
            "WHERE source_system = 'day_name' AND is_deleted = 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_gcal_event_day_name_unique;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pending_event_day_name_unique;");
    }
}
