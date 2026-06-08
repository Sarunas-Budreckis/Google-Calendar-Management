using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class Civ5SessionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public Civ5SessionRepositoryTests()
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
    public async Task InsertPointsAsync_DuplicateFileModifiedAtAndGameMode_IgnoresDuplicate()
    {
        var repository = new Civ5SessionRepository(_contextFactory);
        var modifiedAt = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc);

        var firstCount = await repository.InsertPointsAsync(
        [
            new Civ5SessionPoint
            {
                ScannedAt = DateTime.UtcNow,
                FileModifiedAt = modifiedAt,
                GameMode = "single"
            }
        ]);

        var secondCount = await repository.InsertPointsAsync(
        [
            new Civ5SessionPoint
            {
                ScannedAt = DateTime.UtcNow,
                FileModifiedAt = modifiedAt,
                GameMode = "single"
            }
        ]);

        firstCount.Should().Be(1);
        secondCount.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var storedCount = await context.Civ5SessionPoints.CountAsync();
        storedCount.Should().Be(1);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new CalendarDbContext(_options));
        }
    }
}
