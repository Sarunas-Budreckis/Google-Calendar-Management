using FluentAssertions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit;

public sealed class AuthenticationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public AuthenticationTests()
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
    public async Task DpapiTokenStorage_StoredValue_IsEncryptedNotPlaintext()
    {
        var service = new DpapiTokenStorageService(_contextFactory, NullLogger<DpapiTokenStorageService>.Instance);
        var token = CreateTokenResponse();
        var rawJson = System.Text.Json.JsonSerializer.Serialize(token);

        await service.StoreTokenAsync(token);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.Configs.SingleAsync(config => config.ConfigKey == DpapiTokenStorageService.TokenConfigKey);

        stored.ConfigValue.Should().NotBeNullOrWhiteSpace();
        stored.ConfigValue.Should().NotBe(rawJson);
        stored.ConfigValue.Should().NotContain(token.AccessToken);
    }

    [Fact]
    public async Task DpapiTokenStorage_LoadAfterStore_RoundTrips()
    {
        var service = new DpapiTokenStorageService(_contextFactory, NullLogger<DpapiTokenStorageService>.Instance);
        var token = CreateTokenResponse();

        await service.StoreTokenAsync(token);
        var loaded = await service.LoadTokenAsync();

        loaded.Should().NotBeNull();
        loaded!.AccessToken.Should().Be(token.AccessToken);
        loaded.RefreshToken.Should().Be(token.RefreshToken);
        loaded.ExpiresInSeconds.Should().Be(token.ExpiresInSeconds);
    }

    [Fact]
    public async Task DpapiTokenStorage_Clear_RemovesMetadataRow()
    {
        var service = new DpapiTokenStorageService(_contextFactory, NullLogger<DpapiTokenStorageService>.Instance);

        await service.StoreTokenAsync(CreateTokenResponse());
        await service.ClearTokenAsync();
        var loaded = await service.LoadTokenAsync();

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task GoogleCalendarService_CancelledAuth_ReturnsFailure()
    {
        var tokenStorage = new Mock<ITokenStorageService>();
        var broker = new Mock<IGoogleAuthorizationBroker>();
        broker.Setup(mock => mock.AuthorizeAsync(
                It.IsAny<ClientSecrets>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var options = CreateGoogleOptionsWithClientSecret();
        var service = new GoogleCalendarService(
            tokenStorage.Object,
            broker.Object,
            _contextFactory,
            options,
            NullLogger<GoogleCalendarService>.Instance);

        var result = await service.AuthenticateAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
        tokenStorage.Verify(mock => mock.StoreTokenAsync(It.IsAny<TokenResponse>()), Times.Never);
    }

    [Fact]
    public async Task GoogleCalendarService_MissingCredentialsFile_ReturnsFailure()
    {
        var service = new GoogleCalendarService(
            Mock.Of<ITokenStorageService>(),
            Mock.Of<IGoogleAuthorizationBroker>(),
            _contextFactory,
            new GoogleCalendarOptions(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            NullLogger<GoogleCalendarService>.Instance);

        var result = await service.AuthenticateAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("client_secret.json");
    }

    [Fact]
    public async Task GoogleCalendarService_IsAuthenticated_ReturnsFalse_WhenNoToken()
    {
        var tokenStorage = new Mock<ITokenStorageService>();
        tokenStorage.Setup(mock => mock.LoadTokenAsync()).ReturnsAsync((TokenResponse?)null);

        var service = new GoogleCalendarService(
            tokenStorage.Object,
            Mock.Of<IGoogleAuthorizationBroker>(),
            _contextFactory,
            new GoogleCalendarOptions(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            NullLogger<GoogleCalendarService>.Instance);

        var result = await service.IsAuthenticatedAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static TokenResponse CreateTokenResponse()
    {
        return new TokenResponse
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresInSeconds = 3600,
            IssuedUtc = DateTime.UtcNow
        };
    }

    private static GoogleCalendarOptions CreateGoogleOptionsWithClientSecret()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gcm-auth-{Guid.NewGuid():N}");
        var options = new GoogleCalendarOptions(root);
        Directory.CreateDirectory(options.CredentialsDirectoryPath);
        File.WriteAllText(
            options.ClientSecretPath,
            """
            {
              "installed": {
                "client_id": "test-client-id.apps.googleusercontent.com",
                "project_id": "test-project",
                "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                "token_uri": "https://oauth2.googleapis.com/token",
                "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
                "client_secret": "test-client-secret",
                "redirect_uris": [ "http://localhost" ]
              }
            }
            """);
        return options;
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
