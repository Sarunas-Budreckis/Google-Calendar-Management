using Microsoft.UI.Xaml.Controls;

namespace GoogleCalendarManagement.Services;

public sealed class ContentDialogService : IContentDialogService
{
    private readonly IWindowService _windowService;

    public ContentDialogService(IWindowService windowService)
    {
        _windowService = windowService;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }
}
