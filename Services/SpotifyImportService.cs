using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class SpotifyImportService : ISpotifyImportService
{
    public const string SourceKey = "spotify";
    public const string StatsFmTokenConfigKey = "statsfm_api_token";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IConfigRepository _configRepository;
    private readonly IStatsFmApiClient _apiClient;
    private readonly TimeProvider _timeProvider;

    public SpotifyImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IConfigRepository configRepository,
        IStatsFmApiClient apiClient,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _configRepository = configRepository;
        _apiClient = apiClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SpotifyImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var recordsFetched = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var token = await _configRepository.GetConfigValueAsync(StatsFmTokenConfigKey, ct);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new StatsFmApiException("Configure a stats.fm API token in Settings before importing.");
            }

            var items = await _apiClient.GetStreamsAsync(token, start, end, ct);
            recordsFetched = await UpsertStreamsAsync(items, ct);
            success = true;
            return new SpotifyImportResult(true, recordsFetched, null);
        }
        catch (Exception ex) when (ex is StatsFmApiException or HttpRequestException or TaskCanceledException)
        {
            errorMessage = ex is TaskCanceledException && !ct.IsCancellationRequested
                ? "The stats.fm import timed out. Check your network connection and try again."
                : ex.Message;
            return new SpotifyImportResult(false, recordsFetched, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, start, end, recordsFetched, success, errorMessage, ct);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<int> UpsertStreamsAsync(IReadOnlyList<StatsFmStreamItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var upserted = 0;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var item in items)
        {
            var playedAt = ParseEndTime(item.EndTime);
            var trackName = item.Track?.Name ?? "";
            var artistName = item.Track?.Artists?.FirstOrDefault()?.Name ?? "";
            var albumName = item.Track?.Albums?.FirstOrDefault()?.Name;
            var durationMs = item.Track?.DurationMs ?? 0;
            var msPlayed = item.PlayedMs;

            var existing = await context.SpotifyStreams
                .FirstOrDefaultAsync(s => s.PlayedAt == playedAt && s.TrackName == trackName, ct);

            if (existing is null)
            {
                context.SpotifyStreams.Add(new SpotifyStream
                {
                    PlayedAt = playedAt,
                    TrackName = trackName,
                    ArtistName = artistName,
                    AlbumName = albumName,
                    DurationMs = durationMs,
                    MsPlayed = msPlayed
                });
                upserted++;
            }
            else
            {
                existing.ArtistName = artistName;
                existing.AlbumName = albumName;
                existing.DurationMs = durationMs;
                existing.MsPlayed = msPlayed;
            }
        }

        await context.SaveChangesAsync(ct);
        return upserted;
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
}
