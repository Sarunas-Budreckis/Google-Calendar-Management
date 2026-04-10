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
    public async Task SaveNowAsync_CreatesPendingEventAndPublishesUpdateMessage()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .SetupSequence(service => service.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt)
            .ReturnsAsync(evt with { Title = "Edited title", IsPending = true, Opacity = 0.6 });
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
        string? description = "A test description")
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
            LastSyncedAt: null);
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
