using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglTransitImportService : ITogglTransitImportService
{
    public const string SourceKey = "toggl_transit";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IConfigRepository _configRepository;
    private readonly ITogglApiClient _apiClient;
    private readonly TimeProvider _timeProvider;

    public TogglTransitImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IConfigRepository configRepository,
        ITogglApiClient apiClient,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _configRepository = configRepository;
        _apiClient = apiClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TogglTransitImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var recordsFetched = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var apiToken = await _configRepository.GetConfigValueAsync(TogglSleepImportService.TogglApiTokenConfigKey, ct);
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                throw new TogglApiException("Configure a Toggl Track API token in Settings before importing.");
            }

            var entries = await _apiClient.GetTimeEntriesAsync(apiToken, start, end, ct);
            var transitEntries = entries
                .Where(IsCompletedTransitEntry)
                .DistinctBy(e => e.Id)
                .ToList();

            recordsFetched = await UpsertTransitEntriesAsync(transitEntries, ct);
            await ClassifyExistingTransitRowsAsync(ct);
            success = true;
            return new TogglTransitImportResult(true, recordsFetched, null);
        }
        catch (Exception ex) when (ex is TogglApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            errorMessage = ex is TaskCanceledException && !ct.IsCancellationRequested
                ? "The Toggl import timed out. Check your network connection and try again."
                : ex.Message;
            return new TogglTransitImportResult(false, recordsFetched, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, start, end, recordsFetched, success, errorMessage, ct);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<int> UpsertTransitEntriesAsync(IReadOnlyCollection<TogglTimeEntryDto> entries, CancellationToken ct)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var togglIds = entries.Select(e => e.Id).ToList();
        var existing = await context.TogglEntries
            .Where(e => togglIds.Contains(e.TogglId))
            .ToDictionaryAsync(e => e.TogglId, ct);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var dto in entries)
        {
            if (existing.TryGetValue(dto.Id, out var entry))
            {
                ApplyDto(entry, dto, nowUtc);
                entry.TogglDataType = TogglDataType.TogglTransit;
                continue;
            }

            var newEntry = new TogglEntry
            {
                TogglId = dto.Id,
                CreatedAt = nowUtc,
                TogglDataType = TogglDataType.TogglTransit
            };
            ApplyDto(newEntry, dto, nowUtc);
            context.TogglEntries.Add(newEntry);
        }

        await context.SaveChangesAsync(ct);
        return entries.Count;
    }

    private async Task ClassifyExistingTransitRowsAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unclassified = await context.TogglEntries
            .Where(e => e.TogglDataType != TogglDataType.TogglTransit
                        && e.ProjectName != null
                        && e.ProjectName.ToLower() == "transit")
            .ToListAsync(ct);

        foreach (var entry in unclassified)
        {
            entry.TogglDataType = TogglDataType.TogglTransit;
        }

        if (unclassified.Count > 0)
        {
            await context.SaveChangesAsync(ct);
        }
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
            DisplayName = "Toggl – Driving",
            Description = "Driving sessions from Toggl Track (Transit project)",
            SupportsNoDataHint = true,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        context.DataSources.Add(source);
        await context.SaveChangesAsync(ct);
        return source;
    }

    private async Task WriteImportLogAsync(
        int dataSourceId,
        DateOnly start,
        DateOnly end,
        int recordsFetched,
        bool success,
        string? errorMessage,
        CancellationToken ct)
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

    private static bool IsCompletedTransitEntry(TogglTimeEntryDto entry) =>
        entry.Duration >= 0 &&
        HasValidTogglDateTime(entry.Start) &&
        entry.ProjectName?.Equals("Transit", StringComparison.OrdinalIgnoreCase) == true;

    private static void ApplyDto(TogglEntry entry, TogglTimeEntryDto dto, DateTime nowUtc)
    {
        entry.Description = dto.Description;
        entry.StartTime = ParseTogglDateTime(dto.Start);
        entry.EndTime = string.IsNullOrWhiteSpace(dto.Stop) ? null : ParseTogglDateTime(dto.Stop);
        entry.DurationSeconds = dto.Duration;
        entry.ProjectName = dto.ProjectName;
        entry.Tags = dto.Tags is null ? null : JsonSerializer.Serialize(dto.Tags);
        entry.VisibleAsEvent = true;
        entry.LastSyncedAt = nowUtc;
        if (entry.CreatedAt == default)
        {
            entry.CreatedAt = nowUtc;
        }
    }

    private static DateTime ParseTogglDateTime(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).UtcDateTime;
    }

    private static bool HasValidTogglDateTime(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
