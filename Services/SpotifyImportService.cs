using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class SpotifyImportService : ISpotifyImportService
{
    public const string SourceKey = "spotify";
    public const string StatsFmTokenConfigKey = "statsfm_api_token";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IConfigRepository _configRepository;
    private readonly IStatsFmApiClient _apiClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SpotifyImportService> _logger;

    public SpotifyImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IConfigRepository configRepository,
        IStatsFmApiClient apiClient,
        TimeProvider? timeProvider = null,
        ILogger<SpotifyImportService>? logger = null)
    {
        _contextFactory = contextFactory;
        _configRepository = configRepository;
        _apiClient = apiClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SpotifyImportService>.Instance;
    }

    public async Task<SpotifyImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var newRecords = 0;
        var updatedRecords = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var token = await _configRepository.GetConfigValueAsync(StatsFmTokenConfigKey, ct);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new StatsFmApiException("Configure a stats.fm API token in Settings before importing.");
            }

            _logger.LogInformation("Starting stats.fm import for {StartDate} through {EndDate}.", start, end);
            var items = await _apiClient.GetStreamsAsync(token, start, end, ct);
            _logger.LogInformation("stats.fm API returned {Count} stream item(s) for import.", items.Count);
            (newRecords, updatedRecords) = await UpsertStreamsAsync(items, ct);
            _logger.LogInformation(
                "stats.fm import stored {Inserted} new stream(s) and updated {Updated} existing stream(s).",
                newRecords, updatedRecords);
            success = true;
            return new SpotifyImportResult(true, newRecords, updatedRecords, null);
        }
        catch (Exception ex) when (ex is StatsFmApiException or HttpRequestException or TaskCanceledException)
        {
            errorMessage = ex is TaskCanceledException && !ct.IsCancellationRequested
                ? "The stats.fm import timed out. Check your network connection and try again."
                : ex.Message;
            return new SpotifyImportResult(false, newRecords, updatedRecords, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, start, end, newRecords + updatedRecords, success, errorMessage, CancellationToken.None);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<(int inserted, int updated)> UpsertStreamsAsync(IReadOnlyList<StatsFmStreamItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return (0, 0);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var inserted = 0;
        var updated = 0;
        // Tracks entities added in this batch so duplicate keys within the same batch
        // are treated as in-memory updates rather than triggering a UNIQUE constraint violation.
        var pendingInserts = new Dictionary<(DateTime, string), SpotifyStream>();

        foreach (var item in items)
        {
            var playedAt = ParseEndTime(item.EndTime);
            var trackName = item.Track?.Name ?? item.TrackName ?? "";
            var artistName = item.Track?.Artists?.FirstOrDefault()?.Name ?? "";
            var albumName = item.Track?.Albums?.FirstOrDefault()?.Name;
            var durationMs = item.Track?.DurationMs ?? 0;
            var msPlayed = item.PlayedMs;
            var naturalKey = BuildNaturalKey(playedAt, trackName);
            var key = (playedAt, trackName);

            if (pendingInserts.TryGetValue(key, out var pending))
            {
                pending.NaturalKey = naturalKey;
                pending.ArtistName = artistName;
                pending.AlbumName = albumName;
                pending.DurationMs = durationMs;
                pending.MsPlayed = msPlayed;
                continue;
            }

            var existing = await context.SpotifyStreams
                .FirstOrDefaultAsync(s => s.PlayedAt == playedAt && s.TrackName == trackName, ct);

            if (existing is null)
            {
                var newStream = new SpotifyStream
                {
                    NaturalKey = naturalKey,
                    PlayedAt = playedAt,
                    TrackName = trackName,
                    ArtistName = artistName,
                    AlbumName = albumName,
                    DurationMs = durationMs,
                    MsPlayed = msPlayed
                };
                context.SpotifyStreams.Add(newStream);
                pendingInserts[key] = newStream;
                inserted++;
            }
            else
            {
                existing.NaturalKey = naturalKey;
                existing.ArtistName = artistName;
                existing.AlbumName = albumName;
                existing.DurationMs = durationMs;
                existing.MsPlayed = msPlayed;
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
            DisplayName = "Spotify",
            Description = "Spotify streaming history from stats.fm",
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

    private static DateTime ParseEndTime(string endTime)
    {
        return DateTimeOffset.Parse(endTime, CultureInfo.InvariantCulture).UtcDateTime;
    }

    private static string BuildNaturalKey(DateTime playedAt, string trackName) => $"{playedAt:yyyy-MM-ddTHH:mm:ss.fffffff}|{trackName}";
}
