using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class TogglSleepCompactCardViewModelTests
{
    [Fact]
    public async Task LoadAsync_WhenNoEntries_ShowsNoSleepData()
    {
        var viewModel = CreateViewModel(new StubTogglSleepRepository());

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.NoEntriesVisibility.Should().Be(Visibility.Visible);
        viewModel.SingleEntryVisibility.Should().Be(Visibility.Collapsed);
        viewModel.MultipleEntriesVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task LoadAsync_WhenOneEntry_ShowsTimeSummary()
    {
        var viewModel = CreateViewModel(new StubTogglSleepRepository
        {
            Entries =
            [
                new TogglEntry
                {
                    TogglId = 1,
                    StartTime = new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 05, 13, 12, 15, 00, DateTimeKind.Utc)
                }
            ]
        });

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.SingleEntryVisibility.Should().Be(Visibility.Visible);
        viewModel.StartLabel.Should().NotBeEmpty();
        viewModel.EndLabel.Should().NotBeEmpty();
        viewModel.DurationLabel.Should().Be("7h 45m");
    }

    [Fact]
    public async Task LoadAsync_WhenMultipleEntries_ShowsCountOnly()
    {
        var viewModel = CreateViewModel(new StubTogglSleepRepository
        {
            Entries =
            [
                new TogglEntry { TogglId = 1, StartTime = new DateTime(2026, 05, 13, 04, 30, 00, DateTimeKind.Utc) },
                new TogglEntry { TogglId = 2, StartTime = new DateTime(2026, 05, 13, 10, 30, 00, DateTimeKind.Utc) }
            ]
        });

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.MultipleEntriesVisibility.Should().Be(Visibility.Visible);
        viewModel.CountLabel.Should().Be("2 sleep entries");
        viewModel.StartLabel.Should().BeEmpty();
        viewModel.EndLabel.Should().BeEmpty();
        viewModel.DurationLabel.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenQualitySet_ShowsQualityLabel()
    {
        var viewModel = CreateViewModel(
            new StubTogglSleepRepository
            {
                Entries = [new TogglEntry { TogglId = 1, StartTime = new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc) }]
            },
            new StubTogglSleepQualityRepository { Quality = 7 });

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.QualityLabel.Should().Be("7/10");
        viewModel.QualityLabelVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadAsync_WhenNoQuality_QualityLabelIsEmptyAndHidden()
    {
        var viewModel = CreateViewModel(
            new StubTogglSleepRepository
            {
                Entries = [new TogglEntry { TogglId = 1, StartTime = new DateTime(2026, 05, 13, 04, 30, 0, DateTimeKind.Utc) }]
            });

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.QualityLabel.Should().BeEmpty();
        viewModel.QualityLabelVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task LoadAsync_WhenNoEntries_DoesNotShowStoredQuality()
    {
        var viewModel = CreateViewModel(
            new StubTogglSleepRepository(),
            new StubTogglSleepQualityRepository { Quality = 7 });

        await viewModel.LoadAsync(new DateOnly(2026, 05, 13));

        viewModel.QualityLabel.Should().BeEmpty();
        viewModel.QualityLabelVisibility.Should().Be(Visibility.Collapsed);
    }

    private static TogglSleepCompactCardViewModel CreateViewModel(
        ITogglSleepRepository repository,
        ITogglSleepQualityRepository? qualityRepository = null)
    {
        return new TogglSleepCompactCardViewModel(
            repository,
            qualityRepository ?? new StubTogglSleepQualityRepository());
    }

    private sealed class StubTogglSleepRepository : ITogglSleepRepository
    {
        public IReadOnlyList<TogglEntry> Entries { get; init; } = [];

        public Task<IReadOnlyList<TogglEntry>> GetSleepEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Entries);

        public Task<IReadOnlyDictionary<DateOnly, int>> GetSleepEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(new Dictionary<DateOnly, int>());
    }

    private sealed class StubTogglSleepQualityRepository : ITogglSleepQualityRepository
    {
        public int? Quality { get; init; }

        public Task<int?> GetQualityForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(Quality);

        public Task UpsertQualityAsync(DateOnly date, int? quality, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
