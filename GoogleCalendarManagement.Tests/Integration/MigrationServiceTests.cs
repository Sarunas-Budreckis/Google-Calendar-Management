using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

public class MigrationServiceTests
{
    // Creates a CalendarDbContext and MigrationService backed by a real temp-file SQLite database.
    // Caller is responsible for cleanup of tempDir and its contents.
    private static (CalendarDbContext ctx, MigrationService svc, string dbPath, string tempDir) CreateTempFileService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"migration_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "calendar.db");

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var ctx = new CalendarDbContext(options);

        var dbOptions = new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" };
        var svc = new MigrationService(ctx, dbOptions, NullLogger<MigrationService>.Instance);

        return (ctx, svc, dbPath, tempDir);
    }

    private static void CleanupTempDir(string tempDir)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    // AC-1, AC-5
    [Fact]
    public async Task ApplyMigrationsAsync_OnFreshDatabase_AppliesMigrationsSuccessfully()
    {
        var (ctx, svc, _, tempDir) = CreateTempFileService();
        try
        {
            // Act
            await svc.ApplyMigrationsAsync();

            // Assert — migrations were applied
            var applied = (await ctx.Database.GetAppliedMigrationsAsync()).ToList();
            applied.Should().NotBeEmpty("at least one migration should be applied on a fresh database");
            applied.Should().Contain(m => m.Contains("InitialCreate"));
            applied.Should().Contain(m => m.Contains("Phase1Schema"));
            // GetAppliedMigrationsAsync() queries __EFMigrationsHistory — non-empty result confirms the table exists with rows
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // AC-2, AC-5
    [Fact]
    public async Task ApplyMigrationsAsync_WhenPendingMigrations_CreatesBackupFile()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            // Arrange — create an empty calendar.db so File.Copy has a source
            await File.WriteAllBytesAsync(dbPath, Array.Empty<byte>());

            // Act
            await svc.ApplyMigrationsAsync();

            // Assert — a backup file with the correct naming pattern was created
            var backupFiles = Directory.GetFiles(tempDir, "calendar_backup_*_pre-migration.db");
            backupFiles.Should().HaveCount(1, "one pre-migration backup should be created");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // AC-3
    [Fact]
    public async Task ApplyMigrationsAsync_AfterMigration_DatabaseSchemaVersionRowPresent()
    {
        var (ctx, svc, _, tempDir) = CreateTempFileService();
        try
        {
            // Act
            await svc.ApplyMigrationsAsync();

            // Assert — system_state has a DatabaseSchemaVersion row
            var row = await ctx.SystemStates.SingleOrDefaultAsync(s => s.StateName == "DatabaseSchemaVersion");
            row.Should().NotBeNull("DatabaseSchemaVersion row must be upserted after migration");
            row!.StateValue.Should().NotBeNullOrEmpty("StateValue must be the name of the last applied migration");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // AC-4
    [Fact]
    public async Task CheckDatabaseIntegrityAsync_OnHealthyDatabase_ReturnsTrue()
    {
        var (ctx, svc, _, tempDir) = CreateTempFileService();
        try
        {
            // Arrange — apply migrations so the database exists and has a valid schema
            await ctx.Database.MigrateAsync();

            // Act
            var result = await svc.CheckDatabaseIntegrityAsync();

            // Assert
            result.Should().BeTrue("a freshly migrated database should pass integrity_check");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // AC-2 — backup cleanup: only 5 most recent kept
    [Fact]
    public async Task CreateBackupAsync_WhenMoreThanFiveBackupsExist_DeletesOldest()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            // Arrange — create a real calendar.db and 5 pre-existing backup files with distinct timestamps
            await File.WriteAllBytesAsync(dbPath, Array.Empty<byte>());
            var preExistingFiles = new List<string>();
            for (int i = 1; i <= 5; i++)
            {
                var fakeName = Path.Combine(tempDir, $"calendar_backup_2026010{i}_120000_old.db");
                await File.WriteAllBytesAsync(fakeName, Array.Empty<byte>());
                // Set creation time to distinguish order (oldest first)
                File.SetCreationTimeUtc(fakeName, new DateTime(2026, 1, i, 12, 0, 0, DateTimeKind.Utc));
                preExistingFiles.Add(fakeName);
            }
            var oldestFile = preExistingFiles.First(); // 2026-01-01 — will be deleted

            // Act — creates a 6th backup; cleanup should remove the oldest
            await svc.CreateBackupAsync("test");

            // Assert — exactly 5 backup files remain (1 oldest deleted, 1 new created)
            var remaining = Directory.GetFiles(tempDir, "calendar_backup_*.db");
            remaining.Should().HaveCount(5, "only 5 most recent backups should be kept");
            remaining.Should().NotContain(oldestFile, "the oldest backup should have been deleted");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // AC-1 — no backup created when no pending migrations
    [Fact]
    public async Task ApplyMigrationsAsync_WhenNoMigrationsPending_DoesNotCreateBackup()
    {
        var (ctx, svc, _, tempDir) = CreateTempFileService();
        try
        {
            // Arrange — apply all migrations first (no pending on second call)
            await ctx.Database.MigrateAsync();

            // Act — second call should find no pending migrations
            await svc.ApplyMigrationsAsync();

            // Assert — no backup files were created (no pending migrations means no backup)
            var backupFiles = Directory.GetFiles(tempDir, "calendar_backup_*.db");
            backupFiles.Should().BeEmpty("no backup should be created when there are no pending migrations");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }
}
