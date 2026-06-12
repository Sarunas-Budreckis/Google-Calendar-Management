namespace GoogleCalendarManagement.Services;

/// <summary>
/// Heals gaps in the <c>data_point</c> registry by (re-)projecting raw records through their
/// registered <see cref="IDataPointProjector"/>. Guarantees every raw imported record has a
/// matching <c>data_point</c> row.
/// </summary>
public interface IDataPointReconciliationSweepService
{
    /// <summary>
    /// Finds orphaned raw records for one source and inserts the missing <c>data_point</c> rows.
    /// No-op (with a warning) if no projector is registered for <paramref name="sourceKey"/>.
    /// </summary>
    Task RunPostImportAsync(string sourceKey, CancellationToken ct = default);

    /// <summary>
    /// Runs <see cref="RunPostImportAsync"/> for every registered projector. Logs a warning per
    /// source where orphans were found and healed.
    /// </summary>
    Task RunStartupDriftCheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes all <c>data_point</c> rows for <paramref name="sourceKey"/> then fully re-projects
    /// them (idempotent rebuild), wrapped in a single transaction.
    /// </summary>
    Task RebuildRegistryForSourceAsync(string sourceKey, CancellationToken ct = default);

    /// <summary>Calls <see cref="RebuildRegistryForSourceAsync"/> for every registered projector.</summary>
    Task RebuildRegistryAllAsync(CancellationToken ct = default);
}
