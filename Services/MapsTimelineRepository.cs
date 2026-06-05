using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class MapsTimelineRepository : IMapsTimelineRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public MapsTimelineRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MapsTimelineRaw?> GetLatestAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.MapsTimelineRaws
            .AsNoTracking()
            .OrderByDescending(r => r.ImportedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(MapsTimelineRaw record, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.MapsTimelineRaws.Add(record);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await context.MapsTimelineRaws.ExecuteDeleteAsync(ct);
    }
}
