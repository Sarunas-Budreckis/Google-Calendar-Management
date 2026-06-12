using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedEventEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_id",
                table: "deleted_event",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE deleted_event
                SET event_id = (
                    SELECT event.event_id
                    FROM event
                    WHERE event.gcal_event_id = deleted_event.gcal_event_id
                )
                WHERE event_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "event_id",
                table: "deleted_event");
        }
    }
}
