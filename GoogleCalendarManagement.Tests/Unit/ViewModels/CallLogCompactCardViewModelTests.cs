using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class CallLogCompactCardViewModelTests
{
    private readonly Mock<ICallLogRepository> _repository = new();

    [Fact]
    public async Task LoadAsync_WhenNoEntries_ShowsNoDataState()
    {
        _repository
            .Setup(r => r.GetEntriesForDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var vm = new CallLogCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(new DateOnly(2026, 1, 15));

        vm.HasData.Should().BeFalse();
        vm.SummaryLabel.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithSingleCall_ShowsSingularLabel()
    {
        _repository
            .Setup(r => r.GetEntriesForDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEntry(durationSeconds: 300)]);
        var vm = new CallLogCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(new DateOnly(2026, 1, 15));

        vm.HasData.Should().BeTrue();
        vm.SummaryLabel.Should().Contain("1 call");
        vm.SummaryLabel.Should().Contain("5 min");
    }

    [Fact]
    public async Task LoadAsync_WithMultipleCalls_ShowsPluralLabel()
    {
        _repository
            .Setup(r => r.GetEntriesForDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEntry(durationSeconds: 720), MakeEntry(durationSeconds: 600)]);
        var vm = new CallLogCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(new DateOnly(2026, 1, 15));

        vm.HasData.Should().BeTrue();
        vm.SummaryLabel.Should().Contain("2 calls");
    }

    [Fact]
    public async Task LoadAsync_DurationOver1Hour_FormatsWithHoursAndMinutes()
    {
        _repository
            .Setup(r => r.GetEntriesForDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEntry(durationSeconds: 3900)]);
        var vm = new CallLogCompactCardViewModel(_repository.Object);

        await vm.LoadAsync(new DateOnly(2026, 1, 15));

        vm.SummaryLabel.Should().Contain("1 hr 5 min");
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(30, "30s")]
    [InlineData(60, "1m")]
    [InlineData(90, "1m 30s")]
    [InlineData(3600, "1h")]
    [InlineData(3660, "1h 1m")]
    [InlineData(7200, "2h")]
    public void FormatDuration_ReturnsExpected(int totalSeconds, string expected)
    {
        CallLogEntryViewModel.FormatDuration(totalSeconds).Should().Be(expected);
    }

    private static CallLogEntry MakeEntry(int durationSeconds = 300) => new()
    {
        Id = 1,
        ImportId = 1,
        CallType = "Incoming",
        Date = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local),
        DurationSeconds = durationSeconds,
        Number = "+1234567890",
        Contact = "Test Contact",
        Service = "iPhone"
    };
}
