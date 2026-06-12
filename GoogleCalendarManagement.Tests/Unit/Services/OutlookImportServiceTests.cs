using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

[Collection("Messenger")]
public sealed class OutlookImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<IGraphApiClient> _apiClient = new();
    private readonly RecordingRecipient _recipient = new();

    public OutlookImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);

        WeakReferenceMessenger.Default.Register<DataSourceImportCompletedMessage>(_recipient, (_, msg) =>
        {
            _recipient.Messages.Add(msg);
        });
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_StoresEvents()
    {
        _apiClient
            .Setup(m => m.GetCalendarViewAsync("token", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")]);
        var service = CreateService();

        var result = await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        result.Success.Should().BeTrue();
        result.NewRecords.Should().Be(1);
        result.UpdatedRecords.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.OutlookEvents.SingleAsync();
        stored.OutlookEventId.Should().Be("id1");
        stored.Subject.Should().Be("Standup");
        stored.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_UpsertsById_NoDuplicates()
    {
        _apiClient
            .SetupSequence(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")])
            .ReturnsAsync([MakeEvent("id1", "Standup (updated)", "2026-01-15T09:00:00", "2026-01-15T09:30:00")]);
        var service = CreateService();

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));
        var result = await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        result.Success.Should().BeTrue();
        result.NewRecords.Should().Be(0);
        result.UpdatedRecords.Should().Be(1);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.OutlookEvents.CountAsync()).Should().Be(1);
        (await context.OutlookEvents.SingleAsync()).Subject.Should().Be("Standup (updated)");
    }

    [Fact]
    public async Task ImportAsync_PreservesIsSuppressedOnUpdate()
    {
        _apiClient
            .SetupSequence(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")])
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")]);
        var service = CreateService();

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        // Suppress the event, then re-import
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var ev = await context.OutlookEvents.SingleAsync();
            ev.IsSuppressed = true;
            await context.SaveChangesAsync();
        }

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        (await ctx.OutlookEvents.SingleAsync()).IsSuppressed.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_EmptyResponse_ReturnsZeroRecords()
    {
        _apiClient
            .Setup(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateService();

        var result = await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        result.Success.Should().BeTrue();
        result.NewRecords.Should().Be(0);
        result.UpdatedRecords.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_GraphApiException_ReturnsFalse()
    {
        _apiClient
            .Setup(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GraphApiException("401 Unauthorized"));
        var service = CreateService();

        var result = await service.ImportAsync("bad-token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401");
    }

    [Fact]
    public async Task ImportAsync_WritesImportLog_OnSuccess()
    {
        _apiClient
            .Setup(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")]);
        var service = CreateService();

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var log = await context.DataSourceImportLogs.SingleAsync();
        log.Success.Should().BeTrue();
        log.RecordsFetched.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_PublishesDataSourceImportCompletedMessage()
    {
        _apiClient
            .Setup(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00")]);
        var service = CreateService();

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        _recipient.Messages.Should().ContainSingle();
        _recipient.Messages[0].SourceKey.Should().Be("outlook");
        _recipient.Messages[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_RecurringOccurrence_SetsIsRecurringTrue()
    {
        var ev = MakeEvent("id1", "Standup", "2026-01-15T09:00:00", "2026-01-15T09:30:00");
        ev.Type = "occurrence";
        _apiClient
            .Setup(m => m.GetCalendarViewAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ev]);
        var service = CreateService();

        await service.ImportAsync("token", DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.OutlookEvents.SingleAsync()).IsRecurring.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(_recipient);
        _connection.Dispose();
    }

    private OutlookImportService CreateService()
    {
        return new OutlookImportService(_contextFactory, _apiClient.Object, TimeProvider.System);
    }

    private static GraphEventDto MakeEvent(
        string id,
        string subject,
        string startIso,
        string endIso,
        string? organizer = null,
        string? location = null)
    {
        return new GraphEventDto
        {
            Id = id,
            Subject = subject,
            Start = new GraphDateTimeDto { DateTime = startIso, TimeZone = "UTC" },
            End = new GraphDateTimeDto { DateTime = endIso, TimeZone = "UTC" },
            IsAllDay = false,
            Organizer = organizer is null ? null : new GraphOrganizerDto
            {
                EmailAddress = new GraphEmailAddressDto { Name = organizer }
            },
            Location = location is null ? null : new GraphLocationDto { DisplayName = location },
            BodyPreview = null,
            Type = "singleInstance",
            SeriesMasterId = null
        };
    }

    private sealed class RecordingRecipient
    {
        public List<DataSourceImportCompletedMessage> Messages { get; } = [];
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
            => Task.FromResult(new CalendarDbContext(_options));
    }
}
