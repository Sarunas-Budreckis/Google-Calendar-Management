using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class SpotifyImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<IStatsFmApiClient> _apiClient = new();
    private readonly Mock<IConfigRepository> _configRepository = new();
    private readonly RecordingRecipient _recipient = new();

    public SpotifyImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        _configRepository
            .Setup(m => m.GetConfigValueAsync(SpotifyImportService.StatsFmTokenConfigKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-bearer-token");

        WeakReferenceMessenger.Default.Register<DataSourceImportCompletedMessage>(_recipient, (_, msg) =>
        {
            _recipient.Messages.Add(msg);
        });
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_StoresStreams()
    {
        _apiClient
            .Setup(m => m.GetStreamsAsync("test-bearer-token",
                DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateStreamItem("2025-01-15T08:30:00Z", "Track A", "Artist X", "Album 1", 300_000, 250_000)]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(1);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.SpotifyStreams.SingleAsync();
        stored.TrackName.Should().Be("Track A");
        stored.ArtistName.Should().Be("Artist X");
        stored.AlbumName.Should().Be("Album 1");
        stored.DurationMs.Should().Be(300_000);
        stored.MsPlayed.Should().Be(250_000);
    }

    [Fact]
    public async Task ImportAsync_PlayedAtIsUtcFromEndTime()
    {
        _apiClient
            .Setup(m => m.GetStreamsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateStreamItem("2025-01-15T08:30:00Z", "T", "A", null, 180_000, 180_000)]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.SpotifyStreams.SingleAsync();
        stored.PlayedAt.Should().Be(new DateTime(2025, 1, 15, 8, 30, 0, DateTimeKind.Utc));
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — deduplication
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_UpsertsByPlayedAtAndTrackName_NoDuplicates()
    {
        var item = CreateStreamItem("2025-01-15T08:30:00Z", "Track A", "Artist X", null, 300_000, 250_000);
        var updatedItem = CreateStreamItem("2025-01-15T08:30:00Z", "Track A", "Artist Y", "New Album", 300_000, 300_000);
        _apiClient
            .SetupSequence(m => m.GetStreamsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item])
            .ReturnsAsync([updatedItem]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));
        await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.SpotifyStreams.CountAsync()).Should().Be(1);
        var stored = await context.SpotifyStreams.SingleAsync();
        stored.ArtistName.Should().Be("Artist Y");
        stored.AlbumName.Should().Be("New Album");
        stored.MsPlayed.Should().Be(300_000);
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — error handling
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_ReturnsFalse_WhenNoTokenConfigured()
    {
        _configRepository
            .Setup(m => m.GetConfigValueAsync(SpotifyImportService.StatsFmTokenConfigKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("token");
    }

    [Fact]
    public async Task ImportAsync_ReturnsFalse_WhenApiThrows()
    {
        _apiClient
            .Setup(m => m.GetStreamsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StatsFmApiException("stats.fm API rejected the configured token."));
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("rejected");
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — messaging
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_PublishesDataSourceImportCompletedMessage()
    {
        _apiClient
            .Setup(m => m.GetStreamsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateStreamItem("2025-01-15T08:30:00Z", "T", "A", null, 180_000, 180_000)]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        _recipient.Messages.Should().ContainSingle();
        _recipient.Messages[0].SourceKey.Should().Be("spotify");
        _recipient.Messages[0].Success.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // ImportAsync — import log
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_WritesImportLog_OnSuccess()
    {
        _apiClient
            .Setup(m => m.GetStreamsAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateStreamItem("2025-01-15T08:30:00Z", "T", "A", null, 180_000, 180_000)]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var log = await context.DataSourceImportLogs.SingleAsync();
        log.Success.Should().BeTrue();
        log.RecordsFetched.Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(_recipient);
        _connection.Dispose();
    }

    private SpotifyImportService CreateService()
    {
        return new SpotifyImportService(
            _contextFactory,
            _configRepository.Object,
            _apiClient.Object,
            TimeProvider.System);
    }

    private static StatsFmStreamItemDto CreateStreamItem(
        string endTime, string trackName, string artist, string? album, int durationMs, int playedMs)
    {
        return new StatsFmStreamItemDto(
            PlayedMs: playedMs,
            EndTime: endTime,
            Track: new StatsFmStreamTrackDto(
                Name: trackName,
                DurationMs: durationMs,
                Artists: [new StatsFmArtistDto(artist)],
                Albums: album is null ? null : [new StatsFmAlbumDto(album)]));
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
        {
            return Task.FromResult(new CalendarDbContext(_options));
        }
    }
}
