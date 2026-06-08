using GoogleCalendarManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class MapsTimelineCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMapsTimelineRepository _repository;

    // Cached so we don't re-query the DB on every card render. Refreshed when a new import happens.
    private MapsTimelineRangeCache? _rangeCache;
    private bool _cacheDirty = true;

    public MapsTimelineCardProvider(IServiceProvider serviceProvider, IMapsTimelineRepository repository)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
    }

    public string SourceKey => MapsTimelineImportHandler.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);
    }

    public bool? HasDataForDay(DateOnly date)
    {
        if (_rangeCache is null)
        {
            return null;
        }

        return _rangeCache.CoversDate(date);
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);
        var result = new List<DataSourceDayData>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var hasData = _rangeCache?.CoversDate(date) ?? false;
            result.Add(new DataSourceDayData(date, hasData, null));
        }

        return result;
    }

    public UIElement? CreateCompactSummaryView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<MapsTimelineCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<MapsTimelineDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public async Task<(bool CoversDay, DateTime? ImportedAt)> GetDayMetadataAsync(DateOnly date, CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);
        if (_rangeCache is null)
        {
            return (false, null);
        }

        return (_rangeCache.CoversDate(date), _rangeCache.ImportedAt);
    }

    public void InvalidateCache()
    {
        _cacheDirty = true;
        _rangeCache = null;
    }

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        if (!_cacheDirty && _rangeCache is not null)
        {
            return;
        }

        var record = await _repository.GetLatestAsync(ct);
        _rangeCache = record is not null
            ? new MapsTimelineRangeCache(record.CoveredDateMin, record.CoveredDateMax, record.ImportedAt)
            : null;
        _cacheDirty = false;
    }

    private sealed record MapsTimelineRangeCache(DateOnly? MinDate, DateOnly? MaxDate, DateTime ImportedAt)
    {
        public bool CoversDate(DateOnly date)
        {
            if (MinDate is null || MaxDate is null)
            {
                return false;
            }

            return date >= MinDate && date <= MaxDate;
        }
    }
}
