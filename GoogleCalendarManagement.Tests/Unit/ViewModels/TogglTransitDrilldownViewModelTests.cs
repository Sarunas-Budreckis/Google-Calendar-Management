using FluentAssertions;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class TogglTransitDrilldownViewModelTests
{
    public TogglTransitDrilldownViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task LoadAsync_WhenNoEntries_HasEntriesIsFalse()
    {
        var viewModel = CreateViewModel(new StubTogglTransitRepository());

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.HasEntries.Should().BeFalse();
        viewModel.EmptyStateVisibility.Should().Be(Visibility.Visible);
        viewModel.CreateCandidateEventsCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenEntries_MapsToSessionViewModels()
    {
        var repository = new StubTogglTransitRepository
        {
            Entries =
            [
                CreateEntry(
                    id: 1,
                    startUtc: new DateTime(2026, 06, 04, 13, 0, 0, DateTimeKind.Utc),
                    endUtc: new DateTime(2026, 06, 04, 13, 30, 0, DateTimeKind.Utc))
            ]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.HasEntries.Should().BeTrue();
        viewModel.Sessions.Should().ContainSingle();
        viewModel.Sessions[0].DurationLabel.Should().Be("30m");
        viewModel.SessionsVisibility.Should().Be(Visibility.Visible);
        viewModel.EmptyStateVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task CreateCandidateEventsCommand_WhenOneShortTrip_CreatesSingleEvent()
    {
        var entry = CreateEntry(
            id: 1,
            startUtc: new DateTime(2026, 06, 04, 13, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 06, 04, 13, 10, 0, DateTimeKind.Utc));
        var repository = new StubTogglTransitRepository { Entries = [entry] };
        var pendingEventRepo = new StubPendingEventRepository();
        var draftService = new StubPendingEventDraftService();
        var selectionService = new StubCalendarSelectionService();
        var viewModel = CreateViewModel(repository, pendingEventRepo, selectionService, draftService);

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));
        await viewModel.CreateCandidateEventsCommand.ExecuteAsync(null);

        // 10-min trip = 1 block (single block, last block always kept)
        draftService.Created.Should().HaveCount(1);
        draftService.Created[0].Summary.Should().Be("Driving");
        draftService.Created[0].ColorId.Should().Be("lavender");
        selectionService.LastSelectedId.Should().Be(draftService.Created[0].EventId);
    }

    [Fact]
    public async Task CreateCandidateEventsCommand_WhenTwoTrips_CreatesEventsForEachIndependently()
    {
        var entries = new[]
        {
            CreateEntry(1,
                new DateTime(2026, 06, 04, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 06, 04, 8, 20, 0, DateTimeKind.Utc)),
            CreateEntry(2,
                new DateTime(2026, 06, 04, 17, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 06, 04, 17, 20, 0, DateTimeKind.Utc))
        };
        var repository = new StubTogglTransitRepository { Entries = entries };
        var pendingEventRepo = new StubPendingEventRepository();
        var draftService = new StubPendingEventDraftService();
        var viewModel = CreateViewModel(repository, pendingEventRepo, draftService: draftService);

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));
        await viewModel.CreateCandidateEventsCommand.ExecuteAsync(null);

        // Each 20-min trip → 2 blocks each → 4 total
        draftService.Created.Should().HaveCount(4);
        draftService.Created.Should().AllSatisfy(e =>
        {
            e.Summary.Should().Be("Driving");
            e.ColorId.Should().Be("lavender");
            e.SourceSystem.Should().Be("toggl");
        });
    }

    [Fact]
    public async Task CreateCandidateEventsCommand_WhenNoEntries_CreatesNoEvents()
    {
        var repository = new StubTogglTransitRepository();
        var pendingEventRepo = new StubPendingEventRepository();
        var viewModel = CreateViewModel(repository, pendingEventRepo);

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.CreateCandidateEventsCommand.CanExecute(null).Should().BeFalse();
        pendingEventRepo.Upserted.Should().BeEmpty();
    }

    private static TogglTransitDrilldownViewModel CreateViewModel(
        ITogglTransitRepository repository,
        StubPendingEventRepository? pendingEventRepo = null,
        StubCalendarSelectionService? selectionService = null,
        StubPendingEventDraftService? draftService = null)
    {
        draftService ??= new StubPendingEventDraftService();
        pendingEventRepo ??= new StubPendingEventRepository();
        selectionService ??= new StubCalendarSelectionService();
        var eightFifteen = new EightFifteenRuleService();
        return new TogglTransitDrilldownViewModel(
            repository,
            draftService,
            pendingEventRepo,
            selectionService,
            eightFifteen);
    }

    private static TogglEntry CreateEntry(long id, DateTime startUtc, DateTime endUtc) =>
        new()
        {
            TogglId = id,
            StartTime = startUtc,
            EndTime = endUtc,
            ProjectName = "Transit",
            TogglDataType = TogglDataType.TogglTransit
        };

    private sealed class StubTogglTransitRepository : ITogglTransitRepository
    {
        public IReadOnlyList<TogglEntry> Entries { get; init; } = [];

        public Task<IReadOnlyList<TogglEntry>> GetTransitEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Entries);

        public Task<IReadOnlyDictionary<DateOnly, int>> GetTransitEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(new Dictionary<DateOnly, int>());
    }

    private sealed class StubPendingEventDraftService : IPendingEventDraftService
    {
        public List<Event> Created { get; } = [];

        public Task<Event> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
        {
            var draft = new Event
            {
                EventId = Guid.NewGuid().ToString(),
                StartDatetime = startLocal,
                EndDatetime = endLocal,
                Summary = summary
            };
            Created.Add(draft);
            return Task.FromResult(draft);
        }
    }

    private sealed class StubPendingEventRepository : IPendingEventRepository
    {
        public List<PendingEvent> Upserted { get; } = [];

        public Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
        {
            Upserted.Add(pendingEvent);
            return Task.CompletedTask;
        }

        public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default) => Task.FromResult<PendingEvent?>(null);
        public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default) => Task.FromResult<PendingEvent?>(null);
        public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default) => Task.FromResult<PendingEvent?>(null);
        public Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default) => Task.FromResult<PendingEvent?>(null);
        public Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubCalendarSelectionService : ICalendarSelectionService
    {
        public string? LastSelectedId { get; private set; }
        public string? SelectedEventId => LastSelectedId;
        public CalendarEventSourceKind? SelectedSourceKind { get; private set; }

        public void Select(string eventId, CalendarEventSourceKind sourceKind, bool openInEditMode = false)
        {
            LastSelectedId = eventId;
            SelectedSourceKind = sourceKind;
        }

        public void ClearSelection() { }
    }
}
