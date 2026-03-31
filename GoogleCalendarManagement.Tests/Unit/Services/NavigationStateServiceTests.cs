using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class NavigationStateServiceTests
{
    [Fact]
    public async Task LoadAsync_WhenStateIsMissing_ReturnsYearViewForToday()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 03, 30, 12, 0, 0, TimeSpan.Zero));
        var repository = new InMemorySystemStateRepository();
        var service = new NavigationStateService(
            repository,
            NullLogger<NavigationStateService>.Instance,
            timeProvider);

        var state = await service.LoadAsync();

        state.Should().Be(new NavigationState(ViewMode.Year, new DateOnly(2026, 03, 30)));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsState()
    {
        var repository = new InMemorySystemStateRepository();
        var service = new NavigationStateService(
            repository,
            NullLogger<NavigationStateService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 30, 12, 0, 0, TimeSpan.Zero)));

        var expected = new NavigationState(ViewMode.Week, new DateOnly(2026, 03, 15));

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        actual.Should().Be(expected);
    }

    private sealed class InMemorySystemStateRepository : ISystemStateRepository
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            _values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task SetManyAsync(IReadOnlyDictionary<string, string> pairs, CancellationToken ct = default)
        {
            foreach (var (key, value) in pairs)
            {
                _values[key] = value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
