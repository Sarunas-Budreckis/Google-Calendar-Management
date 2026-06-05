using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class StatsFmApiClientTests
{
    private const string FakeToken = "Bearer test-token";
    private const string FakeUserId = "spotifyuser123";

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static StatsFmApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.stats.fm/") };
        return new StatsFmApiClient(httpClient);
    }

    private static HttpResponseMessage MeResponse(string userId = FakeUserId)
    {
        var json = JsonSerializer.Serialize(new
        {
            item = new { id = userId, displayName = "Test User" }
        });
        return OkJson(json);
    }

    private static HttpResponseMessage StreamsResponse(params object[] items)
    {
        var json = JsonSerializer.Serialize(new { items });
        return OkJson(json);
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static object MakeStreamItem(string endTime, int playedMs, string trackName, string artist, string album, int durationMs) =>
        new
        {
            playedMs,
            endTime,
            track = new
            {
                name = trackName,
                durationMs,
                artists = new[] { new { name = artist } },
                albums = new[] { new { name = album } }
            }
        };

    // ---------------------------------------------------------------------------
    // TestConnectionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TestConnectionAsync_ReturnsUserId_OnSuccess()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(MeResponse("user42"));
        var client = CreateClient(handler);

        var userId = await client.TestConnectionAsync(FakeToken);

        userId.Should().Be("user42");
        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v1/users/me");
    }

    [Fact]
    public async Task TestConnectionAsync_ThrowsStatsFmApiException_On401()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        var act = () => client.TestConnectionAsync(FakeToken);

        await act.Should().ThrowAsync<StatsFmApiException>()
            .WithMessage("*rejected*");
    }

    // ---------------------------------------------------------------------------
    // GetStreamsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStreamsAsync_ReturnsMappedItems()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(MeResponse());
        handler.Enqueue(StreamsResponse(
            MakeStreamItem("2025-01-15T08:30:00Z", 250_000, "Song A", "Artist X", "Album 1", 300_000)));
        var client = CreateClient(handler);

        var result = await client.GetStreamsAsync(FakeToken, DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        result.Should().HaveCount(1);
        result[0].PlayedMs.Should().Be(250_000);
        result[0].EndTime.Should().Be("2025-01-15T08:30:00Z");
        result[0].Track.Name.Should().Be("Song A");
        result[0].Track.Artists![0].Name.Should().Be("Artist X");
        result[0].Track.Albums![0].Name.Should().Be("Album 1");
        result[0].Track.DurationMs.Should().Be(300_000);
    }

    [Fact]
    public async Task GetStreamsAsync_CachesUserId_SecondCallSkipsMe()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(MeResponse());
        handler.Enqueue(StreamsResponse());
        handler.Enqueue(StreamsResponse());
        var client = CreateClient(handler);

        await client.GetStreamsAsync(FakeToken, DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));
        await client.GetStreamsAsync(FakeToken, DateOnly.Parse("2025-01-16"), DateOnly.Parse("2025-01-16"));

        // /me once + 2 streams calls
        handler.Requests.Should().HaveCount(3);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Contain("/users/me");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Contain("/streams");
        handler.Requests[2].RequestUri!.PathAndQuery.Should().Contain("/streams");
    }

    [Fact]
    public async Task GetStreamsAsync_RetriesOnRateLimit()
    {
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimitResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(MeResponse());
        handler.Enqueue(rateLimitResponse);
        handler.Enqueue(StreamsResponse());
        var client = CreateClient(handler);

        var result = await client.GetStreamsAsync(FakeToken, DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        result.Should().BeEmpty();
        // /me + 429 attempt + retry
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetStreamsAsync_IncludesUserIdInUrl()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(MeResponse("myspotifyid"));
        handler.Enqueue(StreamsResponse());
        var client = CreateClient(handler);

        await client.GetStreamsAsync(FakeToken, DateOnly.Parse("2025-01-15"), DateOnly.Parse("2025-01-15"));

        handler.Requests[1].RequestUri!.PathAndQuery.Should().Contain("/users/myspotifyid/streams");
    }

    // ---------------------------------------------------------------------------
    // Fake handler (reused from TogglApiClientTests pattern)
    // ---------------------------------------------------------------------------

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
