namespace GoogleCalendarManagement.Services;

public interface IErrorHandlingService
{
    void Register();
    void HandleCriticalError(Exception ex, string context);
}
