using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class SpotifyCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISpotifyStreamRepository _repository;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public SpotifyCardProvider(IServiceProvider serviceProvider, ISpotifyStreamRepository repository)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
    }

    public string SourceKey => SpotifyImportService.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var streams = await _repository.GetStreamsForDateAsync(date, ct);
        _hasDataByDate[date] = streams.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetStreamCountsForRangeAsync(from, to, ct);
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
        var control = _serviceProvider.GetRequiredService<Views.SpotifyCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<Views.SpotifyDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }
}
