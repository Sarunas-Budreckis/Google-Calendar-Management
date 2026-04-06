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
        if (file is null || string.IsNullOrWhiteSpace(file.Path))
        {
            return CreateFailureResult("Unable to open the selected ICS file.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(file.Path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            _logger.LogError(ex, "Failed to read ICS file from {Path}.", file.Path);
            return CreateFailureResult("Unable to read the selected ICS file.");
        }

        var parseResult = IcsParser.ParseIcs(content);
        if (!parseResult.IsValidCalendar)
        {
            return CreateFailureResult(parseResult.ErrorMessage ?? "The selected file is not a valid ICS calendar.");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var matchingUids = parseResult.Events
            .Select(parsedEvent => parsedEvent.Uid)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingEvents = matchingUids.Count == 0
            ? new Dictionary<string, GcalEvent>(StringComparer.Ordinal)
            : await context.GcalEvents
                .Where(gcalEvent => matchingUids.Contains(gcalEvent.GcalEventId))
                .ToDictionaryAsync(gcalEvent => gcalEvent.GcalEventId, StringComparer.Ordinal, ct);

        var newEventCount = 0;
        var updatedEventCount = 0;

        foreach (var parsedEvent in parseResult.Events)
        {
            if (existingEvents.TryGetValue(parsedEvent.Uid, out var existingEvent))
            {
                context.GcalEventVersions.Add(CreateVersionSnapshot(existingEvent, nowUtc));
                ApplyImportedValues(existingEvent, parsedEvent, nowUtc);
                updatedEventCount++;
                continue;
            }

            var createdEvent = CreateImportedEvent(parsedEvent, nowUtc);
            context.GcalEvents.Add(createdEvent);
            existingEvents[createdEvent.GcalEventId] = createdEvent;
            newEventCount++;
        }

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new ImportResult(
            Success: true,
            ImportedEventCount: newEventCount + updatedEventCount,
            NewEventCount: newEventCount,
            UpdatedEventCount: updatedEventCount,
            SkippedInvalidEventCount: parseResult.InvalidEventCount,
            SkippedRecurringEventCount: parseResult.SkippedRecurringEventCount,
            ErrorMessage: null);
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

    private static GcalEventVersion CreateVersionSnapshot(GcalEvent existingEvent, DateTime createdAt)
    {
        return new GcalEventVersion
        {
            GcalEventId = existingEvent.GcalEventId,
            GcalEtag = existingEvent.GcalEtag,
            Summary = existingEvent.Summary,
            Description = existingEvent.Description,
            StartDatetime = existingEvent.StartDatetime,
            EndDatetime = existingEvent.EndDatetime,
            IsAllDay = existingEvent.IsAllDay,
            ColorId = existingEvent.ColorId,
            GcalUpdatedAt = existingEvent.GcalUpdatedAt,
            RecurringEventId = existingEvent.RecurringEventId,
            IsRecurringInstance = existingEvent.IsRecurringInstance,
            ChangedBy = "ics_import",
            ChangeReason = "imported",
            CreatedAt = createdAt
        };
    }

    private static void ApplyImportedValues(GcalEvent existingEvent, IcsParser.ParsedEvent parsedEvent, DateTime updatedAt)
    {
        existingEvent.Summary = parsedEvent.Summary;
        existingEvent.Description = parsedEvent.Description;
        existingEvent.StartDatetime = parsedEvent.StartUtc;
        existingEvent.EndDatetime = parsedEvent.EndUtc;
        existingEvent.IsAllDay = parsedEvent.IsAllDay;
        existingEvent.UpdatedAt = updatedAt;
    }

    private static GcalEvent CreateImportedEvent(IcsParser.ParsedEvent parsedEvent, DateTime createdAt)
    {
        return new GcalEvent
        {
            GcalEventId = parsedEvent.Uid,
            CalendarId = "primary",
            Summary = parsedEvent.Summary,
            Description = parsedEvent.Description,
            StartDatetime = parsedEvent.StartUtc,
            EndDatetime = parsedEvent.EndUtc,
            IsAllDay = parsedEvent.IsAllDay,
            ColorId = "azure",
            IsDeleted = false,
            AppPublished = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
}
