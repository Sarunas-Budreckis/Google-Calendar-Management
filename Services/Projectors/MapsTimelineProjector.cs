using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class MapsTimelineProjector : IDataPointProjector
{
    public string SourceKey => MapsTimelineImportHandler.SourceKey;

    public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        var existingRefs = await ExistingRefsAsync(ctx, ct);
        var rows = await ctx.MapsTimelineRaws.AsNoTracking().ToListAsync(ct);

        return rows
            .Where(r => !existingRefs.Contains(r.FileName))
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
        var rows = await ctx.MapsTimelineRaws.AsNoTracking().ToListAsync(ct);

        return rows
            .Where(r => requestedRefs.Contains(r.FileName))
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
        return await ctx.MapsTimelineRaws
            .AsNoTracking()
            .Select(r => r.FileName)
            .ToListAsync(ct);
    }

    private DataPointSpec Project(MapsTimelineRaw raw)
    {
        var startUtc = raw.CoveredDateMin.HasValue
            ? AsUtc(raw.CoveredDateMin.Value.ToDateTime(TimeOnly.MinValue))
            : raw.ImportedAt;
        var endUtc = raw.CoveredDateMax.HasValue
            ? AsUtc(raw.CoveredDateMax.Value.ToDateTime(TimeOnly.MaxValue))
            : raw.ImportedAt;

        return new(SourceKey, raw.FileName, startUtc, endUtc);
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
