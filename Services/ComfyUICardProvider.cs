using System.Collections.Concurrent;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class ComfyUICardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IComfyUIRepository _repository;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public ComfyUICardProvider(IServiceProvider serviceProvider, IComfyUIRepository repository)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
    }

    public string SourceKey => ComfyUIFolderScannerService.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var points = await _repository.GetPointsForDateAsync(date, ct);
        _hasDataByDate[date] = points.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetCreatedEventCountsForRangeAsync(from, to, ct);
        var result = new List<DataSourceDayData>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            counts.TryGetValue(date, out var count);
            result.Add(new DataSourceDayData(date, count > 0, count > 0 ? count : null));
        }

        return result;
    }

    public UIElement? CreateCompactSummaryView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<ComfyUICompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<ComfyUIDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }

}
