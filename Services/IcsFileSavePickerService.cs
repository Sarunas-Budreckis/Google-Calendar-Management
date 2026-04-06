using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GoogleCalendarManagement.Services;

public sealed class IcsFileSavePickerService : IIcsFileSavePickerService
{
    private readonly IWindowService _windowService;

    public IcsFileSavePickerService(IWindowService windowService)
    {
        _windowService = windowService;
    }

    public async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        var window = _windowService.GetWindow();
        if (window is null)
        {
            return null;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add("iCalendar file", [".ics"]);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
