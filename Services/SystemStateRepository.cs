using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class SystemStateRepository : ISystemStateRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public SystemStateRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.SystemStates.SingleOrDefaultAsync(state => state.StateName == key, ct);

        if (existing is null)
        {
            context.SystemStates.Add(new SystemState
            {
                StateName = key,
                StateValue = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.StateValue = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}
