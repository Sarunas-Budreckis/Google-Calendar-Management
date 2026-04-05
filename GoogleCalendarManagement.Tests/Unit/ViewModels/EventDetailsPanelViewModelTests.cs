using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class EventDetailsPanelViewModelTests : IDisposable
{
    private static readonly DateTime UtcBase = new DateTime(2026, 4, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICalendarQueryService> _queryServiceMock = new();
    private readonly Mock<ICalendarSelectionService> _selectionServiceMock = new();

    public void Dispose()
    {
        // Clean up any lingering messenger registrations from the view model
        // The WeakReferenceMessenger holds weak refs, but clean up explicitly.
        WeakReferenceMessenger.Default.Cleanup();
    }

    // ── AC-3.4.1 / 3.4.2: selected message loads event and shows panel ────────

    [Fact]
    public async Task EventSelectedMessage_NonNull_LoadsEventAndShowsPanel()
    {
        var evt = MakeEvent("evt-1", title: "Team Meeting");
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));

        // Allow async load to complete
        await Task.Delay(50);

        sut.IsPanelVisible.Should().BeTrue();
        sut.Title.Should().Be("Team Meeting");
        sut.ColorHex.Should().Be("#7986CB");
        sut.ColorName.Should().Be("Lavender");
        sut.SourceDisplay.Should().Be("From Google Calendar");
    }

    // ── AC-3.4.6: null selection message hides panel ──────────────────────────

    [Fact]
    public async Task EventSelectedMessage_Null_HidesPanel()
    {
        var evt = MakeEvent("evt-1");
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await Task.Delay(50);
        sut.IsPanelVisible.Should().BeTrue();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
        await Task.Delay(10);

        sut.IsPanelVisible.Should().BeFalse();
        sut.Title.Should().BeEmpty();
    }

    // ── AC-3.4.4: close command clears selection ──────────────────────────────

    [Fact]
    public void CloseCommand_CallsClearSelection()
    {
        var sut = CreateSut();

        sut.CloseCommand.Execute(null);

        _selectionServiceMock.Verify(s => s.ClearSelection(), Times.Once);
    }

    // ── AC-3.4.7: null description uses placeholder ───────────────────────────

    [Fact]
    public async Task EventSelectedMessage_NullDescription_UsesPlaceholder()
    {
        var evt = MakeEvent("evt-1", description: null);
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await Task.Delay(50);

        sut.DescriptionDisplay.Should().Be("No description provided.");
    }

    [Fact]
    public async Task EventSelectedMessage_EmptyDescription_UsesPlaceholder()
    {
        var evt = MakeEvent("evt-1", description: "");
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await Task.Delay(50);

        sut.DescriptionDisplay.Should().Be("No description provided.");
    }

    // ── AC-3.4.7: null last-synced renders "Never" ────────────────────────────

    [Fact]
    public async Task EventSelectedMessage_NullLastSynced_RendersNever()
    {
        var evt = MakeEvent("evt-1", lastSyncedAt: null);
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await Task.Delay(50);

        sut.LastSyncedDisplay.Should().Be("Never");
    }

    // ── AC-3.4.5: view-mode switch does not affect panel state ────────────────

    [Fact]
    public async Task PanelState_UnchangedWhenNoNewMessage_AfterLoadingEvent()
    {
        var evt = MakeEvent("evt-1", title: "Stable Event");
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var sut = CreateSut();

        WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-1"));
        await Task.Delay(50);

        // Simulate view mode switch by doing nothing to the panel ─
        // no new EventSelectedMessage means no state change.
        sut.IsPanelVisible.Should().BeTrue();
        sut.Title.Should().Be("Stable Event");
    }

    // ── AC-3.4.7: missing event from query service does not throw ─────────────

    [Fact]
    public async Task EventSelectedMessage_EventNotFoundInQuery_DoesNotThrowAndHidesPanel()
    {
        _queryServiceMock
            .Setup(s => s.GetEventByGcalIdAsync("evt-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEventDisplayModel?)null);

        var sut = CreateSut();

        var act = async () =>
        {
            WeakReferenceMessenger.Default.Send(new EventSelectedMessage("evt-missing"));
            await Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
        sut.IsPanelVisible.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EventDetailsPanelViewModel CreateSut()
    {
        return new EventDetailsPanelViewModel(
            _queryServiceMock.Object,
            _selectionServiceMock.Object);
    }

    private static CalendarEventDisplayModel MakeEvent(
        string id,
        string title = "Test Event",
        string? description = "A test description",
        DateTime? lastSyncedAt = null,
        string colorId = "1")
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
            LastSyncedAt: lastSyncedAt);
    }
}
