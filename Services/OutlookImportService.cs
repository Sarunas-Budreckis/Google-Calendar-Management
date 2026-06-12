using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class OutlookImportService : IOutlookImportService
{
    public const string SourceKey = "outlook";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IGraphApiClient _apiClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutlookImportService> _logger;

    public OutlookImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IGraphApiClient apiClient,
        TimeProvider? timeProvider = null,
        ILogger<OutlookImportService>? logger = null)
    {
        _contextFactory = contextFactory;
        _apiClient = apiClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OutlookImportService>.Instance;
    }

    public async Task<OutlookImportResult> ImportAsync(string accessToken, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var newRecords = 0;
        var updatedRecords = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            _logger.LogInformation("Starting Outlook import for {StartDate} through {EndDate}.", start, end);
            var events = await _apiClient.GetCalendarViewAsync(accessToken, start, end, ct);
            _logger.LogInformation("Graph API returned {Count} event(s).", events.Count);

            (newRecords, updatedRecords) = await UpsertEventsAsync(events, ct);
            _logger.LogInformation("Outlook import stored {New} new and updated {Updated} existing event(s).", newRecords, updatedRecords);
            success = true;
            return new OutlookImportResult(true, newRecords, updatedRecords, null);
        }
        catch (Exception ex) when (ex is GraphApiException or HttpRequestException or TaskCanceledException)
        {
            errorMessage = ex is TaskCanceledException && !ct.IsCancellationRequested
                ? "The Outlook import timed out. Check your network connection and try again."
                : ex.Message;
            return new OutlookImportResult(false, newRecords, updatedRecords, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, start, end, newRecords + updatedRecords, success, errorMessage, CancellationToken.None);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<(int inserted, int updated)> UpsertEventsAsync(IReadOnlyList<GraphEventDto> events, CancellationToken ct)
    {
        if (events.Count == 0)
        {
            return (0, 0);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var inserted = 0;
        var updated = 0;

        foreach (var dto in events)
        {
            var startUtc = ParseGraphDateTime(dto.Start);
            var endUtc = ParseGraphDateTime(dto.End);
            var organizer = dto.Organizer?.EmailAddress?.Name;
            var location = dto.Location?.DisplayName;
            var isRecurring = dto.Type is "occurrence" or "exception" or "seriesMaster";

            var existing = await context.OutlookEvents
                .FirstOrDefaultAsync(e => e.OutlookEventId == dto.Id, ct);

            if (existing is null)
            {
                context.OutlookEvents.Add(new OutlookEvent
                {
                    OutlookEventId = dto.Id,
                    Subject = dto.Subject,
                    StartDatetime = startUtc,
                    EndDatetime = endUtc,
                    IsAllDay = dto.IsAllDay,
                    Organizer = organizer,
                    Location = location,
                    BodyPreview = dto.BodyPreview,
                    IsRecurring = isRecurring,
                    SeriesMasterId = dto.SeriesMasterId,
                    LastSyncedAt = now,
                    IsSuppressed = false
                });
                inserted++;
            }
            else
            {
                existing.Subject = dto.Subject;
                existing.StartDatetime = startUtc;
                existing.EndDatetime = endUtc;
                existing.IsAllDay = dto.IsAllDay;
                existing.Organizer = organizer;
                existing.Location = location;
                existing.BodyPreview = dto.BodyPreview;
                existing.IsRecurring = isRecurring;
                existing.SeriesMasterId = dto.SeriesMasterId;
                existing.LastSyncedAt = now;
                updated++;
            }
        }

        await context.SaveChangesAsync(ct);
        return (inserted, updated);
    }

    private async Task<DataSource> EnsureDataSourceAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.DataSources.SingleOrDefaultAsync(s => s.SourceKey == SourceKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var source = new DataSource
        {
            SourceKey = SourceKey,
            DisplayName = "Outlook Work Calendar",
            Description = "Mayo Clinic Outlook work calendar events via Microsoft Graph API",
            SupportsNoDataHint = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        context.DataSources.Add(source);
        await context.SaveChangesAsync(ct);
        return source;
    }

    private async Task WriteImportLogAsync(
        int dataSourceId, DateOnly start, DateOnly end, int recordsFetched,
        bool success, string? errorMessage, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.DataSourceImportLogs.Add(new DataSourceImportLog
        {
            DataSourceId = dataSourceId,
            CoveredStartDate = start,
            CoveredEndDate = end,
            ImportedAt = _timeProvider.GetUtcNow().UtcDateTime,
            RecordsFetched = recordsFetched,
            Success = success,
            ErrorMessage = errorMessage
        });
        await context.SaveChangesAsync(ct);
    }

    private static DateTime ParseGraphDateTime(GraphDateTimeDto? dto)
    {
        if (dto is null)
        {
            return DateTime.UtcNow;
        }

        var parsed = DateTime.Parse(dto.DateTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
        // Graph API returns UTC when timeZone == "UTC", otherwise we treat as UTC for simplicity
        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }
}
