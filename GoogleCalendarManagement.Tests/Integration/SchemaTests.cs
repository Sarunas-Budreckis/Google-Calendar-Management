using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public class SchemaTests
{
    private static CalendarDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new CalendarDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static async Task<CalendarDbContext> CreateMigratedInMemoryContextAsync()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new CalendarDbContext(options);
        ctx.Database.OpenConnection();
        await ctx.Database.MigrateAsync();
        return ctx;
    }

    [Fact]
    public async Task AllPhase1Tables_ExistAfterEnsureCreated()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        // Assert
        var expectedTables = new[]
        {
            "gcal_event", "gcal_event_version", "save_state",
            "audit_log", "config", "data_source_refresh", "system_state"
        };
        foreach (var table in expectedTables)
            tables.Should().Contain(table, $"table '{table}' should exist after EnsureCreated");
    }

    [Fact]
    public async Task GcalEvent_CrudRoundTrip_Succeeds()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var eventId = "test_event_001";
        var now = DateTime.UtcNow;

        // Act - Insert
        var gcalEvent = new GcalEvent
        {
            GcalEventId = eventId,
            CalendarId = "primary",
            Summary = "Test Event",
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.GcalEvents.Add(gcalEvent);
        await ctx.SaveChangesAsync();

        // Assert - Read
        var fetched = await ctx.GcalEvents.FindAsync(eventId);
        fetched.Should().NotBeNull();
        fetched!.Summary.Should().Be("Test Event");
        fetched.CalendarId.Should().Be("primary");

        // Act - Update
        fetched.Summary = "Updated Event";
        await ctx.SaveChangesAsync();

        // Assert - Update persisted
        var updated = await ctx.GcalEvents.AsNoTracking().FirstAsync(e => e.GcalEventId == eventId);
        updated.Summary.Should().Be("Updated Event");

        // Act - Delete
        ctx.GcalEvents.Remove(fetched);
        await ctx.SaveChangesAsync();

        // Assert - Deleted
        var deleted = await ctx.GcalEvents.FindAsync(eventId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GcalEventVersion_ForeignKey_EnforcedOnInsert()
    {
        // Arrange — requires PRAGMA foreign_keys=ON (real SQLite)
        using var ctx = CreateInMemoryContext();
        var connection = ctx.Database.GetDbConnection();
        using (var enableCmd = connection.CreateCommand())
        {
            enableCmd.CommandText = "PRAGMA foreign_keys=ON;";
            await enableCmd.ExecuteNonQueryAsync();
        }

        var version = new GcalEventVersion
        {
            GcalEventId = "nonexistent_event_id",
            Summary = "Orphan Version",
            CreatedAt = DateTime.UtcNow
        };
        ctx.GcalEventVersions.Add(version);

        // Act & Assert — FK violation should throw
        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("FK constraint must prevent orphan version rows");
    }

    [Fact]
    public async Task Config_SeedData_PresentAfterMigration()
    {
        // Arrange — MigrateAsync runs HasData seed
        using var ctx = await CreateMigratedInMemoryContextAsync();

        // Act
        var configs = await ctx.Configs.ToListAsync();

        // Assert
        configs.Should().HaveCount(6);
        configs.Should().Contain(c => c.ConfigKey == "min_event_duration_minutes" && c.ConfigValue == "5");
        configs.Should().Contain(c => c.ConfigKey == "phone_coalesce_gap_minutes" && c.ConfigValue == "15");
        configs.Should().Contain(c => c.ConfigKey == "youtube_coalesce_gap_minutes" && c.ConfigValue == "30");
        configs.Should().Contain(c => c.ConfigKey == "call_min_duration_minutes" && c.ConfigValue == "3");
        configs.Should().Contain(c => c.ConfigKey == "youtube_char_limit_short" && c.ConfigValue == "40");
        configs.Should().Contain(c => c.ConfigKey == "eight_fifteen_threshold" && c.ConfigValue == "8");
    }

    [Fact]
    public async Task DataSourceRefresh_Migration_AddsSyncTokenColumn()
    {
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('data_source_refresh')";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        columns.Should().Contain("sync_token");
    }

    [Fact]
    public async Task AllIndexes_PresentAfterEnsureCreated()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index'";
        var indexes = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));

        // Assert
        var expectedIndexes = new[]
        {
            "idx_gcal_event_date", "idx_gcal_recurring", "idx_gcal_source", "idx_gcal_app_created",
            "idx_version_event", "idx_audit_timestamp", "idx_audit_operation",
            "idx_refresh_source", "idx_refresh_date"
        };
        foreach (var idx in expectedIndexes)
            indexes.Should().Contain(idx, $"index '{idx}' should exist");
    }
}
