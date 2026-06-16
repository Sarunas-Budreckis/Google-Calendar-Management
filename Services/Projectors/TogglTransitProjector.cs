using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglTransitProjector : IDataPointProjector
{
    public string SourceKey => TogglTransitImportService.SourceKey;

    public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        var existingRefs = await ExistingRefsAsync(ctx, ct);
        var entries = await ctx.TogglEntries
            .AsNoTracking()
            .Where(e => e.TogglDataType == TogglDataType.TogglTransit)
            .ToListAsync(ct);

        return entries
            .Where(e => !existingRefs.Contains(SourceRef(e)))
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
        var entries = await ctx.TogglEntries
            .AsNoTracking()
            .Where(e => e.TogglDataType == TogglDataType.TogglTransit)
            .ToListAsync(ct);

        return entries
            .Where(e => requestedRefs.Contains(SourceRef(e)))
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
        var ids = await ctx.TogglEntries
            .AsNoTracking()
            .Where(e => e.TogglDataType == TogglDataType.TogglTransit)
            .Select(e => e.TogglId)
            .ToListAsync(ct);
        return ids.Select(id => id.ToString()).ToList();
    }

    private DataPointSpec Project(TogglEntry entry) =>
        new(SourceKey, SourceRef(entry), entry.StartTime, entry.EndTime ?? entry.StartTime);

    private static string SourceRef(TogglEntry entry) => entry.TogglId.ToString();
}
