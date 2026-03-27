namespace GoogleCalendarManagement.Services;

public interface IMigrationService
{
    Task RunStartupAsync();
    Task ApplyMigrationsAsync();
    Task<bool> CheckDatabaseIntegrityAsync();
    Task CreateBackupAsync(string backupReason);
}
