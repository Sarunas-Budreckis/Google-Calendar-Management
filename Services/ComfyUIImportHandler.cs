using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class ComfyUIImportHandler : IDataSourceImportHandler
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IComfyUIFolderScannerService _scannerService;
    private readonly IContentDialogService _dialogService;
    private readonly TimeProvider _timeProvider;

    public ComfyUIImportHandler(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IComfyUIFolderScannerService scannerService,
        IContentDialogService dialogService,
        TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _scannerService = scannerService;
        _dialogService = dialogService;
        _timeProvider = timeProvider;
    }

    public string SourceKey => ComfyUIFolderScannerService.SourceKey;

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        await EnsureDataSourceAsync(ct);
        var result = await _scannerService.ScanAsync(ct);

        if (result.Success)
        {
            var message =
                $"Stored data points: {result.TotalPointsStored}\n" +
                $"Imported data points: {result.NewPointsAdded}";
            await _dialogService.ShowMessageAsync("ComfyUI Import", message, "OK");
            return;
        }

        await _dialogService.ShowErrorAsync(
            "ComfyUI Import",
            result.ErrorMessage ?? "Unable to scan ComfyUI output folders.");
    }

    private async Task EnsureDataSourceAsync(CancellationToken ct)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await ctx.DataSources.SingleOrDefaultAsync(s => s.SourceKey == SourceKey, ct);
        if (existing is not null)
        {
            return;
        }

        var legacy = await ctx.DataSources.SingleOrDefaultAsync(s => s.SourceKey == "comfyui", ct);
        if (legacy is not null)
        {
            legacy.SourceKey = SourceKey;
            legacy.DisplayName = "ComfyUI";
            await ctx.SaveChangesAsync(ct);
            return;
        }

        ctx.DataSources.Add(new DataSource
        {
            SourceKey = SourceKey,
            DisplayName = "ComfyUI",
            SupportsNoDataHint = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await ctx.SaveChangesAsync(ct);
    }
}
