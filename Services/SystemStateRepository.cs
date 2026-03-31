using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class SystemStateRepository : ISystemStateRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public SystemStateRepository(
        IDbContextFactory<CalendarDbContext> dbContextFactory,
        TimeProvider? timeProvider = null)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.SystemStates
            .AsNoTracking()
            .Where(state => state.StateName == key)
            .Select(state => state.StateValue)
            .SingleOrDefaultAsync(ct);
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await SetManyAsync(new Dictionary<string, string> { [key] = value }, ct);
    }

    public async Task SetManyAsync(IReadOnlyDictionary<string, string> pairs, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        var keys = pairs.Keys.ToList();
        var existing = await context.SystemStates
            .Where(state => state.StateName != null && keys.Contains(state.StateName))
            .ToListAsync(ct);
        var existingByKey = existing
            .Where(s => s.StateName is not null)
            .ToDictionary(s => s.StateName!);

        foreach (var (key, value) in pairs)
        {
            if (existingByKey.TryGetValue(key, out var row))
            {
                row.StateValue = value;
                row.UpdatedAt = now;
            }
            else
            {
                context.SystemStates.Add(new SystemState
                {
                    StateName = key,
                    StateValue = value,
                    UpdatedAt = now
                });
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
