namespace GoogleCalendarManagement.Services;

public interface IContentDialogService
{
    Task ShowErrorAsync(string title, string message);
}
