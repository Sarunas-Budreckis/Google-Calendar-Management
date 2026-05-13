using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

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
        await ShowMessageAsync(title, message);
    }

    public async Task ShowMessageAsync(string title, string message, string closeButtonText = "OK")
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
            CloseButtonText = closeButtonText,
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }

    public async Task ShowSelectableTextAsync(
        string title,
        string message,
        string closeButtonText = "Close",
        string copyButtonText = "Copy to clipboard")
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var textBox = new TextBox
        {
            Text = message,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            SelectionHighlightColor = (Microsoft.UI.Xaml.Media.SolidColorBrush?)Application.Current.Resources["TextSelectionHighlightColorThemeBrush"],
            MinWidth = 640,
            MaxWidth = 820,
            MinHeight = 280,
            MaxHeight = 520
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            SecondaryButtonText = copyButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Secondary)
            {
                break;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(message);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
    }

    public async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText = "Cancel")
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<DeleteWithPendingEditChoice> ShowDeleteWithPendingEditAsync(string eventTitle)
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return DeleteWithPendingEditChoice.Cancel;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete Event",
            Content = $"\"{eventTitle}\" has pending edits. Choose an action:",
            PrimaryButtonText = "Delete Event",
            SecondaryButtonText = "Revert Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => DeleteWithPendingEditChoice.DeleteEvent,
            ContentDialogResult.Secondary => DeleteWithPendingEditChoice.RevertChanges,
            _ => DeleteWithPendingEditChoice.Cancel
        };
    }
}
