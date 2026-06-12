using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GoogleCalendarManagement.Services;

public sealed class TogglCsvImportHandler : IDataSourceImportHandler
{
    private readonly ITogglCsvImportService _importService;
    private readonly IContentDialogService _dialogService;
    private readonly IWindowService _windowService;

    public TogglCsvImportHandler(
        ITogglCsvImportService importService,
        IContentDialogService dialogService,
        IWindowService windowService)
    {
        _importService = importService;
        _dialogService = dialogService;
        _windowService = windowService;
    }

    public string SourceKey => TogglSleepImportService.SourceKey;

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        var window = _windowService.GetWindow();
        if (window is null)
        {
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        Windows.Storage.StorageFile? file;
        try
        {
            file = await picker.PickSingleFileAsync();
        }
        catch (COMException)
        {
            return;
        }

        if (file is null)
        {
            return;
        }

        TogglCsvImportResult result;
        try
        {
            using var stream = await file.OpenStreamForReadAsync();
            result = await _importService.ImportFromStreamAsync(stream, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogService.ShowErrorAsync("Toggl CSV Import", $"Unable to read the selected file: {ex.Message}");
            return;
        }

        if (result.Success)
        {
            var parts = new List<string>();
            if (result.Inserted > 0)
            {
                parts.Add($"{result.Inserted} new entries imported");
            }

            if (result.Skipped > 0)
            {
                parts.Add($"{result.Skipped} already in database");
            }

            if (result.Malformed > 0)
            {
                parts.Add($"{result.Malformed} rows skipped (malformed)");
            }

            var message = parts.Count > 0
                ? string.Join(", ", parts) + "."
                : "No entries found in the selected file.";

            await _dialogService.ShowMessageAsync("Toggl CSV Import", message, "OK");
        }
        else
        {
            await _dialogService.ShowErrorAsync("Toggl CSV Import", result.ErrorMessage ?? "Import failed.");
        }
    }
}
