using FluentAssertions;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class TogglSleepDrilldownViewModelTests
{
    public TogglSleepDrilldownViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task LoadAsync_WhenNoEntries_HasEntriesIsFalse()
    {
        var repository = new StubTogglSleepRepository();
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.HasEntries.Should().BeFalse();
        viewModel.EmptyStateVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
        viewModel.CreateCandidateEventCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenEntries_MapsToEntryViewModels()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries =
            [
                CreateEntry(
                    id: 1,
                    startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
                    endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc),
                    description: "Sleep")
            ]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.HasEntries.Should().BeTrue();
        viewModel.Entries.Should().ContainSingle();
        viewModel.Entries[0].DurationLabel.Should().Be("7h 45m");
        viewModel.Entries[0].Description.Should().Be("Sleep");
    }

    [Fact]
    public async Task LoadAsync_LoadsQualityFromRepository()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var qualityRepository = new StubTogglSleepQualityRepository { Quality = 7 };
        var viewModel = CreateViewModel(repository, qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.Quality.Should().Be(7);
    }

    [Fact]
    public async Task LoadAsync_WhenNoQualityStored_QualityIsNull()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.Quality.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenNoEntries_DoesNotLoadStoredQuality()
    {
        var qualityRepository = new StubTogglSleepQualityRepository { Quality = 7 };
        var viewModel = CreateViewModel(new StubTogglSleepRepository(), qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.Quality.Should().BeNull();
    }

    [Fact]
    public async Task SetQualityAsync_SavesQualityToRepository()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var qualityRepository = new StubTogglSleepQualityRepository();
        var viewModel = CreateViewModel(repository, qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.SetQualityAsync(8);

        viewModel.Quality.Should().Be(8);
        qualityRepository.UpsertedDate.Should().Be(new DateOnly(2026, 05, 13));
        qualityRepository.UpsertedQuality.Should().Be(8);
    }

    [Fact]
    public async Task SetQualityAsync_WhenPendingEventExists_UpdatesEventTitleToSleepWithRating()
    {
        var date = new DateOnly(2026, 05, 13);
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var existingEvent = new PendingEvent { PendingEventId = "pending_sleep_1", Summary = "Sleep" };
        var pendingRepository = new StubPendingEventRepository { SleepEventForDate = existingEvent };
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(date);
        await viewModel.SetQualityAsync(7);

        pendingRepository.UpsertedDraft.Should().NotBeNull();
        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep – 7/10");
    }

    [Fact]
    public async Task SetQualityAsync_WhenClearedAndPendingEventExists_RevertsEventTitleToSleep()
    {
        var date = new DateOnly(2026, 05, 13);
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var existingEvent = new PendingEvent { PendingEventId = "pending_sleep_1", Summary = "Sleep – 7/10" };
        var pendingRepository = new StubPendingEventRepository { SleepEventForDate = existingEvent };
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(date);
        await viewModel.SetQualityAsync(null);

        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep");
    }

    [Fact]
    public async Task SetQualityAsync_WhenCleared_StripsOnlyQualitySuffix()
    {
        var date = new DateOnly(2026, 05, 13);
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var existingEvent = new PendingEvent { PendingEventId = "pending_sleep_1", Summary = "Sleep recovery – 7/10" };
        var pendingRepository = new StubPendingEventRepository { SleepEventForDate = existingEvent };
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(date);
        await viewModel.SetQualityAsync(null);

        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep recovery");
    }

    [Fact]
    public async Task SetQualityAsync_WhenQualityOutOfRange_ThrowsAndDoesNotSave()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var qualityRepository = new StubTogglSleepQualityRepository();
        var viewModel = CreateViewModel(repository, qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        var act = async () => await viewModel.SetQualityAsync(11);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        qualityRepository.UpsertedDate.Should().BeNull();
    }

    [Fact]
    public async Task SetQualityAsync_WhenNoEntries_DoesNotSave()
    {
        var qualityRepository = new StubTogglSleepQualityRepository();
        var viewModel = CreateViewModel(new StubTogglSleepRepository(), qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.SetQualityAsync(7);

        qualityRepository.UpsertedDate.Should().BeNull();
    }

    [Fact]
    public async Task SetQualityAsync_WhenPendingEventUpdated_SendsEventUpdatedMessage()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var existingEvent = new PendingEvent { PendingEventId = "pending_sleep_1", Summary = "Sleep" };
        var pendingRepository = new StubPendingEventRepository { SleepEventForDate = existingEvent };
        EventUpdatedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<TogglSleepDrilldownViewModelTests, EventUpdatedMessage>(
            this,
            (_, msg) => receivedMessage = msg);
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.SetQualityAsync(5);

        receivedMessage.Should().NotBeNull();
        receivedMessage!.EventId.Should().Be("pending_sleep_1");
    }

    [Fact]
    public async Task SetQualityAsync_WhenNoPendingEventAndNoLinkedGcalEvent_NoEventUpdated()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries = [CreateEntry(id: 1, startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc))]
        };
        var pendingRepository = new StubPendingEventRepository();
        EventUpdatedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<TogglSleepDrilldownViewModelTests, EventUpdatedMessage>(
            this,
            (_, msg) => receivedMessage = msg);
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.SetQualityAsync(5);

        receivedMessage.Should().BeNull();
    }

    [Fact]
    public async Task SetQualityAsync_WhenLinkedGcalEventExists_CreatesOrUpdatesPendingEventWithTitle()
    {
        var linkedGcalEventId = "gcal_abc123";
        var gcalEvent = new GcalEvent
        {
            GcalEventId = linkedGcalEventId,
            CalendarId = "primary",
            Summary = "Sleep",
            StartDatetime = new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc),
            IsAllDay = false
        };
        var repository = new StubTogglSleepRepository
        {
            Entries =
            [
                CreateEntry(id: 1,
                    startUtc: new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc),
                    endUtc: new DateTime(2026, 05, 13, 12, 0, 0, DateTimeKind.Utc),
                    linkedEventId: linkedGcalEventId,
                    linkedEventType: "gcal")
            ]
        };
        var pendingRepository = new StubPendingEventRepository();
        var gcalRepository = new StubGcalEventRepository { EventById = gcalEvent };
        var viewModel = CreateViewModel(repository, pendingRepository: pendingRepository, gcalEventRepository: gcalRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.SetQualityAsync(7);

        pendingRepository.UpsertedDraft.Should().NotBeNull();
        pendingRepository.UpsertedDraft!.GcalEventId.Should().Be(linkedGcalEventId);
        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep – 7/10");
    }

    [Fact]
    public async Task CreateCandidateEventCommand_WhenOneEntry_CreatesDraftWithEntryTimes()
    {
        var entry = CreateEntry(
            id: 1,
            startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc));
        var repository = new StubTogglSleepRepository { Entries = [entry] };
        var draftService = new StubPendingEventDraftService();
        var pendingRepository = new StubPendingEventRepository();
        var viewModel = CreateViewModel(repository, draftService: draftService, pendingRepository: pendingRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        draftService.CreateCalls.Should().ContainSingle();
        draftService.CreateCalls[0].StartLocal.ToUniversalTime().Should().Be(entry.StartTime);
        draftService.CreateCalls[0].EndLocal.ToUniversalTime().Should().Be(entry.EndTime);
        pendingRepository.UpsertedDraft.Should().NotBeNull();
        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep");
        pendingRepository.UpsertedDraft.SourceSystem.Should().Be("toggl");
        pendingRepository.UpsertedDraft.ColorId.Should().Be("grey");
        pendingRepository.UpsertedDraft.IsAllDay.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCandidateEventCommand_WhenQualityAlreadySet_IncludesQualityInDraftTitle()
    {
        var entry = CreateEntry(
            id: 1,
            startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc));
        var repository = new StubTogglSleepRepository { Entries = [entry] };
        var draftService = new StubPendingEventDraftService();
        var pendingRepository = new StubPendingEventRepository();
        var qualityRepository = new StubTogglSleepQualityRepository { Quality = 8 };
        var viewModel = CreateViewModel(repository, draftService: draftService,
            pendingRepository: pendingRepository, qualityRepository: qualityRepository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        pendingRepository.UpsertedDraft!.Summary.Should().Be("Sleep – 8/10");
    }

    [Fact]
    public async Task CreateCandidateEventCommand_RoundsDraftTimesToNearestQuarterHour()
    {
        var entry = CreateEntry(
            id: 1,
            startUtc: new DateTime(2026, 05, 13, 04, 22, 00, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 05, 13, 12, 08, 00, DateTimeKind.Utc));
        var repository = new StubTogglSleepRepository { Entries = [entry] };
        var draftService = new StubPendingEventDraftService();
        var viewModel = CreateViewModel(repository, draftService: draftService);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        draftService.CreateCalls.Should().ContainSingle();
        draftService.CreateCalls[0].StartLocal.Should().Be(
            CalendarDraftTiming.RoundToNearestQuarterHour(entry.StartTime.ToLocalTime()));
        draftService.CreateCalls[0].EndLocal.Should().Be(
            CalendarDraftTiming.RoundToNearestQuarterHour(entry.EndTime!.Value.ToLocalTime()));
    }

    [Fact]
    public async Task CreateCandidateEventCommand_WhenMultipleEntries_SpansEarliestToLatest()
    {
        var early = CreateEntry(
            id: 1,
            startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 05, 13, 09, 00, 00, DateTimeKind.Utc));
        var late = CreateEntry(
            id: 2,
            startUtc: new DateTime(2026, 05, 13, 09, 30, 00, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc));
        var repository = new StubTogglSleepRepository { Entries = [late, early] };
        var draftService = new StubPendingEventDraftService();
        var viewModel = CreateViewModel(repository, draftService: draftService);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        draftService.CreateCalls.Should().ContainSingle();
        draftService.CreateCalls[0].StartLocal.ToUniversalTime().Should().Be(early.StartTime);
        draftService.CreateCalls[0].EndLocal.ToUniversalTime().Should().Be(late.EndTime);
    }

    [Fact]
    public async Task CreateCandidateEventCommand_SelectsNewDraftInRightPanel()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries =
            [
                CreateEntry(
                    id: 1,
                    startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
                    endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc))
            ]
        };
        var selectionService = new StubCalendarSelectionService();
        var viewModel = CreateViewModel(repository, selectionService: selectionService);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        selectionService.SelectedEventId.Should().Be("pending_sleep");
        selectionService.SelectedSourceKind.Should().Be(CalendarEventSourceKind.Pending);
        selectionService.LastOpenInEditMode.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCandidateEventCommand_SendsEventUpdatedAfterMetadataUpsert()
    {
        var repository = new StubTogglSleepRepository
        {
            Entries =
            [
                CreateEntry(
                    id: 1,
                    startUtc: new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
                    endUtc: new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc))
            ]
        };
        EventUpdatedMessage? message = null;
        WeakReferenceMessenger.Default.Register<TogglSleepDrilldownViewModelTests, EventUpdatedMessage>(
            this,
            (_, received) => message = received);
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));
        await viewModel.CreateCandidateEventCommand.ExecuteAsync(null);

        message.Should().NotBeNull();
        message!.EventId.Should().Be("pending_sleep");
    }

    private static TogglSleepDrilldownViewModel CreateViewModel(
        ITogglSleepRepository repository,
        ITogglSleepQualityRepository? qualityRepository = null,
        IPendingEventDraftService? draftService = null,
        IPendingEventRepository? pendingRepository = null,
        IGcalEventRepository? gcalEventRepository = null,
        ICalendarSelectionService? selectionService = null)
    {
        return new TogglSleepDrilldownViewModel(
            repository,
            qualityRepository ?? new StubTogglSleepQualityRepository(),
            draftService ?? new StubPendingEventDraftService(),
            pendingRepository ?? new StubPendingEventRepository(),
            gcalEventRepository ?? new StubGcalEventRepository(),
            selectionService ?? new StubCalendarSelectionService());
    }

    private static TogglEntry CreateEntry(
        long id,
        DateTime startUtc,
        DateTime endUtc,
        string description = "Sleep",
        string? linkedEventId = null,
        string? linkedEventType = null)
    {
        return new TogglEntry
        {
            TogglId = id,
            StartTime = startUtc,
            EndTime = endUtc,
            DurationSeconds = (int)(endUtc - startUtc).TotalSeconds,
            Description = description,
            LinkedEventId = linkedEventId,
            LinkedEventType = linkedEventType
        };
    }

    private sealed class StubTogglSleepRepository : ITogglSleepRepository
    {
        public IReadOnlyList<TogglEntry> Entries { get; init; } = [];

        public Task<IReadOnlyList<TogglEntry>> GetSleepEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Entries);

        public Task<IReadOnlyDictionary<DateOnly, int>> GetSleepEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(new Dictionary<DateOnly, int>());
    }

    private sealed class StubTogglSleepQualityRepository : ITogglSleepQualityRepository
    {
        public int? Quality { get; init; }
        public DateOnly? UpsertedDate { get; private set; }
        public int? UpsertedQuality { get; private set; }

        public Task<int?> GetQualityForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Quality);

        public Task UpsertQualityAsync(DateOnly date, int? quality, CancellationToken ct = default)
        {
            UpsertedDate = date;
            UpsertedQuality = quality;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPendingEventDraftService : IPendingEventDraftService
    {
        public List<(DateTime StartLocal, DateTime EndLocal, string? Summary)> CreateCalls { get; } = [];

        public Task<PendingEvent> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
        {
            CreateCalls.Add((startLocal, endLocal, summary));
            return Task.FromResult(new PendingEvent
            {
                PendingEventId = "pending_sleep",
                StartDatetime = startLocal.ToUniversalTime(),
                EndDatetime = endLocal.ToUniversalTime(),
                Summary = summary
            });
        }
    }

    private sealed class StubPendingEventRepository : IPendingEventRepository
    {
        public PendingEvent? UpsertedDraft { get; private set; }
        public PendingEvent? SleepEventForDate { get; init; }

        public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

        public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

        public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

        public Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(SleepEventForDate);

        public Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
        {
            UpsertedDraft = pendingEvent;
            return Task.CompletedTask;
        }

        public Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubGcalEventRepository : IGcalEventRepository
    {
        public GcalEvent? EventById { get; init; }

        public Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IList<GcalEvent>>([]);

        public Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult(EventById?.GcalEventId == gcalEventId ? EventById : null);

        public Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
            => Task.FromResult<(DateOnly, DateOnly)?>(null);
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
            LastOpenInEditMode = false;
        }
    }
}
