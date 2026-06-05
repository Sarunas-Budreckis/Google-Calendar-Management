using System.Collections.Concurrent;
using GoogleCalendarManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider
{
    public const string SourceKey = "toggl_phone";

    private readonly IServiceProvider _serviceProvider;
    private readonly ITogglPhoneRepository _repository;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public TogglPhoneCardProvider(
        IServiceProvider serviceProvider,
        ITogglPhoneRepository repository)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
    }

    string IDataSourceCardProvider.SourceKey => SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetPhoneEntriesForDateAsync(date, ct);
        _hasDataByDate[date] = entries.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetPhoneEntryCountsForRangeAsync(from, to, ct);
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
        var control = _serviceProvider.GetRequiredService<TogglPhoneCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<TogglPhoneDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }
}
