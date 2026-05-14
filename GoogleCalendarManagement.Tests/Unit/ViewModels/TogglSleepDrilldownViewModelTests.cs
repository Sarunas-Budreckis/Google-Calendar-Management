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
        IPendingEventDraftService? draftService = null,
        IPendingEventRepository? pendingRepository = null,
        ICalendarSelectionService? selectionService = null)
    {
        return new TogglSleepDrilldownViewModel(
            repository,
            draftService ?? new StubPendingEventDraftService(),
            pendingRepository ?? new StubPendingEventRepository(),
            selectionService ?? new StubCalendarSelectionService());
    }

    private static TogglEntry CreateEntry(long id, DateTime startUtc, DateTime endUtc, string description = "Sleep")
    {
        return new TogglEntry
        {
            TogglId = id,
            StartTime = startUtc,
            EndTime = endUtc,
            DurationSeconds = (int)(endUtc - startUtc).TotalSeconds,
            Description = description
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

        public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

        public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

        public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);

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
