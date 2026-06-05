using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglSleepQualityRepository : ITogglSleepQualityRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public TogglSleepQualityRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<int?> GetQualityForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var record = await context.TogglSleepQualities
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.Date == date, ct);
        return record?.Quality;
    }

    public async Task UpsertQualityAsync(DateOnly date, int? quality, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.TogglSleepQualities
            .SingleOrDefaultAsync(q => q.Date == date, ct);

        var utcNow = DateTime.UtcNow;
        if (existing is null)
        {
            context.TogglSleepQualities.Add(new TogglSleepQuality
            {
                Date = date,
                Quality = quality,
                UpdatedAt = utcNow
            });
        }
        else
        {
            existing.Quality = quality;
            existing.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}
