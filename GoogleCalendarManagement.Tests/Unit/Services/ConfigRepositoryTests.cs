using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ConfigRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public ConfigRepositoryTests()
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
    public async Task SetConfigValueAsync_WhenEncryptIsTrue_StoresEncryptedValueAndRoundTrips()
    {
        var repository = new ConfigRepository(_contextFactory, NullLogger<ConfigRepository>.Instance);

        await repository.SetConfigValueAsync(
            TogglSleepImportService.TogglApiTokenConfigKey,
            "toggl-token",
            configType: "secret",
            description: "Encrypted Toggl Track API token",
            encrypt: true);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.Configs.SingleAsync(config => config.ConfigKey == TogglSleepImportService.TogglApiTokenConfigKey);
        stored.ConfigValue.Should().NotBe("toggl-token");
        stored.ConfigValue.Should().NotContain("toggl-token");
        stored.ConfigType.Should().Be("secret");

        var loaded = await repository.GetConfigValueAsync(TogglSleepImportService.TogglApiTokenConfigKey);
        loaded.Should().Be("toggl-token");
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
