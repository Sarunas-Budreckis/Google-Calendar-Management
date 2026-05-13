using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class DataSourceRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public DataSourceRepositoryTests()
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
    public async Task SetIntegrationAsync_PersistsToDatabase()
    {
        var repository = new DataSourceRepository(_contextFactory);
        var source = await repository.UpsertSourceAsync(new DataSource
        {
            SourceKey = "toggl",
            DisplayName = "Toggl",
            SupportsNoDataHint = true
        });
        var date = new DateOnly(2026, 05, 13);

        await repository.SetIntegrationAsync(date, source.DataSourceId, true);

        await using var verifyContext = await _contextFactory.CreateDbContextAsync();
        var stored = await verifyContext.DateSourceIntegrations.SingleAsync();
        stored.Date.Should().Be(date);
        stored.DataSourceId.Should().Be(source.DataSourceId);
        stored.Integrated.Should().BeTrue();
        stored.IntegratedAt.Should().NotBeNull();
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
            return Task.FromResult(new CalendarDbContext(_options));
        }
    }
}
