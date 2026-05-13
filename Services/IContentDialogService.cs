namespace GoogleCalendarManagement.Services;

public enum DeleteWithPendingEditChoice { Cancel, RevertChanges, DeleteEvent }

public interface IContentDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task ShowMessageAsync(string title, string message, string closeButtonText = "OK");
    Task ShowSelectableTextAsync(
        string title,
        string message,
        string closeButtonText = "Close",
        string copyButtonText = "Copy to clipboard");
    Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText = "Cancel");
    Task<DeleteWithPendingEditChoice> ShowDeleteWithPendingEditAsync(string eventTitle);
}
