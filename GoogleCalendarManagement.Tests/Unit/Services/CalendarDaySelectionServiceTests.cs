using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

[Collection("Messenger")]
public sealed class CalendarDaySelectionServiceTests : IDisposable
{
    private readonly List<object> _recipients = [];

    public CalendarDaySelectionServiceTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void SelectDay_PublishesDaySelectedMessage()
    {
        var service = CreateService();
        var selectedDay = new DateOnly(2026, 05, 13);
        DaySelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<DaySelectedMessage>(recipient, (_, message) => received = message);

        service.SelectDay(selectedDay);

        service.SelectedDay.Should().Be(selectedDay);
        service.ManuallySelectedDay.Should().Be(selectedDay);
        received.Should().Be(new DaySelectedMessage(selectedDay));
    }

    [Fact]
    public void ClearSelection_PublishesDaySelectedMessageWithNull()
    {
        var service = CreateService();
        service.SelectDay(new DateOnly(2026, 05, 13));
        DaySelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<DaySelectedMessage>(recipient, (_, message) => received = message);

        service.ClearSelection();

        service.SelectedDay.Should().BeNull();
        service.ManuallySelectedDay.Should().BeNull();
        received.Should().Be(new DaySelectedMessage(null));
    }

    [Fact]
    public void SelectDay_WhenSameDaySelectedAgain_ClearsSelection()
    {
        var service = CreateService();
        var selectedDay = new DateOnly(2026, 05, 13);
        service.SelectDay(selectedDay);
        DaySelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<DaySelectedMessage>(recipient, (_, message) => received = message);

        service.SelectDay(selectedDay);

        service.SelectedDay.Should().BeNull();
        service.ManuallySelectedDay.Should().BeNull();
        received.Should().Be(new DaySelectedMessage(null));
    }

    [Fact]
    public void AutoSelectInDayView_DoesNotUpdatePersistentSelection()
    {
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 05, 01), new DateOnly(2026, 05, 10)));
        var service = new CalendarDaySelectionService(navigationStateService);
        var dayViewDate = new DateOnly(2026, 05, 13);

        service.AutoSelectDay(dayViewDate);

        service.SelectedDay.Should().Be(dayViewDate);
        service.ManuallySelectedDay.Should().Be(new DateOnly(2026, 05, 10));
        navigationStateService.LastSavedState.Should().BeNull();
    }

    [Fact]
    public void ReturnFromDayView_RestoresManualSelection()
    {
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 05, 01), new DateOnly(2026, 05, 10)));
        var service = new CalendarDaySelectionService(navigationStateService);
        service.AutoSelectDay(new DateOnly(2026, 05, 13));
        DaySelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<DaySelectedMessage>(recipient, (_, message) => received = message);

        service.RestoreManualSelection();

        service.SelectedDay.Should().Be(new DateOnly(2026, 05, 10));
        service.ManuallySelectedDay.Should().Be(new DateOnly(2026, 05, 10));
        received.Should().Be(new DaySelectedMessage(new DateOnly(2026, 05, 10)));
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
        foreach (var recipient in _recipients)
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    private static CalendarDaySelectionService CreateService()
    {
        return new CalendarDaySelectionService(new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 05, 01))));
    }

    private TRecipient Track<TRecipient>(TRecipient recipient)
        where TRecipient : class
    {
        _recipients.Add(recipient);
        return recipient;
    }

    private sealed class StubNavigationStateService : INavigationStateService
    {
        private NavigationState _state;

        public StubNavigationStateService(NavigationState state)
        {
            _state = state;
        }

        public NavigationState? LastSavedState { get; private set; }

        public Task<NavigationState> LoadAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_state);
        }

        public Task SaveAsync(NavigationState state, CancellationToken ct = default)
        {
            _state = state;
            LastSavedState = state;
            return Task.CompletedTask;
        }
    }
}
