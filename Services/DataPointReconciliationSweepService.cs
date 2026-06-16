using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

/// <inheritdoc />
public sealed class DataPointReconciliationSweepService : IDataPointReconciliationSweepService
{
    private readonly IDataPointProjectorRegistry _projectorRegistry;
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DataPointReconciliationSweepService> _logger;

    public DataPointReconciliationSweepService(
        IDataPointProjectorRegistry projectorRegistry,
        IDbContextFactory<CalendarDbContext> contextFactory,
        TimeProvider timeProvider,
        ILogger<DataPointReconciliationSweepService> logger)
    {
        _projectorRegistry = projectorRegistry;
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunPostImportAsync(string sourceKey, CancellationToken ct = default)
    {
        var projector = _projectorRegistry.GetProjector(sourceKey);
        if (projector is null)
        {
            _logger.LogWarning("No projector registered for sourceKey '{SourceKey}'", sourceKey);
            return;
        }

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var orphanedSpecs = await projector.GetOrphanedSpecsAsync(ctx, ct);
        var inserted = await InsertMissingDataPointsAsync(ctx, sourceKey, orphanedSpecs, ct);

        if (inserted > 0)
        {
            await ctx.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Reconciled {Count} missing datapoints for source '{SourceKey}'", inserted, sourceKey);
        }

        // Orphan deletion: remove data_point rows whose raw record no longer exists.
        var allRawRefs = await projector.GetAllRawSourceRefsAsync(ctx, ct);
        var rawRefsList = allRawRefs.ToList();
        var deleted = await ctx.DataPoints
            .Where(dp => dp.SourceKey == sourceKey && !rawRefsList.Contains(dp.SourceRef))
            .ExecuteDeleteAsync(ct);
        if (deleted > 0)
        {
            _logger.LogWarning(
                "Deleted {Count} stale datapoints for source '{SourceKey}' (raw records removed)",
                deleted, sourceKey);
        }
    }

    public async Task RunStartupDriftCheckAsync(CancellationToken ct = default)
    {
        // Sequential (not parallel) to avoid DB contention on startup.
        foreach (var projector in _projectorRegistry.GetAllProjectors())
        {
            await RunPostImportAsync(projector.SourceKey, ct);
        }
    }

    public async Task RebuildRegistryForSourceAsync(string sourceKey, CancellationToken ct = default)
    {
        var projector = _projectorRegistry.GetProjector(sourceKey);
        if (projector is null)
        {
            _logger.LogWarning("No projector registered for sourceKey '{SourceKey}'", sourceKey);
            return;
        }

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        await ctx.DataPoints
            .Where(dp => dp.SourceKey == sourceKey)
            .ExecuteDeleteAsync(ct);

        // After the delete, all raw records are "orphaned" and re-projected in full.
        var allSpecs = await projector.GetOrphanedSpecsAsync(ctx, ct);
        var inserted = await InsertMissingDataPointsAsync(ctx, sourceKey, allSpecs, ct);

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Rebuilt {Count} datapoints for source '{SourceKey}'", inserted, sourceKey);
    }

    public async Task RebuildRegistryAllAsync(CancellationToken ct = default)
    {
        foreach (var projector in _projectorRegistry.GetAllProjectors())
        {
            await RebuildRegistryForSourceAsync(projector.SourceKey, ct);
        }
    }

    /// <summary>
    /// Adds <see cref="DataPoint"/> rows for the specs that do not already exist
    /// (upsert-or-skip on the <c>(source_key, source_ref)</c> pair). Existing refs are
    /// fetched in a single batch query. Does not call SaveChanges; returns the number added.
    /// </summary>
    private async Task<int> InsertMissingDataPointsAsync(
        CalendarDbContext ctx,
        string sourceKey,
        IReadOnlyList<DataPointSpec> specs,
        CancellationToken ct)
    {
        if (specs.Count == 0)
        {
            return 0;
        }

        var candidateRefs = specs.Select(s => s.SourceRef).ToList();
        var existingRefs = await ctx.DataPoints
            .Where(dp => dp.SourceKey == sourceKey && candidateRefs.Contains(dp.SourceRef))
            .Select(dp => dp.SourceRef)
            .ToHashSetAsync(ct);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var added = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spec in specs)
        {
            // Skip rows already persisted, and de-duplicate within this batch.
            if (existingRefs.Contains(spec.SourceRef) || !seen.Add(spec.SourceRef))
            {
                continue;
            }

            ctx.DataPoints.Add(new DataPoint
            {
                SourceKey = spec.SourceKey,
                SourceRef = spec.SourceRef,
                StartUtc = spec.StartUtc,
                EndUtc = spec.EndUtc,
                CreatedAt = now
            });
            added++;
        }

        return added;
    }
}
