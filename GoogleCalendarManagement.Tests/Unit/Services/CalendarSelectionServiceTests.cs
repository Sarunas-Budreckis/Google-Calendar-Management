using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

[Collection("Messenger")]
public sealed class CalendarSelectionServiceTests : IDisposable
{
    private readonly List<object> _recipients = [];

    [Fact]
    public void Select_NewId_UpdatesSelectedIdAndSendsMessage()
    {
        var sut = new CalendarSelectionService();
        EventSelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, message) => received = message);

        sut.Select("evt-1", CalendarEventSourceKind.Google);

        sut.SelectedEventId.Should().Be("evt-1");
        sut.SelectedSourceKind.Should().Be(CalendarEventSourceKind.Google);
        received.Should().NotBeNull();
        received!.EventId.Should().Be("evt-1");
        received.SourceKind.Should().Be(CalendarEventSourceKind.Google);
    }

    [Fact]
    public void Select_DifferentId_MovesSelectionAndSendsMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1", CalendarEventSourceKind.Google);

        EventSelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, message) => received = message);

        sut.Select("evt-2", CalendarEventSourceKind.Pending);

        sut.SelectedEventId.Should().Be("evt-2");
        sut.SelectedSourceKind.Should().Be(CalendarEventSourceKind.Pending);
        received.Should().NotBeNull();
        received!.EventId.Should().Be("evt-2");
        received.SourceKind.Should().Be(CalendarEventSourceKind.Pending);
    }

    [Fact]
    public void Select_SameId_SuppressesMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1", CalendarEventSourceKind.Google);

        var messageCount = 0;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, _) => messageCount++);

        sut.Select("evt-1", CalendarEventSourceKind.Google);

        messageCount.Should().Be(0);
        sut.SelectedEventId.Should().Be("evt-1");
    }

    [Fact]
    public void ClearSelection_WhenSelected_ClearsAndSendsNullMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1", CalendarEventSourceKind.Google);

        EventSelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, message) => received = message);

        sut.ClearSelection();

        sut.SelectedEventId.Should().BeNull();
        received.Should().NotBeNull();
        received!.EventId.Should().BeNull();
        received.SourceKind.Should().BeNull();
    }

    [Fact]
    public void ClearSelection_WhenAlreadyClear_SuppressesMessage()
    {
        var sut = new CalendarSelectionService();

        var messageCount = 0;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, _) => messageCount++);

        sut.ClearSelection();

        messageCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Select_EmptyOrWhitespace_ThrowsArgumentException(string invalidId)
    {
        var sut = new CalendarSelectionService();

        Action act = () => sut.Select(invalidId, CalendarEventSourceKind.Google);

        act.Should().Throw<ArgumentException>();
        sut.SelectedEventId.Should().BeNull();
    }

    public void Dispose()
    {
        foreach (var recipient in _recipients)
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    private TRecipient Track<TRecipient>(TRecipient recipient)
        where TRecipient : class
    {
        _recipients.Add(recipient);
        return recipient;
    }
}
