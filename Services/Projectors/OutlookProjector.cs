using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class OutlookProjector : IDataPointProjector
{
    public string SourceKey => OutlookImportService.SourceKey;

    public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        var existingRefs = await ExistingRefsAsync(ctx, ct);
        var events = await ctx.OutlookEvents.AsNoTracking().ToListAsync(ct);

        return events
            .Where(e => !existingRefs.Contains(e.OutlookEventId))
            .Select(Project)
            .ToList();
    }

    public async Task<IReadOnlyList<DataPointSpec>> ProjectSourceRefsAsync(
        CalendarDbContext ctx,
        IReadOnlyList<string> sourceRefs,
        CancellationToken ct = default)
    {
        if (sourceRefs.Count == 0)
        {
            return [];
        }

        var requestedRefs = sourceRefs.ToHashSet(StringComparer.Ordinal);
        var events = await ctx.OutlookEvents.AsNoTracking().ToListAsync(ct);

        return events
            .Where(e => requestedRefs.Contains(e.OutlookEventId))
            .Select(Project)
            .ToList();
    }

    private async Task<HashSet<string>> ExistingRefsAsync(CalendarDbContext ctx, CancellationToken ct)
    {
        return await ctx.DataPoints
            .Where(dp => dp.SourceKey == SourceKey)
            .Select(dp => dp.SourceRef)
            .ToHashSetAsync(StringComparer.Ordinal, ct);
    }

    public async Task<IReadOnlyList<string>> GetAllRawSourceRefsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        return await ctx.OutlookEvents
            .AsNoTracking()
            .Select(e => e.OutlookEventId)
            .ToListAsync(ct);
    }

    private DataPointSpec Project(OutlookEvent ev) =>
        new(SourceKey, ev.OutlookEventId, ev.StartDatetime, ev.EndDatetime);
}
