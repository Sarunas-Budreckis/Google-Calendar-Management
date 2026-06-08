using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Configurations;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class ComfyUIRepository : IComfyUIRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public ComfyUIRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<ComfyUIFolder>> GetActiveFoldersAsync(CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        return await ctx.ComfyUIFolders
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.AddedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ComfyUIFolder>> GetAllFoldersAsync(CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        return await ctx.ComfyUIFolders
            .AsNoTracking()
            .OrderByDescending(f => f.IsActive)
            .ThenBy(f => f.AddedAt)
            .ToListAsync(ct);
    }

    public async Task AddFolderAsync(string folderPath, DateTime addedAt, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await ctx.ComfyUIFolders
            .SingleOrDefaultAsync(f => f.FolderPath == folderPath, ct);

        if (existing is not null)
        {
            existing.IsActive = true;
            await ctx.SaveChangesAsync(ct);
            return;
        }

        ctx.ComfyUIFolders.Add(new ComfyUIFolder
        {
            FolderPath = folderPath,
            IsActive = true,
            AddedAt = addedAt
        });
        await ctx.SaveChangesAsync(ct);
    }

    public async Task DeactivateFolderAsync(int folderId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var folder = await ctx.ComfyUIFolders.FindAsync([folderId], ct);
        if (folder is null)
        {
            return;
        }

        folder.IsActive = false;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ComfyUIScanPoint>> GetPointsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var utcStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        if (!await TableExistsAsync(ctx, "comfyui_data", ct))
        {
            return [];
        }

        return await ctx.ComfyUIScanPoints
            .AsNoTracking()
            .Where(p => p.EventType == ComfyUIScanPointConfiguration.ModifiedEventType
                && p.Timestamp >= utcStart
                && p.Timestamp < utcEnd)
            .OrderBy(p => p.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetCreatedEventCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var utcStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var utcEnd = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        if (!await TableExistsAsync(ctx, "comfyui_data", ct))
        {
            return new Dictionary<DateOnly, int>();
        }

        var timestamps = await ctx.ComfyUIScanPoints
            .AsNoTracking()
            .Where(p => p.EventType == ComfyUIScanPointConfiguration.CreatedEventType
                && p.Timestamp >= utcStart
                && p.Timestamp < utcEnd)
            .Select(p => p.Timestamp)
            .ToListAsync(ct);

        return timestamps
            .GroupBy(ts => DateOnly.FromDateTime(NormalizeUtc(ts).ToLocalTime()))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<HashSet<(DateTime Timestamp, string EventType)>> GetExistingDedupKeysAsync(
        IReadOnlyList<(DateTime Timestamp, string EventType)> candidates, CancellationToken ct = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var min = candidates.Min(c => c.Timestamp);
        var max = candidates.Max(c => c.Timestamp);
        var candidateTypes = candidates
            .Select(c => c.EventType)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        if (!await TableExistsAsync(ctx, "comfyui_data", ct))
        {
            return [];
        }

        var existing = await ctx.ComfyUIScanPoints
            .AsNoTracking()
            .Where(p => p.Timestamp >= min
                && p.Timestamp <= max
                && candidateTypes.Contains(p.EventType))
            .Select(p => new { p.Timestamp, p.EventType })
            .ToListAsync(ct);

        return existing
            .Select(p => (p.Timestamp, p.EventType))
            .ToHashSet();
    }

    public async Task InsertPointsAsync(IReadOnlyList<ComfyUIScanPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0)
        {
            return;
        }

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        ctx.ComfyUIScanPoints.AddRange(points);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<int> CountPointsAsync(CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        if (!await TableExistsAsync(ctx, "comfyui_data", ct))
        {
            return 0;
        }

        return await ctx.ComfyUIScanPoints.CountAsync(ct);
    }

    private static async Task<bool> TableExistsAsync(CalendarDbContext ctx, string tableName, CancellationToken ct)
    {
        var connection = ctx.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            return await command.ExecuteScalarAsync(ct) is not null;
        }
        catch (SqliteException)
        {
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
