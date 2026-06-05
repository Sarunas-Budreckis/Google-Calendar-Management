namespace GoogleCalendarManagement.Services;

public sealed record Civ5ScanResult(bool Success, int NewPointsAdded, string? ErrorMessage);

public interface ICiv5SaveScannerService
{
    Task<Civ5ScanResult> ScanAsync(CancellationToken ct = default);
}
