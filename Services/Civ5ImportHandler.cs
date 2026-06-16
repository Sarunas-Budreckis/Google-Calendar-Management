namespace GoogleCalendarManagement.Services;

public sealed class Civ5ImportHandler : IDataSourceImportHandler
{
    private readonly ICiv5SaveScannerService _scannerService;
    private readonly IContentDialogService _dialogService;

    public Civ5ImportHandler(
        ICiv5SaveScannerService scannerService,
        IContentDialogService dialogService)
    {
        _scannerService = scannerService;
        _dialogService = dialogService;
    }

    public string SourceKey => Civ5SaveScannerService.SourceKey;

    public IDataPointProjector GetProjector() => new Civ5Projector();

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        var result = await _scannerService.ScanAsync(ct);
        if (result.Success)
        {
            var message =
                $"Detected {result.SavesDetected} Civilization 5 save files. " +
                $"Added {result.NewPointsAdded} new save points to the database.";
            await _dialogService.ShowMessageAsync("Civilization 5 Import", message, "OK");
            return;
        }

        await _dialogService.ShowErrorAsync(
            "Civilization 5 Import",
            result.ErrorMessage ?? "Unable to scan Civilization 5 save files.");
    }
}
