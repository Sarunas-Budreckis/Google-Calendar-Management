using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class CallLogDrilldownViewModel : ObservableObject
{
    private readonly ICallLogRepository _repository;
    private readonly CallLogCardProvider _cardProvider;
    private bool _hasEntries;
    private DateOnly _currentDate;

    public CallLogDrilldownViewModel(
        ICallLogRepository repository,
        CallLogCardProvider cardProvider)
    {
        _repository = repository;
        _cardProvider = cardProvider;
        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasEntries);
    }

    public ObservableCollection<CallLogEntryViewModel> Entries { get; } = [];

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(EntriesVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility EntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        var entries = await _repository.GetEntriesForDateAsync(date, ct);

        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(CallLogEntryViewModel.FromEntry(entry));
        }

        HasEntries = Entries.Count > 0;
    }

    private Task CreateCandidateEventsAsync() => _cardProvider.AddForDayAsync(_currentDate);
}
