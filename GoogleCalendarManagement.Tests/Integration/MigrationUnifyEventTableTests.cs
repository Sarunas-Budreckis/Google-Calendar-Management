using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

/// <summary>
/// Story 8.2 — validates the UnifyEventTable migration's data-preserving transform. Each test seeds
/// the OLD schema (by migrating to the migration immediately before UnifyEventTable, then inserting
/// raw rows into gcal_event/pending_event/etc.), runs the migration, and asserts the unified `event`
/// table and repointed references are correct.
/// </summary>
public class MigrationUnifyEventTableTests
{
    private const string PreMigration = "AddDataSourceColorHex";
    private const string TestTime = "2026-06-01 12:00:00";

    private static (CalendarDbContext ctx, MigrationService svc, string dbPath, string tempDir) CreateTempFileService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"unify_migration_test_{Guid.NewGuid():N}");
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

    // Migrates a fresh DB to the pre-UnifyEventTable schema so the old tables exist for seeding.
    private static async Task MigrateToOldSchemaAsync(CalendarDbContext ctx)
    {
        var migrator = ctx.GetService<IMigrator>();
        await migrator.MigrateAsync(PreMigration);
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    // ---- AC #11(a): every gcal_event row appears exactly once in event (approved/published) ----
    [Fact]
    public async Task Migration_MovesEveryGcalEventIntoEventExactlyOnce()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','Meeting A','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}'),
                                     ('g2','primary','Meeting B','2026-06-02 09:00:00','2026-06-02 10:00:00','{TestTime}','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event;")).Should().Be(2);
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event WHERE gcal_event_id = 'g1' AND lifecycle = 'approved' AND publish = 'published' AND has_unpublished_changes = 0;"))
                .Should().Be(1, "each gcal_event becomes exactly one approved/published event");
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event WHERE gcal_event_id = 'g2';")).Should().Be(1);
            Scalar(assertConn, "SELECT summary FROM event WHERE gcal_event_id = 'g1';").Should().Be("Meeting A");
            // event_id is a freshly minted local id, not the gcal id
            Scalar(assertConn, "SELECT event_id FROM event WHERE gcal_event_id = 'g1';").Should().NotBe("g1");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(b): overlay pending_event sets has_unpublished_changes=1, no duplicate row ----
    [Fact]
    public async Task Migration_MergesOverlayPendingEvent_NoDuplicate_AndAppliesEdits()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','Original Title','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}');");
                Exec(conn, $@"INSERT INTO pending_event (pending_event_id, gcal_event_id, calendar_id, summary, description, start_datetime, end_datetime, app_created, source_system, ready_to_publish, operation_type, created_at, updated_at)
                              VALUES ('pending_overlay','g1','primary','Edited Title','new desc','2026-06-01 11:00:00','2026-06-01 12:00:00',0,'google-overlay',0,'edit','{TestTime}','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event;")).Should().Be(1, "overlay merges into the gcal row, no duplicate");
            Convert.ToInt64(Scalar(assertConn, "SELECT has_unpublished_changes FROM event WHERE gcal_event_id = 'g1';")).Should().Be(1);
            Scalar(assertConn, "SELECT summary FROM event WHERE gcal_event_id = 'g1';").Should().Be("Edited Title", "the local edit content is applied");
            Scalar(assertConn, "SELECT lifecycle FROM event WHERE gcal_event_id = 'g1';").Should().Be("approved");
            Scalar(assertConn, "SELECT publish FROM event WHERE gcal_event_id = 'g1';").Should().Be("published");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(b) delete overlay: keeps content, still marked dirty ----
    [Fact]
    public async Task Migration_DeleteOverlayPendingEvent_KeepsContent_MarkedDirty()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','Keep Me','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}');");
                Exec(conn, $@"INSERT INTO pending_event (pending_event_id, gcal_event_id, calendar_id, summary, app_created, source_system, ready_to_publish, operation_type, created_at, updated_at)
                              VALUES ('pending_del','g1','primary','Keep Me',0,'google-overlay',0,'delete','{TestTime}','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event;")).Should().Be(1);
            Convert.ToInt64(Scalar(assertConn, "SELECT has_unpublished_changes FROM event WHERE gcal_event_id = 'g1';")).Should().Be(1, "a staged delete is an unpublished change");
            Scalar(assertConn, "SELECT summary FROM event WHERE gcal_event_id = 'g1';").Should().Be("Keep Me");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(c): manual pending (no gcal id, source 'manual') -> approved/local_only ----
    [Fact]
    public async Task Migration_ManualPendingDraft_BecomesApprovedLocalOnly()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO pending_event (pending_event_id, gcal_event_id, calendar_id, summary, start_datetime, end_datetime, app_created, source_system, ready_to_publish, operation_type, created_at, updated_at)
                              VALUES ('pending_manual',NULL,'primary','My Draft','2026-06-03 09:00:00','2026-06-03 10:00:00',1,'manual',0,'edit','{TestTime}','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Scalar(assertConn, "SELECT event_id FROM event WHERE event_id = 'pending_manual';").Should().Be("pending_manual", "event_id reuses the pending id to keep links valid");
            Scalar(assertConn, "SELECT lifecycle FROM event WHERE event_id = 'pending_manual';").Should().Be("approved");
            Scalar(assertConn, "SELECT publish FROM event WHERE event_id = 'pending_manual';").Should().Be("local_only");
            Scalar(assertConn, "SELECT gcal_event_id FROM event WHERE event_id = 'pending_manual';").Should().Be(DBNull.Value);
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(d): machine pending (no gcal id, source 'toggl') -> candidate/local_only ----
    [Fact]
    public async Task Migration_MachinePendingDraft_BecomesCandidateLocalOnly()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO pending_event (pending_event_id, gcal_event_id, calendar_id, summary, start_datetime, end_datetime, app_created, source_system, ready_to_publish, operation_type, created_at, updated_at)
                              VALUES ('pending_toggl',NULL,'primary','Sleep','2026-06-03 23:00:00','2026-06-04 07:00:00',1,'toggl',0,'edit','{TestTime}','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Scalar(assertConn, "SELECT lifecycle FROM event WHERE event_id = 'pending_toggl';").Should().Be("candidate");
            Scalar(assertConn, "SELECT publish FROM event WHERE event_id = 'pending_toggl';").Should().Be("local_only");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(e): gcal_event_version repointed to the stable event_id ----
    [Fact]
    public async Task Migration_RepointsGcalEventVersionToEventId()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','Versioned','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}');");
                Exec(conn, $@"INSERT INTO gcal_event_version (gcal_event_id, summary, changed_by, change_reason, created_at)
                              VALUES ('g1','Old Snapshot','gcal_sync','updated','{TestTime}');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            var eventId = (string)Scalar(assertConn, "SELECT event_id FROM event WHERE gcal_event_id = 'g1';");
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM gcal_event_version;")).Should().Be(1);
            Scalar(assertConn, "SELECT event_id FROM gcal_event_version LIMIT 1;").Should().Be(eventId, "the version FK now references the stable event_id");
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #11(f): toggl_data.linked_event_id (gcal id) rewritten to the stable event_id ----
    [Fact]
    public async Task Migration_RewritesLinkedEventIdOnSourceTables()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','Linked','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}');");
                // toggl row linked to the gcal id, and a civ5 row linked to the same gcal id
                Exec(conn, $@"INSERT INTO toggl_data (toggl_id, start_time, linked_event_id, linked_event_type) VALUES (101,'2026-06-01 09:00:00','g1','gcal');");
                Exec(conn, $@"INSERT INTO civ5_data (scanned_at, file_modified_at, game_mode, linked_event_id, linked_event_type) VALUES ('{TestTime}','{TestTime}','single','g1','gcal');");
            }

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            var eventId = (string)Scalar(assertConn, "SELECT event_id FROM event WHERE gcal_event_id = 'g1';");
            Scalar(assertConn, "SELECT linked_event_id FROM toggl_data WHERE toggl_id = 101;").Should().Be(eventId);
            Scalar(assertConn, "SELECT linked_event_id FROM civ5_data WHERE linked_event_type = 'gcal' LIMIT 1;").Should().Be(eventId);
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #8: old tables are dropped after the transform ----
    [Fact]
    public async Task Migration_DropsLegacyTables()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            await svc.ApplyMigrationsAsync();

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            foreach (var legacy in new[] { "gcal_event", "pending_event", "date_source_integration" })
            {
                Convert.ToInt64(Scalar(assertConn, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{legacy}';"))
                    .Should().Be(0, $"{legacy} should be dropped by the migration");
            }
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='event';")).Should().Be(1);
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }

    // ---- AC #9 / #12: startup migration completes and integrity passes on a DB with old rows ----
    [Fact]
    public async Task Migration_RunStartupAsync_CompletesAndPassesIntegrity()
    {
        var (ctx, svc, dbPath, tempDir) = CreateTempFileService();
        try
        {
            await MigrateToOldSchemaAsync(ctx);
            ctx.Database.CloseConnection();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                Exec(conn, $@"INSERT INTO gcal_event (gcal_event_id, calendar_id, summary, start_datetime, end_datetime, created_at, updated_at)
                              VALUES ('g1','primary','A','2026-06-01 09:00:00','2026-06-01 10:00:00','{TestTime}','{TestTime}');");
                Exec(conn, $@"INSERT INTO pending_event (pending_event_id, gcal_event_id, calendar_id, summary, start_datetime, end_datetime, app_created, source_system, ready_to_publish, operation_type, created_at, updated_at)
                              VALUES ('pending_manual',NULL,'primary','Draft','2026-06-03 09:00:00','2026-06-03 10:00:00',1,'manual',0,'edit','{TestTime}','{TestTime}');");
            }

            // RunStartupAsync applies the migration and then runs PRAGMA integrity_check.
            await svc.RunStartupAsync();

            var healthy = await svc.CheckDatabaseIntegrityAsync();
            healthy.Should().BeTrue("the unified schema must pass integrity_check after migrating real rows");

            using var assertConn = new SqliteConnection($"Data Source={dbPath}");
            assertConn.Open();
            Convert.ToInt64(Scalar(assertConn, "SELECT COUNT(*) FROM event;")).Should().Be(2);

            // A pre-migration backup must have been created automatically (AC #9).
            var backupDir = Path.Combine(tempDir, "backups");
            Directory.GetFiles(backupDir, "calendar_backup_*_pre-migration.db").Should().NotBeEmpty();
        }
        finally
        {
            ctx.Dispose();
            CleanupTempDir(tempDir);
        }
    }
}
