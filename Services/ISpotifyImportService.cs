namespace GoogleCalendarManagement.Services;

public interface ISpotifyImportService
{
    Task<SpotifyImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}
