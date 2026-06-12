using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class OutlookCompactCardViewModel : ObservableObject
{
    private readonly IOutlookEventRepository _repository;
    private string _eventCountLabel = "";
    private string _workHoursLabel = "";
    private Visibility _dataVisibility = Visibility.Collapsed;
    private Visibility _noDataVisibility = Visibility.Visible;
    private Visibility _workHoursVisibility = Visibility.Collapsed;

    public OutlookCompactCardViewModel(IOutlookEventRepository repository)
    {
        _repository = repository;
    }

    public string EventCountLabel
    {
        get => _eventCountLabel;
        private set => SetProperty(ref _eventCountLabel, value);
    }

    public string WorkHoursLabel
    {
        get => _workHoursLabel;
        private set => SetProperty(ref _workHoursLabel, value);
    }

    public Visibility DataVisibility
    {
        get => _dataVisibility;
        private set => SetProperty(ref _dataVisibility, value);
    }

    public Visibility NoDataVisibility
    {
        get => _noDataVisibility;
        private set => SetProperty(ref _noDataVisibility, value);
    }

    public Visibility WorkHoursVisibility
    {
        get => _workHoursVisibility;
        private set => SetProperty(ref _workHoursVisibility, value);
    }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var events = await _repository.GetEventsForDateAsync(date, ct);
        var visibleEvents = events.Where(e => !e.IsSuppressed).ToList();

        if (visibleEvents.Count == 0)
        {
            DataVisibility = Visibility.Collapsed;
            NoDataVisibility = Visibility.Visible;
            return;
        }

        var totalMinutes = visibleEvents
            .Where(e => !e.IsAllDay)
            .Sum(e => (int)(e.EndDatetime - e.StartDatetime).TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        EventCountLabel = visibleEvents.Count == 1 ? "1 work event" : $"{visibleEvents.Count} work events";
        if (totalMinutes > 0)
        {
            WorkHoursLabel = hours > 0 ? $"{hours}h {minutes}m work" : $"{minutes}m work";
            WorkHoursVisibility = Visibility.Visible;
        }
        else
        {
            WorkHoursLabel = "";
            WorkHoursVisibility = Visibility.Collapsed;
        }

        DataVisibility = Visibility.Visible;
        NoDataVisibility = Visibility.Collapsed;
    }
}
