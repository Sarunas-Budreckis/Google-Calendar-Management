using System.Text;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class IcsExportService : IIcsExportService
{
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IIcsFileSavePickerService _fileSavePickerService;
    private readonly ILogger<IcsExportService> _logger;
    private readonly TimeProvider _timeProvider;

    public IcsExportService(
        IGcalEventRepository gcalEventRepository,
        IIcsFileSavePickerService fileSavePickerService,
        ILogger<IcsExportService> logger,
        TimeProvider? timeProvider = null)
    {
        _gcalEventRepository = gcalEventRepository;
        _fileSavePickerService = fileSavePickerService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ExportResult> ExportToFileAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var suggestedFileName = $"calendar-export-{_timeProvider.GetLocalNow():yyyy-MM-dd}.ics";
        var savePath = await _fileSavePickerService.PickSavePathAsync(suggestedFileName);

        if (string.IsNullOrWhiteSpace(savePath))
        {
            return new ExportResult(
                Success: false,
                WasCancelled: true,
                ExportedEventCount: 0,
                FileName: null,
                ErrorMessage: null);
        }

        var rangeStartUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var candidateEvents = await _gcalEventRepository.GetByDateRangeAsync(from, to, ct);
        var exportEvents = candidateEvents
            .Where(calendarEvent => IntersectsRange(calendarEvent, rangeStartUtc, rangeEndExclusiveUtc))
            .ToList();

        if (exportEvents.Count == 0)
        {
            return new ExportResult(
                Success: true,
                WasCancelled: false,
                ExportedEventCount: 0,
                FileName: Path.GetFileName(savePath),
                ErrorMessage: null);
        }

        var icsContent = IcsExporter.GenerateIcs(exportEvents, _timeProvider.GetUtcNow().UtcDateTime);
        var tempPath = BuildTemporaryPath(savePath);

        try
        {
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(tempPath, icsContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
            File.Move(tempPath, savePath, overwrite: true);

            return new ExportResult(
                Success: true,
                WasCancelled: false,
                ExportedEventCount: exportEvents.Count,
                FileName: Path.GetFileName(savePath),
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            _logger.LogError(ex, "Failed to export ICS file to {Path}.", savePath);
            TryDeleteFile(tempPath);
            TryDeleteFile(savePath);

            return new ExportResult(
                Success: false,
                WasCancelled: false,
                ExportedEventCount: 0,
                FileName: Path.GetFileName(savePath),
                ErrorMessage: "Unable to export the ICS file. Check file permissions and available disk space, then try again.");
        }
    }

    public Task<(DateOnly From, DateOnly To)?> GetStoredEventRangeAsync(CancellationToken ct = default)
    {
        return _gcalEventRepository.GetStoredDateRangeAsync(ct);
    }

    private static bool IntersectsRange(GcalEvent calendarEvent, DateTime rangeStartUtc, DateTime rangeEndExclusiveUtc)
    {
        if (calendarEvent.IsDeleted || !calendarEvent.StartDatetime.HasValue)
        {
            return false;
        }

        var startUtc = NormalizeUtc(calendarEvent.StartDatetime.Value);
        var endUtc = NormalizeUtc(calendarEvent.EndDatetime ?? calendarEvent.StartDatetime.Value);
        return startUtc < rangeEndExclusiveUtc && endUtc > rangeStartUtc;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var tempFileName = $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp";
        return Path.Combine(directory, tempFileName);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup after a failed export.
        }
    }
}
