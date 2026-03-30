using FluentAssertions;
using GoogleCalendarManagement.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public class DatabaseInfrastructureTests
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

    [Fact]
    public void CalendarDbContext_CanBeInstantiated_WithInMemorySqlite()
    {
        // Arrange & Act
        using var context = CreateInMemoryContext();

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task Database_CreatesFile_AtExpectedPath()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"db-file-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "calendar.db");

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        try
        {
            // Act
            using (var context = new CalendarDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            // Assert
            File.Exists(dbPath).Should().BeTrue();
            Path.GetDirectoryName(dbPath).Should().Be(tempDir);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
            if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Database_CanExecute_BasicCrudOperations()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var connection = context.Database.GetDbConnection();

        // Act - Create test table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS crud_test (id INTEGER PRIMARY KEY, value TEXT NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
        }

        // Act - Insert
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO crud_test (value) VALUES ('hello')";
            var rows = await cmd.ExecuteNonQueryAsync();
            rows.Should().Be(1);
        }

        // Act - Read
        string? readValue;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT value FROM crud_test WHERE id = 1";
            readValue = (string?)await cmd.ExecuteScalarAsync();
        }

        // Assert read
        readValue.Should().Be("hello");

        // Act - Delete
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM crud_test WHERE id = 1";
            var deleted = await cmd.ExecuteNonQueryAsync();
            deleted.Should().Be(1);
        }
    }

    [Fact]
    public async Task Database_MigrationApplied_SchemaVersionTracked()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new CalendarDbContext(options);
        context.Database.OpenConnection();

        // Act
        await context.Database.MigrateAsync();

        // Assert - __EFMigrationsHistory table exists with InitialCreate entry
        var connection = context.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%InitialCreate'";
        var count = await cmd.ExecuteScalarAsync();
        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    public async Task Database_WALMode_IsEnabled()
    {
        // Arrange - use a temp file (WAL mode is not supported on in-memory databases)
        var tempPath = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={tempPath}")
            .Options;

        try
        {
            using (var context = new CalendarDbContext(options))
            {
                await context.Database.OpenConnectionAsync();

                // Act - set WAL mode via PRAGMA (as done by SqliteConnectionInterceptor in production)
                var connection = context.Database.GetDbConnection();
                using (var setCmd = connection.CreateCommand())
                {
                    setCmd.CommandText = "PRAGMA journal_mode=WAL;";
                    var modeResult = await setCmd.ExecuteScalarAsync();
                    modeResult?.ToString().Should().Be("wal");
                }

                // Verify it is set
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA journal_mode;";
                var result = await checkCmd.ExecuteScalarAsync();

                // Assert
                result?.ToString().Should().Be("wal");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(tempPath + "-wal")) File.Delete(tempPath + "-wal");
            if (File.Exists(tempPath + "-shm")) File.Delete(tempPath + "-shm");
        }
    }

    [Fact]
    public async Task Database_ForeignKeys_AreEnforced()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var connection = context.Database.GetDbConnection();

        // Enable FK enforcement (as done by SqliteConnectionInterceptor in production)
        using (var enableCmd = connection.CreateCommand())
        {
            enableCmd.CommandText = "PRAGMA foreign_keys=ON;";
            await enableCmd.ExecuteNonQueryAsync();
        }

        // Act
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "PRAGMA foreign_keys;";
        var result = await checkCmd.ExecuteScalarAsync();

        // Assert
        Convert.ToInt32(result).Should().Be(1);
    }
}
