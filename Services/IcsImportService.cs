using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace GoogleCalendarManagement.Services;

public sealed class IcsImportService : IIcsImportService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<IcsImportService> _logger;
    private readonly TimeProvider _timeProvider;

    public IcsImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ILogger<IcsImportService> logger,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ImportResult> ImportFromFileAsync(StorageFile file, CancellationToken ct = default)
    {
        if (file is null)
        {
            return CreateFailureResult("Unable to open the selected ICS file.");
        }

        string content;
        try
        {
            content = await FileIO.ReadTextAsync(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read ICS file '{Name}'.", file.Name);
            return CreateFailureResult("Unable to read the selected ICS file.");
        }

        var parseResult = IcsParser.ParseIcs(content);
        if (!parseResult.IsValidCalendar)
        {
            return CreateFailureResult(parseResult.ErrorMessage ?? "The selected file is not a valid ICS calendar.");
        }

        // TODO 8.3/8.5: persist imported events into the unified `event` table (mint stable
        // event_id, snapshot version history). The gcal_event table was removed in Story 8.2, so
        // import is parse-only and does not write until the event-model rewrite lands.
        _ = _timeProvider;
        _ = _contextFactory;
        return CreateFailureResult(
            "ICS import is temporarily disabled while the event model is migrated (restored in Story 8.3/8.5).");
    }

    private static ImportResult CreateFailureResult(string message)
    {
        return new ImportResult(
            Success: false,
            ImportedEventCount: 0,
            NewEventCount: 0,
            UpdatedEventCount: 0,
            SkippedInvalidEventCount: 0,
            SkippedRecurringEventCount: 0,
            ErrorMessage: message);
    }

    // NOTE: the GcalEvent / GcalEventVersion mapping helpers were removed in Story 8.2 (gcal_event
    // table dropped). They are reintroduced against the unified `event` table in Story 8.3/8.5.
}
