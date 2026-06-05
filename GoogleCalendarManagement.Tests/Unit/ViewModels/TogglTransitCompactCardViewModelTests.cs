using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class TogglTransitCompactCardViewModelTests
{
    [Fact]
    public async Task LoadAsync_WhenNoEntries_ShowsNoData()
    {
        var viewModel = new TogglTransitCompactCardViewModel(new StubTogglTransitRepository());

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.NoEntriesVisibility.Should().Be(Visibility.Visible);
        viewModel.EntriesVisibility.Should().Be(Visibility.Collapsed);
        viewModel.TotalDurationLabel.Should().BeEmpty();
        viewModel.TripCountLabel.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenOneTrip_ShowsTotalDurationAndSingularTripCount()
    {
        var viewModel = new TogglTransitCompactCardViewModel(new StubTogglTransitRepository
        {
            Entries =
            [
                new TogglEntry
                {
                    TogglId = 1,
                    StartTime = new DateTime(2026, 06, 04, 13, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 06, 04, 13, 30, 0, DateTimeKind.Utc)
                }
            ]
        });

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.EntriesVisibility.Should().Be(Visibility.Visible);
        viewModel.NoEntriesVisibility.Should().Be(Visibility.Collapsed);
        viewModel.TotalDurationLabel.Should().Be("30m total");
        viewModel.TripCountLabel.Should().Be("1 trip");
    }

    [Fact]
    public async Task LoadAsync_WhenMultipleTrips_ShowsCombinedDurationAndPluralTripCount()
    {
        var viewModel = new TogglTransitCompactCardViewModel(new StubTogglTransitRepository
        {
            Entries =
            [
                new TogglEntry
                {
                    TogglId = 1,
                    StartTime = new DateTime(2026, 06, 04, 8, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 06, 04, 8, 30, 0, DateTimeKind.Utc)
                },
                new TogglEntry
                {
                    TogglId = 2,
                    StartTime = new DateTime(2026, 06, 04, 17, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 06, 04, 17, 45, 0, DateTimeKind.Utc)
                }
            ]
        });

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.TripCountLabel.Should().Be("2 trips");
        viewModel.TotalDurationLabel.Should().Be("1h 15m total");
    }

    [Fact]
    public async Task LoadAsync_WhenTripLongerThan1Hour_ShowsHoursAndMinutes()
    {
        var viewModel = new TogglTransitCompactCardViewModel(new StubTogglTransitRepository
        {
            Entries =
            [
                new TogglEntry
                {
                    TogglId = 1,
                    StartTime = new DateTime(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 06, 04, 11, 20, 0, DateTimeKind.Utc)
                }
            ]
        });

        await viewModel.LoadAsync(new DateOnly(2026, 06, 04));

        viewModel.TotalDurationLabel.Should().Be("1h 20m total");
    }

    private sealed class StubTogglTransitRepository : ITogglTransitRepository
    {
        public IReadOnlyList<TogglEntry> Entries { get; init; } = [];

        public Task<IReadOnlyList<TogglEntry>> GetTransitEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Entries);

        public Task<IReadOnlyDictionary<DateOnly, int>> GetTransitEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(new Dictionary<DateOnly, int>());
    }
}
