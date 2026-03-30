using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GoogleCalendarManagement.Data;

/// <summary>
/// Design-time factory for EF Core migrations tooling (dotnet ef migrations add).
/// Required because the startup project is a WinUI 3 WinExe, not a console app.
/// </summary>
public class CalendarDbContextFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CalendarDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_time.db");
        return new CalendarDbContext(optionsBuilder.Options);
    }
}
