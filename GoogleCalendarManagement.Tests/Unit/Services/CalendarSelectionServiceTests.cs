using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

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

        sut.Select("evt-1");

        sut.SelectedGcalEventId.Should().Be("evt-1");
        received.Should().NotBeNull();
        received!.GcalEventId.Should().Be("evt-1");
    }

    [Fact]
    public void Select_DifferentId_MovesSelectionAndSendsMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1");

        EventSelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, message) => received = message);

        sut.Select("evt-2");

        sut.SelectedGcalEventId.Should().Be("evt-2");
        received.Should().NotBeNull();
        received!.GcalEventId.Should().Be("evt-2");
    }

    [Fact]
    public void Select_SameId_SuppressesMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1");

        var messageCount = 0;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, _) => messageCount++);

        sut.Select("evt-1");

        messageCount.Should().Be(0);
        sut.SelectedGcalEventId.Should().Be("evt-1");
    }

    [Fact]
    public void ClearSelection_WhenSelected_ClearsAndSendsNullMessage()
    {
        var sut = new CalendarSelectionService();
        sut.Select("evt-1");

        EventSelectedMessage? received = null;
        var recipient = Track(new object());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(recipient, (_, message) => received = message);

        sut.ClearSelection();

        sut.SelectedGcalEventId.Should().BeNull();
        received.Should().NotBeNull();
        received!.GcalEventId.Should().BeNull();
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

        Action act = () => sut.Select(invalidId);

        act.Should().Throw<ArgumentException>();
        sut.SelectedGcalEventId.Should().BeNull();
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
