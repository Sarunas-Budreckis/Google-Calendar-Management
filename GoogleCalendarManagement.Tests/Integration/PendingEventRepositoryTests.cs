using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class PendingEventRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public PendingEventRepositoryTests()
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
    public async Task UpsertAsync_CreatesThenUpdatesPendingEvent()
    {
        await using (var seedContext = await _contextFactory.CreateDbContextAsync())
        {
            seedContext.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-1",
                CalendarId = "primary",
                Summary = "Original",
                StartDatetime = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
                ColorId = "1",
                CreatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc)
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new PendingEventRepository(_contextFactory);
        var created = new PendingEvent
        {
            Id = Guid.NewGuid(),
            GcalEventId = "evt-1",
            Summary = "Draft title",
            Description = "Draft description",
            StartDatetime = new DateTime(2026, 04, 04, 11, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 04, 12, 0, 0, DateTimeKind.Utc),
            ColorId = "1",
            CreatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc)
        };

        await repository.UpsertAsync(created);

        var updated = new PendingEvent
        {
            Id = created.Id,
            GcalEventId = created.GcalEventId,
            Summary = "Updated title",
            Description = "Updated description",
            StartDatetime = created.StartDatetime,
            EndDatetime = created.EndDatetime,
            ColorId = created.ColorId,
            CreatedAt = created.CreatedAt,
            UpdatedAt = new DateTime(2026, 04, 04, 8, 45, 0, DateTimeKind.Utc)
        };

        await repository.UpsertAsync(updated);

        var stored = await repository.GetByGcalEventIdAsync("evt-1");

        stored.Should().NotBeNull();
        stored!.Summary.Should().Be("Updated title");
        stored.Description.Should().Be("Updated description");
        stored.CreatedAt.Should().Be(created.CreatedAt);
        stored.UpdatedAt.Should().Be(updated.UpdatedAt);
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
