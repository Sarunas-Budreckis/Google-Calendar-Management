using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        await ApplyMigrationsAsync();
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
        await _context.Database.MigrateAsync();
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
