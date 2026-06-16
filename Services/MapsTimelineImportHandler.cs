using System.Diagnostics;
using System.Runtime.InteropServices;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GoogleCalendarManagement.Services;

public sealed class MapsTimelineImportHandler : IDataSourceImportHandler
{
    public const string SourceKey = "maps_timeline";

    private const string ViewerDir = @"C:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Maps Viewer";
    private const string ViewerJsonFile = "Timeline.json";
    private const string ViewerHtmlFile = "timeline.html";

    private readonly IMapsTimelineRepository _repository;
    private readonly MapsTimelineParser _parser;
    private readonly MapsTimelineCardProvider _cardProvider;
    private readonly IContentDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly ILogger<MapsTimelineImportHandler> _logger;

    public MapsTimelineImportHandler(
        IMapsTimelineRepository repository,
        MapsTimelineParser parser,
        MapsTimelineCardProvider cardProvider,
        IContentDialogService dialogService,
        IWindowService windowService,
        ILogger<MapsTimelineImportHandler> logger)
    {
        _repository = repository;
        _parser = parser;
        _cardProvider = cardProvider;
        _dialogService = dialogService;
        _windowService = windowService;
        _logger = logger;
    }

    string IDataSourceImportHandler.SourceKey => SourceKey;

    public IDataPointProjector GetProjector() => new MapsTimelineProjector();

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        var filePath = await PickJsonFileAsync();
        if (filePath is null)
        {
            return;
        }

        var existing = await _repository.GetLatestAsync(ct);
        if (existing is not null)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Replace existing timeline data?",
                "This will delete the previous import. Continue?",
                "Replace",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            await _repository.DeleteAllAsync(ct);
        }

        string rawJson;
        long fileSizeBytes;
        try
        {
            rawJson = await File.ReadAllTextAsync(filePath, ct);
            fileSizeBytes = new FileInfo(filePath).Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read timeline file {FilePath}", filePath);
            await _dialogService.ShowErrorAsync("Import Failed", $"Could not read the file:\n{ex.Message}");
            return;
        }

        var (minDate, maxDate) = _parser.ExtractDateRange(rawJson);

        var record = new MapsTimelineRaw
        {
            ImportedAt = DateTime.UtcNow,
            FileName = Path.GetFileName(filePath),
            FileSizeBytes = fileSizeBytes,
            CoveredDateMin = minDate,
            CoveredDateMax = maxDate,
            RawJson = rawJson
        };

        try
        {
            await _repository.SaveAsync(record, ct);
            _cardProvider.InvalidateCache();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save timeline import");
            await _dialogService.ShowErrorAsync("Import Failed", $"Could not save the timeline:\n{ex.Message}");
            return;
        }

        var dateRangeText = (minDate, maxDate) switch
        {
            ({ } min, { } max) => $"\nDate range: {min:d} – {max:d}",
            _ => ""
        };

        await _dialogService.ShowMessageAsync(
            "Timeline Imported",
            $"Successfully imported {Path.GetFileName(filePath)}.{dateRangeText}\n\nUse \"Copy to Viewer & Open\" to view the timeline.",
            "OK");
    }

    public async Task CopyToViewerAndOpenAsync(MapsTimelineRaw record)
    {
        var destPath = Path.Combine(ViewerDir, ViewerJsonFile);
        var htmlPath = Path.Combine(ViewerDir, ViewerHtmlFile);

        try
        {
            Directory.CreateDirectory(ViewerDir);
            await File.WriteAllTextAsync(destPath, record.RawJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy timeline to viewer directory");
            await _dialogService.ShowErrorAsync("Copy Failed", $"Could not write to viewer directory:\n{ex.Message}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open timeline viewer");
            await _dialogService.ShowErrorAsync("Open Failed", $"Could not open the timeline viewer:\n{ex.Message}");
        }
    }

    private async Task<string?> PickJsonFileAsync()
    {
        var window = _windowService.GetWindow();
        if (window is null)
        {
            return null;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add(".json");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        try
        {
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex) when (ex is COMException or TaskCanceledException)
        {
            return null;
        }
    }
}
