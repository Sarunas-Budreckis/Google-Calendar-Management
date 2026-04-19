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

    private readonly Mock<ICalendarQueryService> _queryServiceMock = new();
    private readonly Mock<ICalendarSelectionService> _selectionServiceMock = new();
    private readonly Mock<IGcalEventRepository> _gcalEventRepositoryMock = new();
    private readonly Mock<IPendingEventRepository> _pendingEventRepositoryMock = new();

    public EventDetailsPanelViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
        _gcalEventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new GcalEvent
            {
                GcalEventId = id,
                CalendarId = "primary",
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.CloseCommand.ExecuteAsync(null);

        _selectionServiceMock.Verify(service => service.ClearSelection(), Times.Once);
    }

    [Fact]
    public async Task EnterEditMode_PopulatesEditableFields()
    {
        var evt = MakeEvent("evt-1", title: "Draft title", description: "Draft description");
        _queryServiceMock
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();

        sut.EditStartTime = sut.EditStartTime.AddHours(2);

        sut.EditEndTime.Should().Be(TimeOnly.FromDateTime(evt.EndLocal).AddHours(2));
    }

    [Fact]
    public async Task SaveNowAsync_CreatesPendingEventAndPublishesUpdateMessage()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with
            {
                Title = "Edited title",
                IsPending = true,
                Opacity = 0.6,
                PendingUpdatedAt = UtcBase.AddMinutes(30)
            });
        _pendingEventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingEvent?)null);

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModelTests, EventUpdatedMessage>(
            this,
            static (recipient, message) => recipient.OnEventUpdated(message));

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "Edited title";

        await sut.SaveNowAsync();

        _pendingEventRepositoryMock.Verify(
            repo => repo.UpsertAsync(
                It.Is<PendingEvent>(pending => pending.GcalEventId == "evt-1" && pending.Summary == "Edited title"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _lastPublishedMessage.Should().NotBeNull();
        _lastPublishedMessage!.GcalEventId.Should().Be("evt-1");
        sut.SaveStatusText.Should().Be("Saved");
        sut.SourceDisplay.Should().Be("Local changes, pending push to GCal");
        sut.LastSavedLocallyDisplay.Should().NotBe("No local changes");
    }

    [Fact]
    public async Task HandleEscapeAsync_WithPendingChanges_SavesThenCloses()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with { IsPending = true, Opacity = 0.6 });
        _pendingEventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingEvent?)null);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);
        sut.EnterEditMode();
        sut.EditTitle = "Edited title";

        var handled = await sut.HandleEscapeAsync();

        handled.Should().BeTrue();
        _pendingEventRepositoryMock.Verify(repo => repo.UpsertAsync(It.IsAny<PendingEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _selectionServiceMock.Verify(service => service.ClearSelection(), Times.Once);
    }

    [Fact]
    public async Task SaveAndExitEditModeAsync_SavesAndReturnsToViewMode()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with
            {
                Title = "Edited title",
                IsPending = true,
                Opacity = 0.6,
                PendingUpdatedAt = UtcBase.AddMinutes(30)
            });
        _pendingEventRepositoryMock
            .Setup(repo => repo.GetByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingEvent?)null);

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

    [Fact]
    public async Task RevertPendingChangesAsync_RemovesPendingEventAndReloadsOriginalEvent()
    {
        var pendingEvent = MakeEvent(
            "evt-1",
            title: "Pending title",
            description: "Draft description",
            isPending: true,
            pendingUpdatedAt: UtcBase.AddMinutes(30));
        var originalEvent = MakeEvent("evt-1", title: "Original title", description: "Original description");

        _queryServiceMock
            .SetupSequence(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEvent)
            .ReturnsAsync(originalEvent);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await WaitUntilAsync(() => sut.IsPanelVisible);

        await sut.RevertPendingChangesAsync();

        _pendingEventRepositoryMock.Verify(repo => repo.DeleteByGcalEventIdAsync("evt-1", It.IsAny<CancellationToken>()), Times.Once);
        sut.Title.Should().Be("Original title");
        sut.IsPendingEvent.Should().BeFalse();
        sut.SourceDisplay.Should().Be("From Google Calendar");
        sut.LastSavedLocallyDisplay.Should().Be("No local changes");
    }

    [Fact]
    public async Task ApplyDraggedTimeRange_UpdatesStartAndEndTogether()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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
            .Setup(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
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

    private void OnEventUpdated(EventUpdatedMessage message)
    {
        _lastPublishedMessage = message;
    }

    private EventUpdatedMessage? _lastPublishedMessage;

    private EventDetailsPanelViewModel CreateSut()
    {
        _lastPublishedMessage = null;
        return new EventDetailsPanelViewModel(
            _queryServiceMock.Object,
            _selectionServiceMock.Object,
            _gcalEventRepositoryMock.Object,
            _pendingEventRepositoryMock.Object,
            new FixedTimeProvider(new DateTimeOffset(UtcBase)));
    }

    private static CalendarEventDisplayModel MakeEvent(
        string id,
        string title = "Test Event",
        string? description = "A test description",
        bool isPending = false,
        DateTime? pendingUpdatedAt = null)
    {
        var startUtc = UtcBase;
        var endUtc = UtcBase.AddHours(1);
        return new CalendarEventDisplayModel(
            GcalEventId: id,
            Title: title,
            StartUtc: startUtc,
            EndUtc: endUtc,
            StartLocal: startUtc.ToLocalTime(),
            EndLocal: endUtc.ToLocalTime(),
            IsAllDay: false,
            ColorHex: "#7986CB",
            ColorName: "Lavender",
            IsRecurringInstance: false,
            Description: description,
            LastSyncedAt: null,
            IsPending: isPending,
            PendingUpdatedAt: pendingUpdatedAt);
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
