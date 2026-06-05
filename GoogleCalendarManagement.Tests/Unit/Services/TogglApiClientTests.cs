using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class TogglApiClientTests
{
    private const string FakeToken = "test-token";
    private const int FakeWorkspaceId = 99999;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static FakeHttpMessageHandler BuildHandler(params HttpResponseMessage[] responses)
    {
        var handler = new FakeHttpMessageHandler();
        foreach (var r in responses)
        {
            handler.Enqueue(r);
        }

        return handler;
    }

    private static HttpResponseMessage MeResponse(int workspaceId = FakeWorkspaceId)
    {
        var json = JsonSerializer.Serialize(new { default_workspace_id = workspaceId });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage ReportsPageResponse(
        object[] entries,
        int? nextRowNumber = null)
    {
        var json = JsonSerializer.Serialize(entries);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (nextRowNumber.HasValue)
        {
            response.Headers.Add("X-Next-Row-Number", nextRowNumber.Value.ToString());
        }

        return response;
    }

    private static TogglApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.track.toggl.com/") };
        return new TogglApiClient(httpClient);
    }

    // ---------------------------------------------------------------------------
    // Field mapping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTimeEntriesAsync_MapsReportsApiEntryToDto()
    {
        var entry = new
        {
            id = 123L,
            description = "Deep sleep",
            start = "2025-01-01T22:00:00+00:00",
            stop = "2025-01-02T06:00:00+00:00",
            dur = 28_800_000L, // 8 h in milliseconds
            pid = (long?)null,
            project = (string?)null,
            tags = new[] { "sleep" }
        };

        var handler = BuildHandler(MeResponse(), ReportsPageResponse([entry]));
        var client = CreateClient(handler);

        var result = await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-01-01"), DateOnly.Parse("2025-01-01"));

        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(123L);
        dto.Description.Should().Be("Deep sleep");
        dto.Start.Should().Be("2025-01-01T22:00:00+00:00");
        dto.Stop.Should().Be("2025-01-02T06:00:00+00:00");
        dto.Duration.Should().Be(28_800); // milliseconds → seconds
        dto.Tags.Should().BeEquivalentTo(["sleep"]);
    }

    // ---------------------------------------------------------------------------
    // Workspace ID fetching and caching
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTimeEntriesAsync_FetchesWorkspaceIdFromMe()
    {
        var handler = BuildHandler(MeResponse(), ReportsPageResponse([]));
        var client = CreateClient(handler);

        await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-01-01"), DateOnly.Parse("2025-01-01"));

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v9/me");
        handler.Requests[1].RequestUri!.AbsoluteUri.Should().Contain($"/workspace/{FakeWorkspaceId}/");
    }

    [Fact]
    public async Task GetTimeEntriesAsync_CachesWorkspaceId_SecondCallSkipsMe()
    {
        var handler = BuildHandler(
            MeResponse(),
            ReportsPageResponse([]),
            ReportsPageResponse([]));
        var client = CreateClient(handler);

        await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-01-01"), DateOnly.Parse("2025-01-01"));
        await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-02-01"), DateOnly.Parse("2025-02-01"));

        handler.Requests.Should().HaveCount(3); // /me once + 2 Reports API calls
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/v9/me");
        handler.Requests[1].Method.Should().Be(HttpMethod.Post);
        handler.Requests[2].Method.Should().Be(HttpMethod.Post);
    }

    // ---------------------------------------------------------------------------
    // Pagination
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTimeEntriesAsync_PaginatesUntilHeaderAbsent()
    {
        var entry1 = new { id = 1L, description = "sleep 1", start = "2025-01-01T22:00:00+00:00", stop = "2025-01-02T06:00:00+00:00", dur = 3_600_000L, pid = (long?)null, project = (string?)null, tags = (string[]?)null };
        var entry2 = new { id = 2L, description = "sleep 2", start = "2025-01-02T22:00:00+00:00", stop = "2025-01-03T06:00:00+00:00", dur = 7_200_000L, pid = (long?)null, project = (string?)null, tags = (string[]?)null };

        var handler = BuildHandler(
            MeResponse(),
            ReportsPageResponse([entry1], nextRowNumber: 50),
            ReportsPageResponse([entry2]));
        var client = CreateClient(handler);

        var result = await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-01-01"), DateOnly.Parse("2025-01-02"));

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1L);
        result[1].Id.Should().Be(2L);

        // Me + 2 Reports calls
        handler.Requests.Should().HaveCount(3);
        handler.Requests[1].Method.Should().Be(HttpMethod.Post);
        handler.Requests[2].Method.Should().Be(HttpMethod.Post);
    }

    // ---------------------------------------------------------------------------
    // Rate limit retry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTimeEntriesAsync_RetriesOnRateLimitResponse()
    {
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimitResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

        var handler = BuildHandler(
            MeResponse(),
            rateLimitResponse,
            ReportsPageResponse([]));
        var client = CreateClient(handler);

        var result = await client.GetTimeEntriesAsync(FakeToken, DateOnly.Parse("2025-01-01"), DateOnly.Parse("2025-01-01"));

        result.Should().BeEmpty();
        // /me + 429 attempt + retry
        handler.Requests.Should().HaveCount(3);
    }

    // ---------------------------------------------------------------------------
    // Fake HttpMessageHandler
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
