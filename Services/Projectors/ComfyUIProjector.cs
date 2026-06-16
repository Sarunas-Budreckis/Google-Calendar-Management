using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class ComfyUIProjector : IDataPointProjector
{
    public string SourceKey => ComfyUIFolderScannerService.SourceKey;

    public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        var existingRefs = await ExistingRefsAsync(ctx, ct);
        var points = await ctx.ComfyUIScanPoints.AsNoTracking().ToListAsync(ct);

        return points
            .Where(p => !existingRefs.Contains(SourceRef(p)))
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
        var points = await ctx.ComfyUIScanPoints.AsNoTracking().ToListAsync(ct);

        return points
            .Where(p => requestedRefs.Contains(SourceRef(p)))
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
        var ids = await ctx.ComfyUIScanPoints
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(ct);
        return ids.Select(id => id.ToString()).ToList();
    }

    private DataPointSpec Project(ComfyUIScanPoint point) =>
        new(SourceKey, SourceRef(point), point.Timestamp, point.Timestamp);

    private static string SourceRef(ComfyUIScanPoint point) => point.Id.ToString();
}
