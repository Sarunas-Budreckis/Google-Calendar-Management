using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SerilogTimings;

namespace GoogleCalendarManagement.Services;

public class MigrationService : IMigrationService
{
    private readonly CalendarDbContext _context;
    private readonly ILogger<MigrationService> _logger;
    private readonly string _dbPath;
    private readonly string _appDataDir;

    public MigrationService(CalendarDbContext context, DatabaseOptions dbOptions, ILogger<MigrationService> logger)
    {
        _context = context;
        _logger = logger;
        var builder = new SqliteConnectionStringBuilder(dbOptions.ConnectionString);
        _dbPath = builder.DataSource;
        _appDataDir = Path.GetDirectoryName(_dbPath)!;
    }

    public async Task RunStartupAsync()
    {
        using (Operation.Time("Database migration"))
        {
            await ApplyMigrationsAsync();
        }
        var isHealthy = await CheckDatabaseIntegrityAsync();
        if (!isHealthy)
            throw new InvalidOperationException("Database integrity check failed on startup.");
    }

    public async Task ApplyMigrationsAsync()
    {
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToList();
        if (!pending.Any())
        {
            _logger.LogInformation("No pending migrations.");
            return;
        }

        _logger.LogInformation("Found {Count} pending migrations: {Names}", pending.Count, string.Join(", ", pending));
        await CreateBackupAsync("pre-migration");
        _context.Database.CloseConnection();

        var directSqlMigrations = new HashSet<string>
        {
            "20260605021000_DropLegacyCiv5SessionPointTable",
            "20260605030000_RenameSpotifyStreamToSpotifyData",
        };

        if (pending.All(id => directSqlMigrations.Contains(id)))
        {
            _logger.LogInformation("Applying migrations via direct SQL to avoid EF Core migration lock.");
            ApplyDirectSqlMigrations(pending);
        }
        else
        {
            await _context.Database.MigrateAsync();
        }
        _logger.LogInformation("Successfully applied {Count} migrations.", pending.Count);

        var latestApplied = (await _context.Database.GetAppliedMigrationsAsync()).Last();
        var state = await _context.SystemStates.SingleOrDefaultAsync(s => s.StateName == "DatabaseSchemaVersion");
        if (state == null)
        {
            _context.SystemStates.Add(new SystemState
            {
                StateName = "DatabaseSchemaVersion",
                StateValue = latestApplied,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            state.StateValue = latestApplied;
            state.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> CheckDatabaseIntegrityAsync()
    {
        try
        {
            var conn = _context.Database.GetDbConnection();
            await _context.Database.OpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = await cmd.ExecuteScalarAsync();
            var passed = result?.ToString() == "ok";
            _logger.LogInformation("Database integrity check: {Result}", passed ? "OK" : "FAILED");
            return passed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database integrity check threw an exception.");
            return false;
        }
    }

    private static readonly Dictionary<string, string[]> _directSqlMap = new()
    {
        ["20260605021000_DropLegacyCiv5SessionPointTable"] =
        [
            "DROP TABLE IF EXISTS \"civ5_session_point\""
        ],
        ["20260605030000_RenameSpotifyStreamToSpotifyData"] =
        [
            "ALTER TABLE \"spotify_stream\" RENAME TO \"spotify_data\"",
            "DROP INDEX IF EXISTS \"idx_spotify_stream_dedup\"",
            "DROP INDEX IF EXISTS \"idx_spotify_stream_played_at\"",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"idx_spotify_data_dedup\" ON \"spotify_data\" (\"played_at\", \"track_name\")",
            "CREATE INDEX IF NOT EXISTS \"idx_spotify_data_played_at\" ON \"spotify_data\" (\"played_at\")",
            "INSERT OR IGNORE INTO data_source (source_key, display_name, supports_no_data_hint, created_at) VALUES ('spotify', 'Spotify (stats.fm)', 0, '2026-06-05 00:00:00')"
        ],
    };

    private void ApplyDirectSqlMigrations(IEnumerable<string> migrationIds)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        foreach (var migrationId in migrationIds)
        {
            _logger.LogInformation("Direct SQL migration: {MigrationId}", migrationId);
            using var tx = conn.BeginTransaction();
            foreach (var sql in _directSqlMap[migrationId])
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            using var hist = conn.CreateCommand();
            hist.Transaction = tx;
            hist.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, $ver)";
            hist.Parameters.AddWithValue("$id", migrationId);
            hist.Parameters.AddWithValue("$ver", "9.0.12");
            hist.ExecuteNonQuery();
            tx.Commit();
            _logger.LogInformation("Direct SQL migration done: {MigrationId}", migrationId);
        }
    }

    public Task CreateBackupAsync(string backupReason)
    {
        if (!File.Exists(_dbPath))
        {
            _logger.LogInformation("No database file to back up (fresh install).");
            return Task.CompletedTask;
        }

        var backupName = $"calendar_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{backupReason}.db";
        var backupPath = Path.Combine(_appDataDir, backupName);
        File.Copy(_dbPath, backupPath, overwrite: false);
        _logger.LogInformation("Created backup: {BackupPath}", backupPath);

        var oldBackups = Directory.GetFiles(_appDataDir, "calendar_backup_*.db")
            .OrderByDescending(File.GetCreationTimeUtc)
            .Skip(5)
            .ToList();
        foreach (var old in oldBackups)
        {
            File.Delete(old);
            _logger.LogInformation("Deleted old backup: {Path}", old);
        }
        return Task.CompletedTask;
    }
}
