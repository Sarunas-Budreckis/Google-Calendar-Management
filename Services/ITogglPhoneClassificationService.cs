namespace GoogleCalendarManagement.Services;

public interface ITogglPhoneClassificationService
{
    Task ClassifyAllAsync(CancellationToken ct = default);
}
