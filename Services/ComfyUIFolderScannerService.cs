using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Configurations;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class ComfyUIFolderScannerService : IComfyUIFolderScannerService
{
    public const string SourceKey = "comfyui_data";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IComfyUIRepository _repository;
    private readonly IContentDialogService _dialogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ComfyUIFolderScannerService> _logger;

    public ComfyUIFolderScannerService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IComfyUIRepository repository,
        IContentDialogService dialogService,
        ILogger<ComfyUIFolderScannerService> logger,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _repository = repository;
        _dialogService = dialogService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ComfyUIScanResult> ScanAsync(CancellationToken ct = default)
    {
        return await ScanInternalAsync(null, null, ct);
    }

    public async Task<ComfyUIScanResult> ScanAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await ScanInternalAsync(from, to, ct);
    }

    private async Task<ComfyUIScanResult> ScanInternalAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var source = await EnsureDataSourceAsync(ct);
        var newPointsAdded = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var folders = await _repository.GetActiveFoldersAsync(ct);
            if (folders.Count == 0)
            {
                success = true;
                return new ComfyUIScanResult(true, 0, await _repository.CountPointsAsync(ct), null);
            }

            DateTime? utcStart = from.HasValue
                ? DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime()
                : null;
            DateTime? utcEnd = to.HasValue
                ? DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime()
                : null;
            var candidates = await CollectCandidatesAsync(folders, utcStart, utcEnd, ct);
            if (candidates.Count == 0)
            {
                success = true;
                return new ComfyUIScanResult(true, 0, await _repository.CountPointsAsync(ct), null);
            }

            var existingKeys = await _repository.GetExistingDedupKeysAsync(candidates, ct);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var newPoints = candidates
                .Distinct()
                .Where(c => !existingKeys.Contains(c))
                .Select(c => new ComfyUIScanPoint
                {
                    ScannedAt = now,
                    Timestamp = c.Timestamp,
                    EventType = c.EventType
                })
                .ToList();

            await _repository.InsertPointsAsync(newPoints, ct);
            newPointsAdded = newPoints.Count;
            success = true;
            return new ComfyUIScanResult(true, newPointsAdded, await _repository.CountPointsAsync(ct), null);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "ComfyUI folder scan failed");
            return new ComfyUIScanResult(false, newPointsAdded, await _repository.CountPointsAsync(CancellationToken.None), errorMessage);
        }
        finally
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            var coveredFrom = from ?? DateOnly.FromDateTime(now);
            var coveredTo = to ?? DateOnly.FromDateTime(now);
            await WriteImportLogAsync(source.DataSourceId, coveredFrom, coveredTo, newPointsAdded, success, errorMessage, ct);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<List<(DateTime Timestamp, string EventType)>> CollectCandidatesAsync(
        IReadOnlyList<ComfyUIFolder> folders,
        DateTime? utcStart,
        DateTime? utcEnd,
        CancellationToken ct)
    {
        var results = new List<(DateTime Timestamp, string EventType)>();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.FolderPath))
            {
                await _dialogService.ShowErrorAsync(
                    "ComfyUI Scan",
                    $"Could not access folder: {folder.FolderPath}. Check that it exists and you have read permissions.");
                continue;
            }

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(folder.FolderPath);

            while (pendingDirectories.Count > 0)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var currentDirectory = pendingDirectories.Pop();
                AddTimestampEvents(currentDirectory, utcStart, utcEnd, results);

                IReadOnlyList<string> children;
                try
                {
                    children = Directory.EnumerateFileSystemEntries(currentDirectory).ToList();
                }
                catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
                {
                    _logger.LogWarning(ex, "Cannot access ComfyUI folder {Path}", currentDirectory);
                    if (string.Equals(currentDirectory, folder.FolderPath, StringComparison.Ordinal))
                    {
                        await _dialogService.ShowErrorAsync(
                            "ComfyUI Scan",
                            $"Could not access folder: {folder.FolderPath}. Check that it exists and you have read permissions.");
                    }

                    continue;
                }

                foreach (var child in children)
                {
                    if (Directory.Exists(child))
                    {
                        pendingDirectories.Push(child);
                    }
                    else
                    {
                        AddTimestampEvents(child, utcStart, utcEnd, results);
                    }
                }
            }
        }

        return results;
    }

    private void AddTimestampEvents(
        string path,
        DateTime? utcStart,
        DateTime? utcEnd,
        List<(DateTime Timestamp, string EventType)> results)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);

            AddIfInRange(info.CreationTimeUtc, ComfyUIScanPointConfiguration.CreatedEventType, utcStart, utcEnd, results);
            AddIfInRange(info.LastWriteTimeUtc, ComfyUIScanPointConfiguration.ModifiedEventType, utcStart, utcEnd, results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot read metadata for {Path}", path);
        }
    }

    private static void AddIfInRange(
        DateTime timestamp,
        string eventType,
        DateTime? utcStart,
        DateTime? utcEnd,
        List<(DateTime Timestamp, string EventType)> results)
    {
        if ((!utcStart.HasValue || timestamp >= utcStart.Value) &&
            (!utcEnd.HasValue || timestamp < utcEnd.Value))
        {
            results.Add((timestamp, eventType));
        }
    }

    private async Task<DataSource> EnsureDataSourceAsync(CancellationToken ct)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await ctx.DataSources.SingleOrDefaultAsync(s => s.SourceKey == SourceKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var legacy = await ctx.DataSources.SingleOrDefaultAsync(s => s.SourceKey == "comfyui", ct);
        if (legacy is not null)
        {
            legacy.SourceKey = SourceKey;
            legacy.DisplayName = "ComfyUI";
            await ctx.SaveChangesAsync(ct);
            return legacy;
        }

        var source = new DataSource
        {
            SourceKey = SourceKey,
            DisplayName = "ComfyUI",
            SupportsNoDataHint = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        ctx.DataSources.Add(source);
        await ctx.SaveChangesAsync(ct);
        return source;
    }

    private async Task WriteImportLogAsync(
        int dataSourceId,
        DateOnly from,
        DateOnly to,
        int recordsFetched,
        bool success,
        string? errorMessage,
        CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        ctx.DataSourceImportLogs.Add(new DataSourceImportLog
        {
            DataSourceId = dataSourceId,
            CoveredStartDate = from,
            CoveredEndDate = to,
            ImportedAt = now,
            RecordsFetched = recordsFetched,
            Success = success,
            ErrorMessage = errorMessage
        });
        await ctx.SaveChangesAsync(ct);
    }
}
