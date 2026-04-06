namespace GoogleCalendarManagement.Services;

public interface IIcsFileSavePickerService
{
    Task<string?> PickSavePathAsync(string suggestedFileName);
}
