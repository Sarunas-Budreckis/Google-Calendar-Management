using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GoogleCalendarManagement.Services;

public sealed class CallLogImportHandler : IDataSourceImportHandler
{
    private readonly ICallLogImportService _importService;
    private readonly IContentDialogService _dialogService;
    private readonly IWindowService _windowService;

    public CallLogImportHandler(
        ICallLogImportService importService,
        IContentDialogService dialogService,
        IWindowService windowService)
    {
        _importService = importService;
        _dialogService = dialogService;
        _windowService = windowService;
    }

    public string SourceKey => CallLogImportService.SourceKey;

    public IDataPointProjector GetProjector() => new CallLogProjector();

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

        CallLogImportResult result;
        try
        {
            using var stream = await file.OpenStreamForReadAsync();
            result = await _importService.ImportFromStreamAsync(stream, file.Name, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogService.ShowErrorAsync("iOS Call Log Import", $"Unable to read the selected file: {ex.Message}");
            return;
        }

        if (result.Success)
        {
            var message = result.NewRecordsInserted == 0 && result.DuplicatesSkipped > 0
                ? $"All {result.DuplicatesSkipped} entries already imported (no new data)."
                : $"Imported {result.NewRecordsInserted} new call entries ({result.DuplicatesSkipped} duplicates skipped).";
            await _dialogService.ShowMessageAsync("iOS Call Log Import", message, "OK");
        }
        else
        {
            await _dialogService.ShowErrorAsync("iOS Call Log Import", result.ErrorMessage ?? "Import failed.");
        }
    }
}
