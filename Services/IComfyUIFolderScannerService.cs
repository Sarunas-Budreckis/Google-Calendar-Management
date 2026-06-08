namespace GoogleCalendarManagement.Services;

public sealed record ComfyUIScanResult(bool Success, int NewPointsAdded, int TotalPointsStored, string? ErrorMessage);

public interface IComfyUIFolderScannerService
{
    Task<ComfyUIScanResult> ScanAsync(CancellationToken ct = default);
    Task<ComfyUIScanResult> ScanAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
