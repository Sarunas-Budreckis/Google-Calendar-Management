using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class EventDetailsPanelViewModelTests : IDisposable
{
    private static readonly DateTime UtcBase = new(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly IColorMappingService ColorMappingService = new ColorMappingService();

    private readonly Mock<ICalendarQueryService> _queryServiceMock = new();
    private readonly Mock<ICalendarSelectionService> _selectionServiceMock = new();
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();

    public EventDetailsPanelViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new Event
            {
                EventId = id,
                GcalEventId = id,
                CalendarId = "primary",
                Lifecycle = "approved",
                Publish = "published",
                Summary = "Stored title",
                Description = "Stored description",
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                ColorId = "1",
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });
        _eventRepositoryMock
            .Setup(repo => repo.GetByEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new Event
            {
                EventId = id,
                GcalEventId = id.StartsWith("pending", StringComparison.Ordinal) ? null : id,
                CalendarId = "primary",
                Lifecycle = "approved",
                Publish = id.StartsWith("pending", StringComparison.Ordinal) ? "local_only" : "published",
                Summary = "Stored title",
                Description = "Stored description",
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                ColorId = "1",
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task EventSelectedMessage_NonNull_LoadsEventAndShowsPanel()
    {
        var evt = MakeEvent("evt-1", title: "Team Meeting");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        sut.IsPanelVisible.Should().BeTrue();
        sut.Title.Should().Be("Team Meeting");
        sut.ColorHex.Should().Be("#7986CB");
        sut.ColorName.Should().Be("Lavender");
    }

    [Fact]
    public async Task CloseCommand_CallsClearSelection_WhenPanelIsVisible()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.CloseCommand.ExecuteAsync(null);

        _selectionServiceMock.Verify(service => service.ClearSelection(), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WhenCandidate_UpdatesLifecycleAndRefreshesPanel()
    {
        var candidate = MakeEvent(
            "candidate-1",
            sourceKind: CalendarEventSourceKind.Candidate,
            isPending: true);
        var approved = candidate with
        {
            SourceKind = CalendarEventSourceKind.Pending,
            StatusLabel = "Not yet published to Google Calendar"
        };
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("candidate-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidate)
            .ReturnsAsync(approved);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("candidate-1", CalendarEventSourceKind.Candidate));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.ApproveAsync();

        _eventRepositoryMock.Verify(
            repo => repo.UpdateLifecycleAsync("candidate-1", "approved", It.IsAny<CancellationToken>()),
            Times.Once);
        sut.SourceDisplay.Should().Be("Not yet published to Google Calendar");
    }

    [Fact]
    public async Task EnterEditMode_PopulatesEditableFields()
    {
        var evt = MakeEvent("evt-1", title: "Draft title", description: "Draft description");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        sut.EnterEditMode();

        sut.IsEditMode.Should().BeTrue();
        sut.EditTitle.Should().Be("Draft title");
        sut.EditDescription.Should().Be("Draft description");
        sut.EditStartDate.Should().Be(DateOnly.FromDateTime(evt.StartLocal));
        sut.EditEndDate.Should().Be(DateOnly.FromDateTime(evt.EndLocal));
    }

    [Fact]
    public async Task ValidateFields_EmptyTitleAndInvalidDateRange_ReturnsFalse()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = string.Empty;
        sut.EditEndDate = sut.EditStartDate;
        sut.EditEndTime = sut.EditStartTime;

        var isValid = sut.ValidateFields();

        isValid.Should().BeFalse();
        sut.TitleError.Should().Be("Title is required");
        sut.DateTimeError.Should().Be("End time must be after start time");
    }

    [Fact]
    public async Task UndoLastChange_RestoresPreviousFieldValue()
    {
        var evt = MakeEvent("evt-1", title: "Before");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "After";

        sut.UndoLastChange();

        sut.EditTitle.Should().Be("Before");
    }

    [Fact]
    public async Task EditStartTime_ShiftsEndTimeBySameDuration()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        sut.EditStartTime = sut.EditStartTime.AddHours(2);

        sut.EditEndTime.Should().Be(TimeOnly.FromDateTime(evt.EndLocal).AddHours(2));
    }

    [Fact(Skip = "Story 8.5 removes Event overlay save path; replace with unified Event repository assertions.")]
    public async Task SaveNowAsync_CreatesEventAndPublishesUpdateMessage()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with
            {
                Title = "Edited title",
                IsPending = true,
                Opacity = 0.6,
                PendingUpdatedAt = UtcBase.AddMinutes(30)
            });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModelTests, EventUpdatedMessage>(
            this,
            static (recipient, message) => recipient.OnEventUpdated(message));

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "Edited title";

        await sut.SaveNowAsync();

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending => pending.GcalEventId == "evt-1" && pending.Summary == "Edited title"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _lastPublishedMessage.Should().NotBeNull();
        _lastPublishedMessage!.EventId.Should().Be("evt-1");
        sut.SaveStatusText.Should().Be("Saved");
        sut.SourceDisplay.Should().Be("Local changes, pending push to GCal");
        sut.LastSavedLocallyDisplay.Should().NotBe("No local changes");
    }

    [Fact(Skip = "Story 8.5 removes Event overlay save path; replace with unified Event repository assertions.")]
    public async Task SelectColorAsync_SavesImmediatelyWithoutWaitingForDebounce()
    {
        var evt = MakeEvent("evt-1", colorKey: "azure", colorName: "Azure", colorHex: "#00AAFF");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with
            {
                ColorKey = "navy",
                ColorName = "Navy",
                ColorHex = "#3F51B5",
                IsPending = true,
                Opacity = 0.6,
                PendingUpdatedAt = UtcBase.AddMinutes(30)
            });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        await sut.SelectColorAsync("navy");

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending => pending.GcalEventId == "evt-1" && pending.ColorId == "navy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        sut.ColorName.Should().Be("Navy");
        sut.ColorHex.Should().Be("#3F51B5");
        sut.EditColorId.Should().Be("navy");
    }

    [Fact]
    public async Task SelectColorAsync_SameColor_DoesNotWriteEvent()
    {
        var evt = MakeEvent("evt-1", colorKey: "azure", colorName: "Azure", colorHex: "#00AAFF");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        await sut.SelectColorAsync("azure");

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay save path; replace with unified Event repository assertions.")]
    public async Task ApplyColorToEventAsync_WhenNotInEditMode_CreatesEventForGoogleEvent()
    {
        var evt = MakeEvent("evt-1", colorKey: "azure", colorName: "Azure", colorHex: "#00AAFF");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut();

        await sut.ApplyColorToEventAsync("evt-1", CalendarEventSourceKind.Google, "navy");

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.GcalEventId == "evt-1" &&
                    pending.ColorId == "navy" &&
                    pending.Summary == "Test Event"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay save path; replace with unified Event repository assertions.")]
    public async Task HandleEscapeAsync_WithPendingChanges_SavesThenCloses()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with { IsPending = true, Opacity = 0.6 });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "Edited title";

        var handled = await sut.HandleEscapeAsync();

        handled.Should().BeTrue();
        _eventRepositoryMock.Verify(repo => repo.UpsertAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Once);
        _selectionServiceMock.Verify(service => service.ClearSelection(), Times.Once);
    }

    [Fact]
    public async Task SaveAndExitEditModeAsync_SavesAndReturnsToViewMode()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with
            {
                Title = "Edited title",
                IsPending = true,
                Opacity = 0.6,
                PendingUpdatedAt = UtcBase.AddMinutes(30)
            });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "Edited title";

        await sut.SaveAndExitEditModeAsync();

        sut.IsEditMode.Should().BeFalse();
        sut.Title.Should().Be("Edited title");
        sut.IsPendingEvent.Should().BeTrue();
    }

    [Fact(Skip = "Story 8.5 removes Event overlay revert path; replace with unified Event repository assertions.")]
    public async Task RevertPendingChangesAsync_RemovesEventAndReloadsOriginalEvent()
    {
        var Event = MakeEvent(
            "evt-1",
            title: "Pending title",
            description: "Draft description",
            isPending: true,
            pendingUpdatedAt: UtcBase.AddMinutes(30));
        var originalEvent = MakeEvent("evt-1", title: "Original title", description: "Original description");

        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Event)
            .ReturnsAsync(originalEvent);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.RevertPendingChangesAsync();

        _eventRepositoryMock.Verify(repo => repo.DeleteByEventIdAsync("evt-1", It.IsAny<CancellationToken>()), Times.Once);
        sut.Title.Should().Be("Original title");
        sut.IsPendingEvent.Should().BeFalse();
        sut.SourceDisplay.Should().Be("From Google Calendar");
        sut.LastSavedLocallyDisplay.Should().Be("No local changes");
    }

    [Fact(Skip = "Story 8.5 removes Event draft delete path; replace with unified Event repository assertions.")]
    public async Task RevertPendingChangesForEventAsync_WhenPendingDraft_DeletesPendingDraft()
    {
        var sut = CreateSut();

        await sut.RevertPendingChangesForEventAsync("pending-1", CalendarEventSourceKind.Pending);

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyDraggedTimeRange_UpdatesStartAndEndTogether()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        var newStart = evt.StartLocal.AddMinutes(30);
        var newEnd = evt.EndLocal.AddMinutes(30);

        sut.ApplyDraggedTimeRange("evt-1", newStart, newEnd);

        sut.EditStartDate.Should().Be(DateOnly.FromDateTime(newStart));
        sut.EditStartTime.Should().Be(TimeOnly.FromDateTime(newStart));
        sut.EditEndDate.Should().Be(DateOnly.FromDateTime(newEnd));
        sut.EditEndTime.Should().Be(TimeOnly.FromDateTime(newEnd));
    }

    [Fact]
    public async Task ApplyResizedEndTime_UpdatesOnlyEndFields()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        var newEnd = evt.EndLocal.AddMinutes(45);
        sut.ApplyResizedEndTime("evt-1", newEnd);

        sut.EditStartDate.Should().Be(DateOnly.FromDateTime(evt.StartLocal));
        sut.EditStartTime.Should().Be(TimeOnly.FromDateTime(evt.StartLocal));
        sut.EditEndDate.Should().Be(DateOnly.FromDateTime(newEnd));
        sut.EditEndTime.Should().Be(TimeOnly.FromDateTime(newEnd));
    }

    [Fact(Skip = "Story 8.5 removes Event overlay drag path; replace with unified Event repository assertions.")]
    public async Task ApplyDroppedTimeRangeAsync_UnselectedGoogleEvent_CreatesPendingOverlayWithoutSelecting()
    {
        var evt = MakeEvent("evt-drag-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-drag-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-drag-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);
        var sut = CreateSut();
        var newStart = evt.StartLocal.AddMinutes(45);
        var newEnd = evt.EndLocal.AddMinutes(45);

        var applied = await sut.ApplyDroppedTimeRangeAsync("evt-drag-1", CalendarEventSourceKind.Google, newStart, newEnd);

        applied.Should().BeTrue();
        _selectionServiceMock.Verify(service => service.Select(
                It.IsAny<string>(),
                It.IsAny<CalendarEventSourceKind>(),
                It.IsAny<bool>()),
            Times.Never);
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.GcalEventId == "evt-drag-1" &&
                    pending.StartDatetime == DateTime.SpecifyKind(newStart, DateTimeKind.Local).ToUniversalTime() &&
                    pending.EndDatetime == DateTime.SpecifyKind(newEnd, DateTimeKind.Local).ToUniversalTime()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay drag path; replace with unified Event repository assertions.")]
    public async Task ApplyDroppedTimeRangeAsync_PendingDraft_UpdatesPendingRowByPendingId()
    {
        var draft = MakeEvent("pending_drag_1", sourceKind: CalendarEventSourceKind.Pending, isPending: true);
        var pendingRow = new Event
        {
            EventId = "pending_drag_1",
            CalendarId = "primary",
            Summary = "Draft",
            StartDatetime = draft.StartUtc,
            EndDatetime = draft.EndUtc,
            IsAllDay = false,
            SourceSystem = "manual",
            CreatedAt = UtcBase,
            UpdatedAt = UtcBase
        };
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_drag_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _eventRepositoryMock
            .Setup(repo => repo.GetByEventIdAsync("pending_drag_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingRow);
        var sut = CreateSut();
        var newStart = draft.StartLocal.AddMinutes(30);
        var newEnd = draft.EndLocal.AddMinutes(30);

        var applied = await sut.ApplyDroppedTimeRangeAsync("pending_drag_1", CalendarEventSourceKind.Pending, newStart, newEnd);

        applied.Should().BeTrue();
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.EventId == "pending_drag_1" &&
                    pending.GcalEventId == null &&
                    pending.StartDatetime == DateTime.SpecifyKind(newStart, DateTimeKind.Local).ToUniversalTime()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay drag undo path; replace with unified Event repository assertions.")]
    public async Task UndoLastDragRescheduleAsync_RestoresExistingPendingOverlay()
    {
        var evt = MakeEvent("evt-drag-undo", isPending: true);
        var previousPending = new Event
        {
            EventId = "pending_drag_undo",
            GcalEventId = "evt-drag-undo",
            CalendarId = "primary",
            Summary = "Before drag",
            StartDatetime = evt.StartUtc,
            EndDatetime = evt.EndUtc,
            IsAllDay = false,
            SourceSystem = "google-overlay",
            CreatedAt = UtcBase,
            UpdatedAt = UtcBase
        };
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-drag-undo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-drag-undo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousPending);
        var sut = CreateSut();

        await sut.ApplyDroppedTimeRangeAsync(
            "evt-drag-undo",
            CalendarEventSourceKind.Google,
            evt.StartLocal.AddHours(2),
            evt.EndLocal.AddHours(2));
        var undone = await sut.UndoLastDragRescheduleAsync();

        undone.Should().BeTrue();
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.EventId == "pending_drag_undo" &&
                    pending.StartDatetime == evt.StartUtc &&
                    pending.EndDatetime == evt.EndUtc),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private void OnEventUpdated(EventUpdatedMessage message)
    {
        _lastPublishedMessage = message;
    }

    private EventUpdatedMessage? _lastPublishedMessage;

    private EventDetailsPanelViewModel CreateSut(Mock<IContentDialogService>? dialogServiceMock = null)
    {
        _lastPublishedMessage = null;
        return new EventDetailsPanelViewModel(
            _queryServiceMock.Object,
            _selectionServiceMock.Object,
            ColorMappingService,
            _eventRepositoryMock.Object,
            new FixedTimeProvider(new DateTimeOffset(UtcBase)),
            dialogServiceMock?.Object);
    }

    private static CalendarEventDisplayModel MakeEvent(
        string id,
        string title = "Test Event",
        string? description = "A test description",
        bool isPending = false,
        bool isPendingDelete = false,
        DateTime? pendingUpdatedAt = null,
        string colorKey = "lavender",
        string colorName = "Lavender",
        string colorHex = "#7986CB",
        CalendarEventSourceKind sourceKind = CalendarEventSourceKind.Google)
    {
        var startUtc = UtcBase;
        var endUtc = UtcBase.AddHours(1);
        return new CalendarEventDisplayModel(
            EventId: id,
            SourceKind: sourceKind,
            Title: title,
            StartUtc: startUtc,
            EndUtc: endUtc,
            StartLocal: startUtc.ToLocalTime(),
            EndLocal: endUtc.ToLocalTime(),
            IsAllDay: false,
            ColorHex: colorHex,
            ColorName: colorName,
            IsRecurringInstance: false,
            Description: description,
            LastSyncedAt: null,
            IsPending: isPending,
            PendingUpdatedAt: pendingUpdatedAt,
            ColorKey: colorKey,
            IsPendingDelete: isPendingDelete);
    }

    [Fact(Skip = "Story 8.5 removes Event draft delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_LocalDraft_ConfirmedDeletesAndHidesPanel()
    {
        var draft = new CalendarEventDisplayModel(
            EventId: "pending_draft_1",
            SourceKind: CalendarEventSourceKind.Pending,
            Title: "My Draft",
            StartUtc: UtcBase,
            EndUtc: UtcBase.AddHours(1),
            StartLocal: UtcBase.ToLocalTime(),
            EndLocal: UtcBase.AddHours(1).ToLocalTime(),
            IsAllDay: false,
            ColorHex: "#00AAFF",
            ColorName: "Azure",
            IsRecurringInstance: false,
            Description: null,
            LastSyncedAt: null,
            IsPending: true,
            ColorKey: "azure");

        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_draft_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var dialogMock = new Mock<IContentDialogService>();
        dialogMock
            .Setup(d => d.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("pending_draft_1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_draft_1", It.IsAny<CancellationToken>()),
            Times.Once);
        sut.IsPanelVisible.Should().BeFalse();
    }

    [Fact(Skip = "Story 8.5 removes Event draft delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_LocalDraft_DeletesWithoutConfirmation()
    {
        var draft = new CalendarEventDisplayModel(
            EventId: "pending_draft_2",
            SourceKind: CalendarEventSourceKind.Pending,
            Title: "My Draft",
            StartUtc: UtcBase,
            EndUtc: UtcBase.AddHours(1),
            StartLocal: UtcBase.ToLocalTime(),
            EndLocal: UtcBase.AddHours(1).ToLocalTime(),
            IsAllDay: false,
            ColorHex: "#00AAFF",
            ColorName: "Azure",
            IsRecurringInstance: false,
            Description: null,
            LastSyncedAt: null,
            IsPending: true,
            ColorKey: "azure");

        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_draft_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var dialogMock = new Mock<IContentDialogService>();
        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("pending_draft_2"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_draft_2", It.IsAny<CancellationToken>()),
            Times.Once);
        dialogMock.Verify(
            d => d.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        sut.IsPanelVisible.Should().BeFalse();
    }

    [Fact(Skip = "Story 8.5 removes Event draft delete path; replace with unified Event repository assertions.")]
    public async Task EventSelectedMessage_NewUneditedDraftCleared_DeletesDraftWithoutPrompt()
    {
        var draft = MakeEvent(
            "pending_blank_1",
            title: string.Empty,
            sourceKind: CalendarEventSourceKind.Pending,
            isPending: true);
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_blank_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("pending_blank_1", CalendarEventSourceKind.Pending, OpenInEditMode: true));
        await WaitUntilAsync(() => sut.IsNewUneditedDraft);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
        await WaitUntilAsync(() => !sut.IsPanelVisible);

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_blank_1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event draft delete path; replace with unified Event repository assertions.")]
    public async Task EventSelectedMessage_NewUneditedDraftThenAnotherEvent_LoadsClickedEventAndDeletesDraft()
    {
        var draft = MakeEvent(
            "pending_blank_switch_1",
            title: string.Empty,
            sourceKind: CalendarEventSourceKind.Pending,
            isPending: true);
        var nextEvent = MakeEvent("evt-clicked-1", title: "Clicked Event");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_blank_switch_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-clicked-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextEvent);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(
            "pending_blank_switch_1",
            CalendarEventSourceKind.Pending,
            OpenInEditMode: true));
        await WaitUntilAsync(() => sut.IsNewUneditedDraft);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-clicked-1", CalendarEventSourceKind.Google));
        await WaitUntilAsync(() => sut.Title == "Clicked Event");

        sut.Title.Should().Be("Clicked Event");
        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_blank_switch_1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event draft save path; replace with unified Event repository assertions.")]
    public async Task EventSelectedMessage_NewDraftWithTypedTitle_SavesBeforeSelectionChanges()
    {
        var draft = MakeEvent(
            "pending_typed_1",
            title: string.Empty,
            sourceKind: CalendarEventSourceKind.Pending,
            isPending: true);
        var nextEvent = MakeEvent("evt-next-1", title: "Next Event");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_typed_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-next-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextEvent);
        _eventRepositoryMock
            .Setup(repo => repo.GetByEventIdAsync("pending_typed_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Event
            {
                EventId = "pending_typed_1",
                CalendarId = "primary",
                Summary = null,
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                IsAllDay = false,
                ColorId = "azure",
                SourceSystem = "manual",
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("pending_typed_1", CalendarEventSourceKind.Pending, OpenInEditMode: true));
        await WaitUntilAsync(() => sut.IsNewUneditedDraft);
        sut.EditTitle = "Real title";
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-next-1", CalendarEventSourceKind.Google));
        await WaitUntilAsync(() => sut.Title == "Next Event");

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.EventId == "pending_typed_1" &&
                    pending.Summary == "Real title"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_typed_1", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Story 8.5 removes Event draft save path; replace with unified Event repository assertions.")]
    public async Task SelectColorAsync_NewDraftMarksCandidateEvenWithBlankTitle()
    {
        var draft = MakeEvent(
            "pending_color_1",
            title: string.Empty,
            sourceKind: CalendarEventSourceKind.Pending,
            isPending: true,
            colorKey: "azure",
            colorName: "Azure",
            colorHex: "#00AAFF");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_color_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _eventRepositoryMock
            .Setup(repo => repo.GetByEventIdAsync("pending_color_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Event
            {
                EventId = "pending_color_1",
                CalendarId = "primary",
                Summary = null,
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                IsAllDay = false,
                ColorId = "azure",
                SourceSystem = "manual",
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("pending_color_1", CalendarEventSourceKind.Pending, OpenInEditMode: true));
        await WaitUntilAsync(() => sut.IsNewUneditedDraft);
        await sut.SelectColorAsync("navy");
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
        await Task.Delay(50);

        sut.IsNewUneditedDraft.Should().BeFalse();
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(pending =>
                    pending.EventId == "pending_color_1" &&
                    pending.ColorId == "navy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_color_1", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EventSelectedMessage_TitledGeneratedDraft_IsNotTreatedAsUneditedBlankDraft()
    {
        var draft = MakeEvent(
            "pending_generated_sleep",
            title: "Sleep",
            sourceKind: CalendarEventSourceKind.Pending,
            isPending: true,
            colorKey: "grey",
            colorName: "Grey",
            colorHex: "#616161");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("pending_generated_sleep", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(
            "pending_generated_sleep",
            CalendarEventSourceKind.Pending,
            OpenInEditMode: true));
        await WaitUntilAsync(() => sut.IsEditMode && sut.EditTitle == "Sleep");

        sut.IsNewUneditedDraft.Should().BeFalse();

        await sut.SaveAndExitEditModeAsync();
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
        await Task.Delay(50);

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("pending_generated_sleep", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_PublishedEventNoPending_ConfirmedStagesPendingDelete()
    {
        var evt = MakeEvent("evt-pub-1", title: "Published Meeting");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-pub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with { IsPending = true, IsPendingDelete = true, Opacity = 0.6 });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-pub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var dialogMock = new Mock<IContentDialogService>();
        dialogMock
            .Setup(d => d.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-pub-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(p => p.GcalEventId == "evt-pub-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_PublishedEventWithPendingEdit_ChooseRevertRoutesToRevert()
    {
        var pendingEvt = MakeEvent("evt-edit-1", title: "Edited Title", isPending: true);
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-edit-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEvt)
            .ReturnsAsync(MakeEvent("evt-edit-1", title: "Original Title"));
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-edit-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Event
            {
                EventId = "pending_edit_1",
                GcalEventId = "evt-edit-1",
                CalendarId = "primary",
                Summary = "Edited Title",
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });

        var dialogMock = new Mock<IContentDialogService>();
        dialogMock
            .Setup(d => d.ShowDeleteWithPendingEditAsync(It.IsAny<string>()))
            .ReturnsAsync(GoogleCalendarManagement.Services.DeleteWithPendingEditChoice.RevertChanges);

        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-edit-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.DeleteByEventIdAsync("evt-edit-1", It.IsAny<CancellationToken>()),
            Times.Once);
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_PublishedEventWithPendingEdit_ChooseDeleteConvertsToDeleteType()
    {
        var pendingEvt = MakeEvent("evt-edit-2", title: "Edited Title", isPending: true);
        _queryServiceMock
            .SetupSequence(service => service.GetEventByIdAsync("evt-edit-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEvt)
            .ReturnsAsync(pendingEvt with { IsPendingDelete = true });
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-edit-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Event
            {
                EventId = "pending_edit_2",
                GcalEventId = "evt-edit-2",
                CalendarId = "primary",
                Summary = "Edited Title",
                StartDatetime = UtcBase,
                EndDatetime = UtcBase.AddHours(1),
                CreatedAt = UtcBase,
                UpdatedAt = UtcBase
            });

        var dialogMock = new Mock<IContentDialogService>();
        dialogMock
            .Setup(d => d.ShowDeleteWithPendingEditAsync(It.IsAny<string>()))
            .ReturnsAsync(GoogleCalendarManagement.Services.DeleteWithPendingEditChoice.DeleteEvent);

        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-edit-2"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(p => p.GcalEventId == "evt-edit-2"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Story 8.5 removes Event overlay delete path; replace with unified Event repository assertions.")]
    public async Task DeleteEventAsync_PublishedEventNoPending_StagesDeleteWithoutConfirmation()
    {
        var evt = MakeEvent("evt-cancel-del-1");
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-cancel-del-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        _eventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-cancel-del-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var dialogMock = new Mock<IContentDialogService>();
        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-cancel-del-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.DeleteEventAsync();

        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<Event>(p => p.GcalEventId == "evt-cancel-del-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        dialogMock.Verify(
            d => d.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteEventAsync_AlreadyStagedForDelete_ShowsInfoDialogAndMakesNoRepositoryWrite()
    {
        var evt = MakeEvent("evt-already-del-1", isPendingDelete: true);
        _queryServiceMock
            .Setup(service => service.GetEventByIdAsync("evt-already-del-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var dialogMock = new Mock<IContentDialogService>();
        dialogMock
            .Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(dialogMock);
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-already-del-1"));
        await WaitUntilAsync(() => sut.IsPendingDeleteEvent);

        await sut.DeleteEventAsync();

        dialogMock.Verify(
            d => d.ShowMessageAsync(
                It.Is<string>(s => s.Contains("Already")),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        _eventRepositoryMock.Verify(
            repo => repo.UpsertAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 500)
    {
        var startedAt = Environment.TickCount64;
        while (!predicate())
        {
            if (Environment.TickCount64 - startedAt > timeoutMs)
            {
                break;
            }

            await Task.Delay(10);
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
