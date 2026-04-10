using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Data;

public class CalendarDbContext : DbContext
{
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options)
        : base(options) { }

    public DbSet<GcalEvent> GcalEvents { get; set; }
    public DbSet<PendingEvent> PendingEvents { get; set; }
    public DbSet<GcalEventVersion> GcalEventVersions { get; set; }
    public DbSet<SaveState> SaveStates { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<DataSourceRefresh> DataSourceRefreshes { get; set; }
    public DbSet<SystemState> SystemStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
