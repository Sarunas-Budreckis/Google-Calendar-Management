using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class DataSourceRepository : IDataSourceRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public DataSourceRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<DataSource>> GetAllSourcesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.DataSources.AsNoTracking().ToListAsync(ct);
    }

    public async Task<DataSource?> GetSourceByKeyAsync(string sourceKey, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.DataSources.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SourceKey == sourceKey, ct);
    }

    public async Task<DataSource> UpsertSourceAsync(DataSource source, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.DataSources
            .SingleOrDefaultAsync(s => s.SourceKey == source.SourceKey, ct);

        if (existing is null)
        {
            source.CreatedAt = DateTime.UtcNow;
            db.DataSources.Add(source);
            await db.SaveChangesAsync(ct);
            return source;
        }

        existing.DisplayName = source.DisplayName;
        existing.Description = source.Description;
        existing.SupportsNoDataHint = source.SupportsNoDataHint;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<DateSourceIntegration?> GetIntegrationAsync(DateOnly date, int dataSourceId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.DateSourceIntegrations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Date == date && i.DataSourceId == dataSourceId, ct);
    }

    public async Task<DateSourceIntegration> SetIntegrationAsync(DateOnly date, int dataSourceId, bool integrated, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.DateSourceIntegrations
            .SingleOrDefaultAsync(i => i.Date == date && i.DataSourceId == dataSourceId, ct);

        var now = DateTime.UtcNow;

        if (existing is not null)
        {
            existing.Integrated = integrated;
            existing.IntegratedAt = integrated ? now : existing.IntegratedAt;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var newRow = new DateSourceIntegration
        {
            Date = date,
            DataSourceId = dataSourceId,
            Integrated = integrated,
            IntegratedAt = integrated ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.DateSourceIntegrations.Add(newRow);
        await db.SaveChangesAsync(ct);
        return newRow;
    }

    public async Task<DataSourceImportLog?> GetLastImportAsync(int dataSourceId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.DataSourceImportLogs.AsNoTracking()
            .Where(l => l.DataSourceId == dataSourceId && l.Success)
            .OrderByDescending(l => l.ImportedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DataSourceImportLog> AddImportLogAsync(DataSourceImportLog log, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        log.ImportedAt = DateTime.UtcNow;
        db.DataSourceImportLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }
}
