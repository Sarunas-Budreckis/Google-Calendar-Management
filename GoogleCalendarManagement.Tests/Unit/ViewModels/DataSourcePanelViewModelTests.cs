using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class DataSourcePanelViewModelTests : IDisposable
{
    public DataSourcePanelViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task LoadSources_WhenRepositoryReturnsEmpty_ShowsEmptyState()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        await viewModel.InitializeAsync();

        viewModel.Sources.Should().BeEmpty();
        viewModel.EmptyGlobalStateVisibility.Should().Be(Visibility.Visible);
        viewModel.SourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.IsLoadingGlobal.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSources_WhenRepositoryReturnsSources_BuildsSummaryViewModels()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 2, SourceKey = "toggl", DisplayName = "Toggl" },
                new DataSource { DataSourceId = 1, SourceKey = "oura", DisplayName = "Oura" }
            ],
            LastImports =
            {
                [1] = new DataSourceImportLog
                {
                    DataSourceId = 1,
                    CoveredEndDate = new DateOnly(2026, 05, 13),
                    ImportedAt = new DateTime(2026, 05, 15, 09, 00, 00, DateTimeKind.Utc),
                    Success = true
                }
            }
        };

        var viewModel = CreateViewModel(
            repository,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 05, 15, 12, 00, 00, TimeSpan.Zero)));

        await viewModel.InitializeAsync();

        viewModel.Sources.Select(source => source.DisplayName).Should().Equal("Oura", "Toggl");
        viewModel.Sources[0].LastDataDateLabel.Should().Be("May 13, 2026");
        viewModel.Sources[0].LastImportedRelativeLabel.Should().Be("3 hours ago");
        viewModel.Sources[1].LastDataDateLabel.Should().Be("Never imported");
        viewModel.Sources[1].LastImportedRelativeLabel.Should().BeNull();
        viewModel.EmptyGlobalStateVisibility.Should().Be(Visibility.Collapsed);
        viewModel.SourceListVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadSources_WhenHandlerRegistered_ImportCommandIsEnabled()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var registry = new DataSourceImportHandlerRegistry();
        var handler = new RecordingImportHandler("toggl");
        registry.Register(handler);
        var viewModel = CreateViewModel(repository, registry: registry);

        await viewModel.InitializeAsync();
        var source = viewModel.Sources.Single();

        source.HasImportHandler.Should().BeTrue();
        source.ImportCommand.CanExecute(null).Should().BeTrue();

        await source.ImportCommand.ExecuteAsync(null);

        handler.TriggerCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadSources_WhenNoHandlerRegistered_ImportCommandIsDisabled()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        var source = viewModel.Sources.Single();

        source.HasImportHandler.Should().BeFalse();
        source.ImportCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task OnImportCompletedMessage_ReloadsSourceList()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);
        await viewModel.InitializeAsync();
        repository.Sources =
        [
            new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" },
            new DataSource { DataSourceId = 2, SourceKey = "oura", DisplayName = "Oura" }
        ];

        WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(1, "toggl", true));
        await repository.WaitForGetAllSourcesCallsAsync(2);

        viewModel.Sources.Select(source => source.DisplayName).Should().Equal("Oura", "Toggl");
    }

    [Fact]
    public async Task DaySelectedMessage_WithSelectedDay_HidesGlobalSourceList()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var daySelectionService = new StubCalendarDaySelectionService { SelectedDay = null };
        var viewModel = CreateViewModel(repository, daySelectionService: daySelectionService);

        await viewModel.InitializeAsync();
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));

        viewModel.IsGlobalMode.Should().BeFalse();
        viewModel.GlobalModeVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task OnDaySelected_SwitchesToDayMode()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));

        viewModel.IsGlobalMode.Should().BeFalse();
        viewModel.CurrentDay.Should().Be(new DateOnly(2026, 05, 13));
        viewModel.DayLabel.Should().Be("Wednesday, May 13");
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
        viewModel.DayCards.Should().ContainSingle(card => card.DisplayName == "Toggl");
    }

    [Fact]
    public async Task OnDayDeselected_ReturnsTaGlobalMode()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(null));

        viewModel.IsGlobalMode.Should().BeTrue();
        viewModel.CurrentDay.Should().BeNull();
        viewModel.DayCards.Should().BeEmpty();
        viewModel.DrilldownCard.Should().BeNull();
    }

    [Fact]
    public async Task LoadDayMode_WhenNameEventExists_PopulatesDayName()
    {
        var repository = new StubDataSourceRepository();
        var pendingRepository = new StubPendingEventRepository
        {
            DayNameEvent = new PendingEvent
            {
                PendingEventId = "pending_day_name",
                Summary = "Deep Work",
                IsAllDay = true,
                SourceSystem = "day_name",
                StartDatetime = new DateTime(2026, 05, 13, 0, 0, 0, DateTimeKind.Utc)
            }
        };
        var viewModel = CreateViewModel(repository, pendingEventRepository: pendingRepository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        viewModel.DayName.Should().Be("Deep Work");
        viewModel.HasDayName.Should().BeTrue();
        viewModel.DayNameOrHint.Should().Be("Deep Work");
    }

    [Fact]
    public async Task LoadDayMode_WhenNoNameEvent_DayNameIsNull()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        viewModel.DayName.Should().BeNull();
        viewModel.HasDayName.Should().BeFalse();
        viewModel.DayNameOrHint.Should().Be("Tap to name this day");
    }

    [Fact]
    public async Task ToggleIntegration_CallsRepository_AndUpdatesCheckbox()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        var card = viewModel.DayCards.Single();
        await card.ToggleIntegrationCommand.ExecuteAsync(null);

        repository.SetIntegrationCalls.Should().ContainSingle();
        repository.SetIntegrationCalls[0].Should().Be((new DateOnly(2026, 05, 13), 7, true));
        card.IsIntegrated.Should().BeTrue();
    }

    [Fact]
    public async Task ExpandCard_SetsDrilldownCard()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        var card = viewModel.DayCards.Single();
        card.ExpandCommand.Execute(null);

        viewModel.DrilldownCard.Should().BeSameAs(card);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task BackFromDrilldown_ClearsDrilldownCard()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.DayCards.Single().ExpandCommand.Execute(null);
        viewModel.BackFromDrilldownCommand.Execute(null);

        viewModel.DrilldownCard.Should().BeNull();
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Collapsed);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    private static DataSourcePanelViewModel CreateViewModel(
        StubDataSourceRepository repository,
        DataSourceImportHandlerRegistry? registry = null,
        ICalendarDaySelectionService? daySelectionService = null,
        TimeProvider? timeProvider = null,
        DataSourceCardProviderRegistry? cardProviderRegistry = null,
        ICalendarSelectionService? calendarSelectionService = null,
        IPendingEventDraftService? pendingEventDraftService = null,
        IGcalEventRepository? gcalEventRepository = null,
        IPendingEventRepository? pendingEventRepository = null)
    {
        return new DataSourcePanelViewModel(
            new StubSystemStateRepository(),
            repository,
            registry ?? new DataSourceImportHandlerRegistry(),
            daySelectionService ?? new StubCalendarDaySelectionService(),
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 05, 15, 12, 00, 00, TimeSpan.Zero)),
            cardProviderRegistry ?? new DataSourceCardProviderRegistry(),
            calendarSelectionService ?? new StubCalendarSelectionService(),
            pendingEventDraftService ?? new StubPendingEventDraftService(),
            gcalEventRepository ?? new StubGcalEventRepository(),
            pendingEventRepository ?? new StubPendingEventRepository());
    }

    private sealed class StubSystemStateRepository : ISystemStateRepository
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            _values.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task SetManyAsync(IReadOnlyDictionary<string, string> pairs, CancellationToken ct = default)
        {
            foreach (var pair in pairs)
            {
                _values[pair.Key] = pair.Value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubDataSourceRepository : IDataSourceRepository
    {
        private readonly List<TaskCompletionSource> _getAllSourcesWaiters = [];

        public IReadOnlyList<DataSource> Sources { get; set; } = [];

        public Dictionary<int, DataSourceImportLog?> LastImports { get; } = [];

        public Dictionary<(DateOnly Date, int DataSourceId), bool> Integrations { get; } = [];

        public List<(DateOnly Date, int DataSourceId, bool Integrated)> SetIntegrationCalls { get; } = [];

        public int GetAllSourcesCallCount { get; private set; }

        public Task<IReadOnlyList<DataSource>> GetAllSourcesAsync(CancellationToken ct = default)
        {
            GetAllSourcesCallCount++;
            foreach (var waiter in _getAllSourcesWaiters.ToArray())
            {
                waiter.TrySetResult();
            }

            return Task.FromResult(Sources);
        }

        public Task<DataSource?> GetSourceByKeyAsync(string sourceKey, CancellationToken ct = default)
            => Task.FromResult(Sources.SingleOrDefault(source => source.SourceKey == sourceKey));

        public Task<DataSource> UpsertSourceAsync(DataSource source, CancellationToken ct = default)
            => Task.FromResult(source);

        public Task<DateSourceIntegration?> GetIntegrationAsync(DateOnly date, int dataSourceId, CancellationToken ct = default)
        {
            if (!Integrations.TryGetValue((date, dataSourceId), out var integrated))
            {
                return Task.FromResult<DateSourceIntegration?>(null);
            }

            return Task.FromResult<DateSourceIntegration?>(new DateSourceIntegration
            {
                Date = date,
                DataSourceId = dataSourceId,
                Integrated = integrated
            });
        }

        public Task<DateSourceIntegration> SetIntegrationAsync(DateOnly date, int dataSourceId, bool integrated, CancellationToken ct = default)
        {
            SetIntegrationCalls.Add((date, dataSourceId, integrated));
            Integrations[(date, dataSourceId)] = integrated;
            return Task.FromResult(new DateSourceIntegration
            {
                Date = date,
                DataSourceId = dataSourceId,
                Integrated = integrated
            });
        }

        public Task<DataSourceImportLog?> GetLastImportAsync(int dataSourceId, CancellationToken ct = default)
        {
            LastImports.TryGetValue(dataSourceId, out var log);
            return Task.FromResult(log);
        }

        public Task<DataSourceImportLog> AddImportLogAsync(DataSourceImportLog log, CancellationToken ct = default)
            => Task.FromResult(log);

        public async Task WaitForGetAllSourcesCallsAsync(int expectedCallCount)
        {
            while (GetAllSourcesCallCount < expectedCallCount)
            {
                var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _getAllSourcesWaiters.Add(waiter);
                await waiter.Task;
            }
        }
    }

    private sealed class StubCalendarDaySelectionService : ICalendarDaySelectionService
    {
        public DateOnly? SelectedDay { get; set; }
        public DateOnly? ManuallySelectedDay { get; set; }
        public void SelectDay(DateOnly date) => SelectedDay = date;
        public void AutoSelectDay(DateOnly date) => SelectedDay = date;
        public void RestoreManualSelection() => SelectedDay = ManuallySelectedDay;
        public void ClearSelection() => SelectedDay = null;
    }

    private sealed class StubCalendarSelectionService : ICalendarSelectionService
    {
        public string? SelectedEventId { get; private set; }
        public CalendarEventSourceKind? SelectedSourceKind { get; private set; }
        public bool LastOpenInEditMode { get; private set; }

        public void Select(string eventId, CalendarEventSourceKind sourceKind, bool openInEditMode = false)
        {
            SelectedEventId = eventId;
            SelectedSourceKind = sourceKind;
            LastOpenInEditMode = openInEditMode;
        }

        public void ClearSelection()
        {
            SelectedEventId = null;
            SelectedSourceKind = null;
        }
    }

    private sealed class StubPendingEventDraftService : IPendingEventDraftService
    {
        public Task<PendingEvent> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
            => Task.FromResult(new PendingEvent
            {
                PendingEventId = "pending_new",
                Summary = summary,
                StartDatetime = startLocal,
                EndDatetime = endLocal
            });
    }

    private sealed class StubGcalEventRepository : IGcalEventRepository
    {
        public IList<GcalEvent> Events { get; set; } = [];

        public Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(Events);

        public Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult(Events.SingleOrDefault(gcalEvent => gcalEvent.GcalEventId == gcalEventId));

        public Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
            => Task.FromResult<(DateOnly From, DateOnly To)?>(null);
    }

    private sealed class StubPendingEventRepository : IPendingEventRepository
    {
        public PendingEvent? DayNameEvent { get; set; }

        public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent?.PendingEventId == pendingEventId ? DayNameEvent : null);

        public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent?.GcalEventId == gcalEventId ? DayNameEvent : null);

        public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent);

        public Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
        {
            DayNameEvent = pendingEvent;
            return Task.CompletedTask;
        }

        public Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingImportHandler : IDataSourceImportHandler
    {
        public RecordingImportHandler(string sourceKey)
        {
            SourceKey = sourceKey;
        }

        public string SourceKey { get; }

        public int TriggerCount { get; private set; }

        public Task TriggerImportAsync(CancellationToken ct = default)
        {
            TriggerCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
