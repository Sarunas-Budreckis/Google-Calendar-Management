using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.ViewModels;

public sealed class OutlookDrilldownViewModel : ObservableObject
{
    private readonly IOutlookEventRepository _repository;
    private readonly IRuleEngineService _ruleEngine;
    private IReadOnlyList<OutlookEventItemViewModel> _items = [];
    private string _summaryLabel = "";
    private Visibility _dataVisibility = Visibility.Collapsed;
    private Visibility _emptyStateVisibility = Visibility.Visible;
    private DateOnly _currentDate;

    public OutlookDrilldownViewModel(IOutlookEventRepository repository, IRuleEngineService ruleEngine)
    {
        _repository = repository;
        _ruleEngine = ruleEngine;
    }

    public IReadOnlyList<OutlookEventItemViewModel> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public string SummaryLabel
    {
        get => _summaryLabel;
        private set => SetProperty(ref _summaryLabel, value);
    }

    public Visibility DataVisibility
    {
        get => _dataVisibility;
        private set => SetProperty(ref _dataVisibility, value);
    }

    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        private set => SetProperty(ref _emptyStateVisibility, value);
    }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        await RefreshAsync(ct);
    }

    public async Task ToggleSuppressAsync(string outlookEventId, bool suppress, CancellationToken ct = default)
    {
        await _repository.SetSuppressedAsync(outlookEventId, suppress, ct);
        await _ruleEngine.RunForImportAsync(OutlookImportService.SourceKey, ct);
        await RefreshAsync(ct);
    }

    private async Task RefreshAsync(CancellationToken ct = default)
    {
        var events = await _repository.GetEventsForDateAsync(_currentDate, ct);

        if (events.Count == 0)
        {
            Items = [];
            DataVisibility = Visibility.Collapsed;
            EmptyStateVisibility = Visibility.Visible;
            return;
        }

        var visibleCount = events.Count(e => !e.IsSuppressed);
        var totalMinutes = events
            .Where(e => !e.IsSuppressed && !e.IsAllDay)
            .Sum(e => (int)(e.EndDatetime - e.StartDatetime).TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        var timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";

        SummaryLabel = visibleCount > 0
            ? $"{visibleCount} work event{(visibleCount == 1 ? "" : "s")} · {timeStr}"
            : $"{events.Count} event{(events.Count == 1 ? "" : "s")} (all hidden)";

        Items = events.Select(e => new OutlookEventItemViewModel(e, this)).ToList();
        DataVisibility = Visibility.Visible;
        EmptyStateVisibility = Visibility.Collapsed;
    }
}

public sealed class OutlookEventItemViewModel : ObservableObject
{
    private readonly OutlookDrilldownViewModel _parent;
    private bool _isSuppressed;

    public OutlookEventItemViewModel(OutlookEvent ev, OutlookDrilldownViewModel parent)
    {
        _parent = parent;
        OutlookEventId = ev.OutlookEventId;
        Subject = ev.Subject;
        _isSuppressed = ev.IsSuppressed;
        IsAllDay = ev.IsAllDay;
        IsRecurring = ev.IsRecurring;
        Organizer = ev.Organizer;
        Location = ev.Location;
        BodyPreview = ev.BodyPreview;
        OrganizerVisibility = string.IsNullOrWhiteSpace(ev.Organizer) ? Visibility.Collapsed : Visibility.Visible;
        LocationVisibility = string.IsNullOrWhiteSpace(ev.Location) ? Visibility.Collapsed : Visibility.Visible;

        var startLocal = DateTime.SpecifyKind(ev.StartDatetime, DateTimeKind.Utc).ToLocalTime();
        var endLocal = DateTime.SpecifyKind(ev.EndDatetime, DateTimeKind.Utc).ToLocalTime();

        if (ev.IsAllDay)
        {
            TimeLabel = "All day";
        }
        else
        {
            var durationMinutes = (int)(ev.EndDatetime - ev.StartDatetime).TotalMinutes;
            var durationStr = durationMinutes >= 60
                ? $"{durationMinutes / 60}h{(durationMinutes % 60 > 0 ? $" {durationMinutes % 60}m" : "")}"
                : $"{durationMinutes}m";
            TimeLabel = $"{startLocal:HH:mm}–{endLocal:HH:mm} ({durationStr})";
        }

        ToggleSuppressCommand = new AsyncRelayCommand(ToggleSuppressAsync);
    }

    public string OutlookEventId { get; }
    public string Subject { get; }
    public string TimeLabel { get; }
    public bool IsAllDay { get; }
    public bool IsRecurring { get; }
    public string? Organizer { get; }
    public string? Location { get; }
    public string? BodyPreview { get; }
    public IAsyncRelayCommand ToggleSuppressCommand { get; }
    public Visibility OrganizerVisibility { get; }
    public Visibility LocationVisibility { get; }

    public bool IsSuppressed
    {
        get => _isSuppressed;
        private set
        {
            if (SetProperty(ref _isSuppressed, value))
            {
                OnPropertyChanged(nameof(SuppressButtonLabel));
                OnPropertyChanged(nameof(SubjectDisplay));
                OnPropertyChanged(nameof(ItemOpacity));
            }
        }
    }

    public string SubjectDisplay => IsSuppressed ? $"[Hidden] {Subject}" : Subject;
    public double ItemOpacity => IsSuppressed ? 0.45 : 1.0;
    public string SuppressButtonLabel => IsSuppressed ? "Show" : "Hide";

    private async Task ToggleSuppressAsync(CancellationToken ct)
    {
        await _parent.ToggleSuppressAsync(OutlookEventId, !IsSuppressed, ct);
    }
}
