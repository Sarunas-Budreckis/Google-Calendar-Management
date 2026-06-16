using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class SpotifyProjector : IDataPointProjector
{
    public string SourceKey => SpotifyImportService.SourceKey;

    public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default)
    {
        var existingRefs = await ExistingRefsAsync(ctx, ct);
        var streams = await ctx.SpotifyStreams.AsNoTracking().ToListAsync(ct);

        return streams
            .Where(s => !string.IsNullOrWhiteSpace(s.NaturalKey))
            .Where(s => !existingRefs.Contains(s.NaturalKey))
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
        var streams = await ctx.SpotifyStreams.AsNoTracking().ToListAsync(ct);

        return streams
            .Where(s => requestedRefs.Contains(s.NaturalKey))
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
        return await ctx.SpotifyStreams
            .AsNoTracking()
            .Where(s => !string.IsNullOrWhiteSpace(s.NaturalKey))
            .Select(s => s.NaturalKey)
            .ToListAsync(ct);
    }

    private DataPointSpec Project(SpotifyStream stream)
    {
        var elapsedMs = stream.MsPlayed > 0 ? stream.MsPlayed : stream.DurationMs;
        var startUtc = elapsedMs > 0
            ? stream.PlayedAt - TimeSpan.FromMilliseconds(elapsedMs)
            : stream.PlayedAt;

        return new(SourceKey, stream.NaturalKey, startUtc, stream.PlayedAt);
    }
}
