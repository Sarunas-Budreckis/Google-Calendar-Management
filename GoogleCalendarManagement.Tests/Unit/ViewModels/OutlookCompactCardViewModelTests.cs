using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class OutlookCompactCardViewModelTests
{
    private readonly Mock<IOutlookEventRepository> _repository = new();
    private readonly DateOnly _date = new(2026, 1, 15);

    [Fact]
    public async Task LoadAsync_WhenNoEvents_ShowsNoDataState()
    {
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var vm = new OutlookCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(_date);

        vm.DataVisibility.Should().Be(Visibility.Collapsed);
        vm.NoDataVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadAsync_WithSingleEvent_ShowsSingularLabel()
    {
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", 60)]);
        var vm = new OutlookCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(_date);

        vm.DataVisibility.Should().Be(Visibility.Visible);
        vm.NoDataVisibility.Should().Be(Visibility.Collapsed);
        vm.EventCountLabel.Should().Be("1 work event");
    }

    [Fact]
    public async Task LoadAsync_WithMultipleEvents_ShowsPluralLabel()
    {
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", 30), MakeEvent("id2", 60)]);
        var vm = new OutlookCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(_date);

        vm.EventCountLabel.Should().Be("2 work events");
        vm.WorkHoursLabel.Should().Contain("1h");
        vm.WorkHoursLabel.Should().Contain("30m");
    }

    [Fact]
    public async Task LoadAsync_AllEventsSuppressed_ShowsNoDataState()
    {
        var ev = MakeEvent("id1", 60);
        ev.IsSuppressed = true;
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ev]);
        var vm = new OutlookCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(_date);

        vm.DataVisibility.Should().Be(Visibility.Collapsed);
        vm.NoDataVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadAsync_AllDayEvent_WorkHoursLabelHidden()
    {
        var ev = MakeEvent("id1", 0);
        ev.IsAllDay = true;
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ev]);
        var vm = new OutlookCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(_date);

        vm.WorkHoursVisibility.Should().Be(Visibility.Collapsed);
    }

    private static OutlookEvent MakeEvent(string id, int durationMinutes)
    {
        var start = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        return new OutlookEvent
        {
            OutlookEventId = id,
            Subject = "Test Event",
            StartDatetime = start,
            EndDatetime = start.AddMinutes(durationMinutes),
            IsAllDay = false,
            IsSuppressed = false,
            IsRecurring = false,
            LastSyncedAt = DateTime.UtcNow
        };
    }
}
