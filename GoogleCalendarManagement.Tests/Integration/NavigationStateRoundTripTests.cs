using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class NavigationStateRoundTripTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public NavigationStateRoundTripTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RestoresIdenticalNavigationState()
    {
        var repository = new SystemStateRepository(_contextFactory);
        var service = new NavigationStateService(
            repository,
            NullLogger<NavigationStateService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 30, 12, 0, 0, TimeSpan.Zero)));
        var expected = new NavigationState(ViewMode.Week, new DateOnly(2026, 03, 15));

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        actual.Should().Be(expected);
    }

    public void Dispose()
    {
        _connection.Dispose();
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

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext()
        {
            return new CalendarDbContext(_options);
        }

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
