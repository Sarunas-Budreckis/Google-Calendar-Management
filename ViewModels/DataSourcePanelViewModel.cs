using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourcePanelViewModel : ObservableObject
{
    private const string MinimizedStateKey = "DataSourcePanelMinimized";
    private const string PanelWidthStateKey = "DataSourcePanelWidth";
    public const double DefaultPanelWidth = 240.0;

    private readonly ISystemStateRepository _systemStateRepository;
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly DataSourceImportHandlerRegistry _importHandlerRegistry;
    private readonly ICalendarDaySelectionService _daySelectionService;
    private readonly TimeProvider _timeProvider;
    private readonly DataSourceCardProviderRegistry _cardProviderRegistry;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly ICalendarViewRangeProvider _viewRangeProvider;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _isMinimized;
    private bool _isLoadingGlobal;
    private bool _isGlobalMode = true;
    private bool _sourceDataInViewIsExpanded = true;
    private bool _otherSourcesIsExpanded = true;
    private double _panelWidth = double.NaN;
    private DateOnly? _currentDay;
    private string _dayLabel = "";
    private string? _dayName;
    private DataSourceDayCardViewModel? _drilldownCard;
    private string? _pendingDrilldownSourceKey;

    public DataSourcePanelViewModel(
        ISystemStateRepository systemStateRepository,
        IDataSourceRepository dataSourceRepository,
        DataSourceImportHandlerRegistry importHandlerRegistry,
        ICalendarDaySelectionService daySelectionService,
        TimeProvider timeProvider,
        DataSourceCardProviderRegistry cardProviderRegistry,
        ICalendarSelectionService calendarSelectionService,
        IPendingEventDraftService pendingEventDraftService,
        IGcalEventRepository gcalEventRepository,
        IPendingEventRepository pendingEventRepository,
        ICalendarViewRangeProvider viewRangeProvider)
    {
        _systemStateRepository = systemStateRepository;
        _dataSourceRepository = dataSourceRepository;
        _importHandlerRegistry = importHandlerRegistry;
        _daySelectionService = daySelectionService;
        _timeProvider = timeProvider;
        _cardProviderRegistry = cardProviderRegistry;
        _calendarSelectionService = calendarSelectionService;
        _pendingEventDraftService = pendingEventDraftService;
        _gcalEventRepository = gcalEventRepository;
        _pendingEventRepository = pendingEventRepository;
        _viewRangeProvider = viewRangeProvider;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        OpenDayNameHeaderCommand = new AsyncRelayCommand(OpenSelectedDayNameEventAsync, () => CurrentDay.HasValue);
        BackFromDrilldownCommand = new RelayCommand(() => DrilldownCard = null);
        ToggleSourceDataInViewCommand = new RelayCommand(() => SourceDataInViewIsExpanded = !SourceDataInViewIsExpanded);
        ToggleOtherSourcesCommand = new RelayCommand(() => OtherSourcesIsExpanded = !OtherSourcesIsExpanded);

        WeakReferenceMessenger.Default.Register<DataSourcePanelViewModel, DataSourceImportCompletedMessage>(
            this,
            static (recipient, _) => recipient.ReloadSourcesOnUiThread());
        WeakReferenceMessenger.Default.Register<DataSourcePanelViewModel, DaySelectedMessage>(
            this,
            static (recipient, message) => recipient.ApplySelectedDay(message.SelectedDay));
        WeakReferenceMessenger.Default.Register<DataSourcePanelViewModel, CalendarViewRangeChangedMessage>(
            this,
            static (recipient, _) => recipient.ReloadSourcesForCurrentViewOnUiThread());
        WeakReferenceMessenger.Default.Register<DataSourcePanelViewModel, DataSourceDayOpenRequestedMessage>(
            this,
            static (recipient, message) => recipient.RequestDaySourceDrilldown(message));
    }

    public ObservableCollection<DataSourceSummaryViewModel> Sources { get; } = [];

    public ObservableCollection<DataSourceSummaryViewModel> SourceDataInViewSources { get; } = [];

    public ObservableCollection<DataSourceSummaryViewModel> OtherSources { get; } = [];

    public ObservableCollection<DataSourceDayCardViewModel> DayCards { get; } = [];

    public void MoveDayCard(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= DayCards.Count || newIndex < 0 || newIndex >= DayCards.Count || oldIndex == newIndex)
        {
            return;
        }

        DayCards.Move(oldIndex, newIndex);
    }

    public IAsyncRelayCommand OpenDayNameHeaderCommand { get; }

    public IRelayCommand BackFromDrilldownCommand { get; }

    public IRelayCommand ToggleSourceDataInViewCommand { get; }

    public IRelayCommand ToggleOtherSourcesCommand { get; }

    public bool IsMinimized
    {
        get => _isMinimized;
        set
        {
            if (SetProperty(ref _isMinimized, value))
            {
                OnPropertyChanged(nameof(PanelBodyVisibility));
                OnPropertyChanged(nameof(RestoreTabVisibility));
                _ = _systemStateRepository.SetAsync(MinimizedStateKey, value ? "true" : "false");
            }
        }
    }

    public double PanelWidth
    {
        get => _panelWidth;
        set
        {
            if (SetProperty(ref _panelWidth, value))
            {
                if (!double.IsNaN(value))
                    _ = _systemStateRepository.SetAsync(PanelWidthStateKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    public Visibility PanelBodyVisibility => _isMinimized ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RestoreTabVisibility => _isMinimized ? Visibility.Visible : Visibility.Collapsed;

    public bool IsLoadingGlobal
    {
        get => _isLoadingGlobal;
        private set
        {
            if (SetProperty(ref _isLoadingGlobal, value))
            {
                OnPropertyChanged(nameof(LoadingGlobalVisibility));
                OnPropertyChanged(nameof(EmptyGlobalStateVisibility));
                OnPropertyChanged(nameof(SourceListVisibility));
                OnPropertyChanged(nameof(SourceDataInViewVisibility));
                OnPropertyChanged(nameof(SourceDataInViewListVisibility));
                OnPropertyChanged(nameof(OtherSourcesVisibility));
                OnPropertyChanged(nameof(OtherSourcesListVisibility));
            }
        }
    }

    public bool IsGlobalMode
    {
        get => _isGlobalMode;
        private set
        {
            if (SetProperty(ref _isGlobalMode, value))
            {
                OnPropertyChanged(nameof(GlobalModeVisibility));
                OnPropertyChanged(nameof(DayModePlaceholderVisibility));
                OnPropertyChanged(nameof(DayModeVisibility));
                OnPropertyChanged(nameof(DayModeSourceListVisibility));
                OnPropertyChanged(nameof(DayModeDrilldownVisibility));
                OnPropertyChanged(nameof(LoadingGlobalVisibility));
                OnPropertyChanged(nameof(EmptyGlobalStateVisibility));
                OnPropertyChanged(nameof(SourceListVisibility));
                OnPropertyChanged(nameof(SourceDataInViewVisibility));
                OnPropertyChanged(nameof(SourceDataInViewListVisibility));
                OnPropertyChanged(nameof(OtherSourcesVisibility));
                OnPropertyChanged(nameof(OtherSourcesListVisibility));
            }
        }
    }

    public Visibility GlobalModeVisibility => IsGlobalMode ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DayModePlaceholderVisibility => Visibility.Collapsed;

    public Visibility DayModeVisibility => IsGlobalMode ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DayModeSourceListVisibility =>
        !IsGlobalMode && DrilldownCard is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DayModeDrilldownVisibility =>
        !IsGlobalMode && DrilldownCard is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingGlobalVisibility => IsGlobalMode && IsLoadingGlobal ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyGlobalStateVisibility =>
        IsGlobalMode && !IsLoadingGlobal && Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SourceListVisibility =>
        IsGlobalMode && !IsLoadingGlobal && Sources.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool SourceDataInViewIsExpanded
    {
        get => _sourceDataInViewIsExpanded;
        private set
        {
            if (SetProperty(ref _sourceDataInViewIsExpanded, value))
            {
                OnPropertyChanged(nameof(SourceDataInViewListVisibility));
                OnPropertyChanged(nameof(SourceDataInViewChevronGlyph));
            }
        }
    }

    public string SourceDataInViewHeader => $"Source data in view ({SourceDataInViewSources.Count})";

    public string SourceDataInViewChevronGlyph => SourceDataInViewIsExpanded ? "" : "";

    public Visibility SourceDataInViewVisibility =>
        IsGlobalMode && !IsLoadingGlobal ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SourceDataInViewListVisibility =>
        IsGlobalMode && !IsLoadingGlobal && SourceDataInViewIsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public bool OtherSourcesIsExpanded
    {
        get => _otherSourcesIsExpanded;
        private set
        {
            if (SetProperty(ref _otherSourcesIsExpanded, value))
            {
                OnPropertyChanged(nameof(OtherSourcesListVisibility));
                OnPropertyChanged(nameof(OtherSourcesChevronGlyph));
            }
        }
    }

    public string OtherSourcesHeader => $"Other data sources ({OtherSources.Count})";

    public string OtherSourcesChevronGlyph => OtherSourcesIsExpanded ? "" : "";

    public Visibility OtherSourcesVisibility =>
        IsGlobalMode && !IsLoadingGlobal ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OtherSourcesListVisibility =>
        IsGlobalMode && !IsLoadingGlobal && OtherSourcesIsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public DateOnly? CurrentDay
    {
        get => _currentDay;
        private set
        {
            if (SetProperty(ref _currentDay, value))
            {
                OpenDayNameHeaderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string DayLabel
    {
        get => _dayLabel;
        private set => SetProperty(ref _dayLabel, value);
    }

    public string? DayName
    {
        get => _dayName;
        private set
        {
            if (SetProperty(ref _dayName, value))
            {
                OnPropertyChanged(nameof(HasDayName));
                OnPropertyChanged(nameof(DayNameOrHint));
                OnPropertyChanged(nameof(DayNameVisibility));
                OnPropertyChanged(nameof(DayNameHintVisibility));
            }
        }
    }

    public bool HasDayName => !string.IsNullOrWhiteSpace(DayName);

    public string DayNameOrHint => HasDayName ? DayName! : "Tap to name this day";

    public Visibility DayNameVisibility => HasDayName ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DayNameHintVisibility => HasDayName ? Visibility.Collapsed : Visibility.Visible;

    public DataSourceDayCardViewModel? DrilldownCard
    {
        get => _drilldownCard;
        private set
        {
            if (SetProperty(ref _drilldownCard, value))
            {
                OnPropertyChanged(nameof(DayModeSourceListVisibility));
                OnPropertyChanged(nameof(DayModeDrilldownVisibility));
            }
        }
    }

    public async Task InitializeAsync()
    {
        var stored = await _systemStateRepository.GetAsync(MinimizedStateKey);
        IsMinimized = stored == "true";

        var storedWidth = await _systemStateRepository.GetAsync(PanelWidthStateKey);
        if (storedWidth is not null && double.TryParse(storedWidth, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var width) && !double.IsNaN(width))
        {
            _panelWidth = Math.Clamp(width, 160.0, 600.0);
            OnPropertyChanged(nameof(PanelWidth));
        }

        ApplySelectedDay(_daySelectionService.SelectedDay);
        if (IsGlobalMode)
        {
            await LoadSourcesAsync();
        }
    }

    public async Task LoadDayModeAsync(DateOnly date, CancellationToken ct = default)
    {
        var previousDrilldownSourceKey = _pendingDrilldownSourceKey ?? DrilldownCard?.SourceKey;
        _pendingDrilldownSourceKey = null;
        CurrentDay = date;
        IsGlobalMode = false;
        DrilldownCard = null;
        DayLabel = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMMM d", CultureInfo.CurrentCulture);

        var dayNameEvent = await GetDayNameEventAsync(date, ct);
        DayName = dayNameEvent?.Summary;

        DayCards.Clear();
        var sources = await _dataSourceRepository.GetAllSourcesAsync(ct);
        foreach (var source in sources.OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var integration = await _dataSourceRepository.GetIntegrationAsync(date, source.DataSourceId, ct);
            var provider = _cardProviderRegistry.GetProvider(source.SourceKey);
            if (provider is IDataSourceCardProviderPreloader preloader)
            {
                await preloader.PreloadAsync(date, ct);
            }

            var hasData = provider?.HasDataForDay(date);
            var isGreyedOut = hasData == false;
            var compactSummaryView = provider?.CreateCompactSummaryView(date);
            Func<UIElement> drilldownViewFactory = provider is null
                ? () => DataSourceDayCardViewModel.CreatePlaceholderDrilldown(source.DisplayName)
                : () => provider.CreateDrilldownView(date);
            Func<Task>? addAction = provider is IDataSourceDayActionProvider actionProvider
                ? () => actionProvider.AddForDayAsync(date)
                : null;
            var addButtonContent = provider is ComfyUICardProvider ? "Import" : "Add";
            var allowAddWhenGreyedOut = provider is ComfyUICardProvider;

            DayCards.Add(new DataSourceDayCardViewModel(
                source.DataSourceId,
                source.SourceKey,
                source.DisplayName,
                integration?.Integrated == true,
                isGreyedOut,
                date,
                _dataSourceRepository,
                card => DrilldownCard = card,
                compactSummaryView,
                drilldownViewFactory,
                addAction,
                addButtonContent,
                allowAddWhenGreyedOut));
        }

        if (previousDrilldownSourceKey is not null)
        {
            DrilldownCard = DayCards.FirstOrDefault(c => SourceKeysMatch(c.SourceKey, previousDrilldownSourceKey));
        }

        OnPropertyChanged(nameof(DayModeSourceListVisibility));
    }

    public async Task OpenOrCreateDayNameEventAsync(DateOnly date, CancellationToken ct = default)
    {
        var existing = await GetDayNameEventAsync(date, ct);
        if (existing is not null)
        {
            _calendarSelectionService.Select(existing.EventId, existing.SourceKind);
            return;
        }

        var dayStartLocal = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var draft = await _pendingEventDraftService.CreateDraftAsync(dayStartLocal, dayStartLocal, "", ct);
        draft.Summary = "";
        draft.IsAllDay = true;
        draft.SourceSystem = "day_name";
        draft.StartDatetime = dayStartLocal.ToUniversalTime();
        draft.EndDatetime = dayStartLocal.ToUniversalTime();
        draft.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _pendingEventRepository.UpsertAsync(draft, ct);
        DayName = draft.Summary;
        _calendarSelectionService.Select(draft.PendingEventId, CalendarEventSourceKind.Pending, openInEditMode: true);
    }

    public async Task LoadSourcesAsync(CancellationToken ct = default)
    {
        IsLoadingGlobal = true;
        Sources.Clear();
        SourceDataInViewSources.Clear();
        OtherSources.Clear();
        OnSourceCollectionChanged();

        try
        {
            var sources = await _dataSourceRepository.GetAllSourcesAsync(ct);
            var summaries = new List<DataSourceSummaryViewModel>();
            var viewRange = _viewRangeProvider.GetCurrentViewDisplayRange();

            foreach (var source in sources.OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var lastImport = await _dataSourceRepository.GetLastImportAsync(source.DataSourceId, ct);
                summaries.Add(await CreateSummaryAsync(source, lastImport, viewRange.From, viewRange.To, ct));
            }

            var existingSourceKeys = sources
                .Select(source => source.SourceKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in _importHandlerRegistry.GetHandlers()
                         .Where(handler => !existingSourceKeys.Contains(handler.SourceKey))
                         .OrderBy(handler => handler.SourceKey, StringComparer.CurrentCultureIgnoreCase))
            {
                summaries.Add(new DataSourceSummaryViewModel(
                    dataSourceId: 0,
                    sourceKey: handler.SourceKey,
                    displayName: FormatSourceKey(handler.SourceKey),
                    lastDataDateLabel: "Never imported",
                    lastImportedRelativeLabel: null,
                    handlerRegistry: _importHandlerRegistry));
            }

            Sources.Clear();
            foreach (var summary in summaries)
            {
                Sources.Add(summary);
                if (summary.HasDataInCurrentView)
                {
                    SourceDataInViewSources.Add(summary);
                }
                else
                {
                    OtherSources.Add(summary);
                }
            }
        }
        finally
        {
            IsLoadingGlobal = false;
            OnSourceCollectionChanged();
        }
    }

    private async Task<DataSourceSummaryViewModel> CreateSummaryAsync(
        DataSource source,
        DataSourceImportLog? lastImport,
        DateOnly viewFrom,
        DateOnly viewTo,
        CancellationToken ct)
    {
        var lastDataDateLabel = lastImport is null
            ? "Never imported"
            : lastImport.CoveredEndDate.ToDateTime(TimeOnly.MinValue).ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
        var lastImportedRelativeLabel = lastImport is null
            ? null
            : RelativeTimeFormatter.FormatElapsed(lastImport.ImportedAt, _timeProvider.GetLocalNow().DateTime);

        IReadOnlyList<DataSourceDayDataMarkerViewModel> dayDataMarkers = [];
        if (_cardProviderRegistry.GetProvider(source.SourceKey) is IDataSourceViewDataProvider viewDataProvider)
        {
            var dayData = await viewDataProvider.GetDataForRangeAsync(viewFrom, viewTo, ct);
            dayDataMarkers = dayData
                .Select(item => new DataSourceDayDataMarkerViewModel(
                    item.Date,
                    item.HasData,
                    item.Count,
                    date =>
                    {
                        WeakReferenceMessenger.Default.Send(new DataSourceDayOpenRequestedMessage(date, source.SourceKey));
                        return Task.CompletedTask;
                    }))
                .ToList();
        }

        return new DataSourceSummaryViewModel(
            source.DataSourceId,
            source.SourceKey,
            source.DisplayName,
            lastDataDateLabel,
            lastImportedRelativeLabel,
            _importHandlerRegistry,
            dayDataMarkers);
    }

    private static string FormatSourceKey(string sourceKey)
    {
        return string.Join(
            " ",
            sourceKey.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part)));
    }

    private void ReloadSourcesOnUiThread()
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            _ = ReloadSourcesAsync();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => _ = ReloadSourcesAsync());
    }

    private async Task ReloadSourcesAsync()
    {
        if (!IsGlobalMode && CurrentDay is { } date)
        {
            await LoadDayModeAsync(date);
            return;
        }

        await LoadSourcesAsync();
    }

    private void ReloadSourcesForCurrentViewOnUiThread()
    {
        if (!IsGlobalMode)
        {
            return;
        }

        ReloadSourcesOnUiThread();
    }

    private void ApplySelectedDay(DateOnly? selectedDay)
    {
        var wasGlobalMode = IsGlobalMode;
        if (selectedDay is null)
        {
            CurrentDay = null;
            DayLabel = "";
            DayName = null;
            DayCards.Clear();
            DrilldownCard = null;
            IsGlobalMode = true;

            if (!wasGlobalMode)
            {
                ReloadSourcesOnUiThread();
            }

            return;
        }

        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            _ = LoadDayModeAsync(selectedDay.Value);
            return;
        }

        _dispatcherQueue.TryEnqueue(() => _ = LoadDayModeAsync(selectedDay.Value));
    }

    private void RequestDaySourceDrilldown(DataSourceDayOpenRequestedMessage message)
    {
        _pendingDrilldownSourceKey = message.SourceKey;

        if (CurrentDay == message.Date && !IsGlobalMode)
        {
            DrilldownCard = DayCards.FirstOrDefault(c => SourceKeysMatch(c.SourceKey, message.SourceKey));
        }
    }

    private static bool SourceKeysMatch(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            || IsComfyUIKey(left) && IsComfyUIKey(right);
    }

    private static bool IsComfyUIKey(string sourceKey)
    {
        return string.Equals(sourceKey, "comfyui", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceKey, ComfyUIFolderScannerService.SourceKey, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSourceCollectionChanged()
    {
        OnPropertyChanged(nameof(EmptyGlobalStateVisibility));
        OnPropertyChanged(nameof(SourceListVisibility));
        OnPropertyChanged(nameof(SourceDataInViewVisibility));
        OnPropertyChanged(nameof(SourceDataInViewHeader));
        OnPropertyChanged(nameof(SourceDataInViewListVisibility));
        OnPropertyChanged(nameof(OtherSourcesVisibility));
        OnPropertyChanged(nameof(OtherSourcesHeader));
        OnPropertyChanged(nameof(OtherSourcesListVisibility));
    }

    private async Task OpenSelectedDayNameEventAsync()
    {
        if (CurrentDay is { } date)
        {
            await OpenOrCreateDayNameEventAsync(date);
        }
    }

    private async Task<DayNameEventReference?> GetDayNameEventAsync(DateOnly date, CancellationToken ct)
    {
        var pendingEvent = await _pendingEventRepository.GetDayNameEventAsync(date, ct);
        if (pendingEvent is not null)
        {
            return new DayNameEventReference(
                pendingEvent.PendingEventId,
                CalendarEventSourceKind.Pending,
                pendingEvent.Summary);
        }

        var gcalEvents = await _gcalEventRepository.GetByDateRangeAsync(date, date, ct);
        var gcalEvent = gcalEvents
            .Where(e => e.IsAllDay == true && string.Equals(e.SourceSystem, "day_name", StringComparison.Ordinal))
            .OrderByDescending(e => e.UpdatedAt)
            .FirstOrDefault();

        return gcalEvent is null
            ? null
            : new DayNameEventReference(gcalEvent.GcalEventId, CalendarEventSourceKind.Google, gcalEvent.Summary);
    }

    private sealed record DayNameEventReference(string EventId, CalendarEventSourceKind SourceKind, string? Summary);
}
