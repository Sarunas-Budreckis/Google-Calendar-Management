namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneImportHandler : IDataSourceImportHandler
{
    private readonly ITogglPhoneClassificationService _classificationService;
    private readonly IContentDialogService _dialogService;

    public TogglPhoneImportHandler(
        ITogglPhoneClassificationService classificationService,
        IContentDialogService dialogService)
    {
        _classificationService = classificationService;
        _dialogService = dialogService;
    }

    public string SourceKey => TogglPhoneCardProvider.SourceKey;

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        try
        {
            await _classificationService.ClassifyAllAsync(ct);
            await _dialogService.ShowMessageAsync(
                "Toggl Phone Import",
                "Re-classified Toggl entries using the active phone rules.",
                "OK");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(
                "Toggl Phone Import",
                $"Unable to re-classify Toggl phone entries: {ex.Message}");
        }
    }
}
