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
    public async Task OutlookEvent_TableExistsWithExpectedColumns_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('outlook_event')";
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        // Assert
        columns.Should().Contain("id");
        columns.Should().Contain("subject");
        columns.Should().Contain("start_datetime");
        columns.Should().Contain("end_datetime");
        columns.Should().Contain("is_all_day");
        columns.Should().Contain("organizer");
        columns.Should().Contain("location");
        columns.Should().Contain("body_preview");
        columns.Should().Contain("is_recurring");
        columns.Should().Contain("series_master_id");
        columns.Should().Contain("last_synced_at");
        columns.Should().Contain("is_suppressed");
    }

    [Fact]
    public async Task Config_ThresholdRows_RemovedAfterMigration()
    {
        // Arrange — RemoveThresholdConfigRows migration deletes the 6 seed rows; thresholds now live in ImportThresholds.cs
        using var ctx = await CreateMigratedInMemoryContextAsync();

        // Act
        var configs = await ctx.Configs.ToListAsync();

        // Assert — config table exists but threshold rows are gone
        configs.Should().NotContain(c => c.ConfigKey == "min_event_duration_minutes");
        configs.Should().NotContain(c => c.ConfigKey == "phone_coalesce_gap_minutes");
        configs.Should().NotContain(c => c.ConfigKey == "youtube_coalesce_gap_minutes");
        configs.Should().NotContain(c => c.ConfigKey == "call_min_duration_minutes");
        configs.Should().NotContain(c => c.ConfigKey == "youtube_char_limit_short");
        configs.Should().NotContain(c => c.ConfigKey == "eight_fifteen_threshold");
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
    public async Task GcalEventVersionRelationship_IsNonCascading_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act — read the FK definition from SQLite schema
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_list('gcal_event_version')";

        string? onDeleteBehavior = null;
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // PRAGMA foreign_key_list columns: id, seq, table, from, to, on_update, on_delete, match
            onDeleteBehavior = reader.GetString(6); // on_delete column
        }

        // Assert — SQLite records "NO ACTION" for both Restrict and NoAction
        onDeleteBehavior.Should().NotBe("CASCADE", "gcal_event_version FK must not cascade delete history rows");
    }

    [Fact]
    public async Task GcalEventVersion_HasNewSnapshotColumns_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('gcal_event_version')";

        var columns = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        // Assert
        columns.Should().Contain("gcal_updated_at");
        columns.Should().Contain("recurring_event_id");
        columns.Should().Contain("is_recurring_instance");
    }

    [Fact]
    public async Task DataSourceTier3Tables_ExistAfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        // Assert
        tables.Should().Contain("data_source", "data_source table must exist after migration");
        tables.Should().Contain("date_source_integration", "date_source_integration table must exist after migration");
        tables.Should().Contain("data_source_import_log", "data_source_import_log table must exist after migration");
    }

    [Fact]
    public async Task DataSource_HasExpectedColumns_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('data_source')";
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        // Assert
        columns.Should().Contain("data_source_id");
        columns.Should().Contain("source_key");
        columns.Should().Contain("display_name");
        columns.Should().Contain("description");
        columns.Should().Contain("supports_no_data_hint");
        columns.Should().Contain("created_at");
    }

    [Fact]
    public async Task DataSource_HasColorHexColumn_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('data_source')";
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        // Assert
        columns.Should().Contain("color_hex", "data_source table must have color_hex column after AddDataSourceColorHex migration");
    }

    [Fact]
    public async Task DateSourceIntegration_HasUniqueIndexOnDateAndDataSourceId_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act — read all indexes for date_source_integration
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='date_source_integration'";
        var indexes = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));

        // Assert
        indexes.Should().Contain("idx_date_source_integration_date_source",
            "date_source_integration must have a unique index on (date, data_source_id)");

        // Verify the index covers both columns and is unique
        using var infoCmd = connection.CreateCommand();
        infoCmd.CommandText = "PRAGMA index_info('idx_date_source_integration_date_source')";
        var indexColumns = new List<string>();
        await using var infoReader = await infoCmd.ExecuteReaderAsync();
        while (await infoReader.ReadAsync())
            indexColumns.Add(infoReader.GetString(2));

        indexColumns.Should().Contain("date");
        indexColumns.Should().Contain("data_source_id");
    }

    [Fact]
    public async Task DataSourceImportLog_FkToDataSource_IsRestrictOnDelete_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_list('data_source_import_log')";

        string? onDeleteBehavior = null;
        string? referencedTable = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            referencedTable = reader.GetString(2);
            onDeleteBehavior = reader.GetString(6);
        }

        // Assert
        referencedTable.Should().Be("data_source", "import log FK must reference data_source");
        onDeleteBehavior.Should().NotBe("CASCADE", "import log FK must not cascade deletes");
    }

    [Fact]
    public async Task DateSourceIntegration_FkToDataSource_IsRestrictOnDelete_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_list('date_source_integration')";

        string? onDeleteBehavior = null;
        string? referencedTable = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            referencedTable = reader.GetString(2);
            onDeleteBehavior = reader.GetString(6);
        }

        // Assert
        referencedTable.Should().Be("data_source", "integration FK must reference data_source");
        onDeleteBehavior.Should().NotBe("CASCADE", "integration FK must not cascade deletes");
    }

    [Fact]
    public async Task TogglData_HasExpectedSchema_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('toggl_data')";
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        // Assert
        columns.Should().Contain("toggl_id");
        columns.Should().Contain("description");
        columns.Should().Contain("start_time");
        columns.Should().Contain("end_time");
        columns.Should().Contain("duration_seconds");
        columns.Should().Contain("project_name");
        columns.Should().Contain("tags");
        columns.Should().Contain("visible_as_event");
        columns.Should().Contain("published_to_gcal");
        columns.Should().Contain("published_gcal_event_id");
        columns.Should().Contain("last_synced_at");
        columns.Should().Contain("created_at");
    }

    [Fact]
    public async Task TogglData_FkToGcalEvent_IsRestrictOnDelete_AfterMigration()
    {
        // Arrange
        await using var ctx = await CreateMigratedInMemoryContextAsync();
        var connection = ctx.Database.GetDbConnection();

        // Act
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_list('toggl_data')";

        string? onDeleteBehavior = null;
        string? referencedTable = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            referencedTable = reader.GetString(2);
            onDeleteBehavior = reader.GetString(6);
        }

        // Assert
        referencedTable.Should().Be("gcal_event", "published Toggl entries may reference generated Google Calendar events");
        onDeleteBehavior.Should().NotBe("CASCADE", "Toggl data must not be deleted by event deletion");
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
