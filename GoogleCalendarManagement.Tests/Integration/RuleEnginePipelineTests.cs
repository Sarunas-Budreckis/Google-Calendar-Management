using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.Services.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

/// <summary>
/// Integrity matrix for the rule engine pipeline (Story 8.14): idempotency, manual-sacred,
/// auto-link application, first-rule-wins, reversal cleanup, generate-candidate, convergence after
/// a move, and shared action-group grouping.
/// </summary>
public sealed class RuleEnginePipelineTests : IDisposable
{
    private static readonly DateTime BaseUtc =
        DateTime.SpecifyKind(new DateTime(2026, 6, 10, 12, 0, 0), DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public RuleEnginePipelineTests()
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

    public void Dispose() => _connection.Dispose();

    // --- Tests ---

    [Fact] // 8.3
    public async Task RunPipeline_IsIdempotent_OnSecondRunWithNoDataChanges()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var engine = CreateEngine(LinkAllRule("rule-x", "evt-1"));

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        var firstRow = await GetLinkAsync(dpId);

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        var secondRow = await GetLinkAsync(dpId);

        (await CountLinksAsync()).Should().Be(1);
        secondRow.Should().NotBeNull();
        // The unchanged row was skipped, so its action group (write marker) did not change.
        secondRow!.ActionGroupId.Should().Be(firstRow!.ActionGroupId);
        secondRow.UpdatedAt.Should().Be(firstRow.UpdatedAt);
    }

    [Fact] // 8.4
    public async Task RunPipeline_DoesNotTouch_ManuallyLinkedDatapoint()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-A", BaseUtc, BaseUtc.AddMinutes(30));
        await SeedEventAsync("evt-B", BaseUtc, BaseUtc.AddMinutes(30));
        await new LinkService(_contextFactory).LinkAsync(dpId, "evt-A"); // manual

        var engine = CreateEngine(LinkAllRule("rule-x", "evt-B"));
        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row!.Origin.Should().Be("manual");
        row.EventId.Should().Be("evt-A");
        row.RuleId.Should().BeNull();
    }

    [Fact] // 8.5
    public async Task RunPipeline_WritesAutoLink_WithRuleIdAndActionGroup()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var engine = CreateEngine(LinkAllRule("spotify_auto_link", "evt-1"));

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("linked");
        row.Origin.Should().Be("auto_rule");
        row.EventId.Should().Be("evt-1");
        row.RuleId.Should().Be("spotify_auto_link");
        row.ActionGroupId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact] // 8.6
    public async Task RunPipeline_FirstRuleWins_WhenTwoRulesProposeForSameDatapoint()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-A", BaseUtc, BaseUtc.AddMinutes(30));
        await SeedEventAsync("evt-B", BaseUtc, BaseUtc.AddMinutes(30));
        var engine = CreateEngine(
            LinkAllRule("rule-first", "evt-A"),
            LinkAllRule("rule-second", "evt-B"));

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row!.RuleId.Should().Be("rule-first");
        row.EventId.Should().Be("evt-A");
    }

    [Fact] // 8.7
    public async Task ReverseAndRerun_DeletesAutoLink_WhenNoRuleReproposesIt()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        await new LinkService(_contextFactory).WriteAutoLinkAsync(dpId, "evt-1", "rule-x");
        var engine = CreateEngine(EmptyRule("rule-x")); // proposes nothing on re-run

        await engine.ReverseAndRerunAsync("evt-1");

        (await GetLinkAsync(dpId)).Should().BeNull();
    }

    [Fact] // 8.8
    public async Task ReverseAndRerun_LeavesManualLinkIntact()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        await new LinkService(_contextFactory).LinkAsync(dpId, "evt-1"); // manual
        var engine = CreateEngine(EmptyRule("rule-x"));

        await engine.ReverseAndRerunAsync("evt-1");

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.Origin.Should().Be("manual");
        row.EventId.Should().Be("evt-1");
    }

    [Fact] // 8.9
    public async Task RunForEventDelete_RemovesAutoLink_ButNotManualLink()
    {
        var autoDp = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        var manualDp = await SeedDataPointAsync("toggl", BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var links = new LinkService(_contextFactory);
        await links.WriteAutoLinkAsync(autoDp, "evt-1", "rule-x");
        await links.LinkAsync(manualDp, "evt-1");
        var engine = CreateEngine(EmptyRule("rule-x"));

        await engine.RunForEventDeleteAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));

        (await GetLinkAsync(autoDp)).Should().BeNull();
        var manualRow = await GetLinkAsync(manualDp);
        manualRow.Should().NotBeNull();
        manualRow!.Origin.Should().Be("manual");
    }

    [Fact] // 8.10
    public async Task RunPipeline_GenerateCandidate_CreatesEventAndLink()
    {
        var dpId = await SeedDataPointAsync("outlook", BaseUtc, BaseUtc.AddMinutes(60));
        var engine = CreateEngine(GenerateCandidateRule("outlook_generate_candidate", "Outlook Meeting"));

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        var firstRow = await GetLinkAsync(dpId);

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.Origin.Should().Be("auto_rule");
        row.State.Should().Be("linked");
        row.RuleId.Should().Be("outlook_generate_candidate");
        row.EventId.Should().NotBeNullOrWhiteSpace();
        row.EventId.Should().Be(firstRow!.EventId);
        (await CountEventsAsync(e => e.Lifecycle == "candidate")).Should().Be(1);

        var created = await GetEventAsync(row.EventId!);
        created.Should().NotBeNull();
        created!.Lifecycle.Should().Be("candidate");
        created.Publish.Should().Be("local_only");
        created.SourceSystem.Should().Be("auto_rule");
        created.Summary.Should().Be("Outlook Meeting");
    }

    [Fact] // 8.11
    public async Task RunForEventEditTime_ConvergesAfterMove_RemovingStaleAutoLink()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        var oldStart = BaseUtc;
        var oldEnd = BaseUtc.AddMinutes(30);
        await SeedEventAsync("evt-1", oldStart, oldEnd);
        var engine = CreateEngine(new OverlapLinkRule(_contextFactory));

        // Initial pipeline auto-links the datapoint to the single overlapping event.
        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        (await GetLinkAsync(dpId))!.Origin.Should().Be("auto_rule");

        // Move the event far away (next day), then fire the time-change trigger with the old range.
        var newStart = BaseUtc.AddDays(1);
        await MoveEventAsync("evt-1", newStart, newStart.AddMinutes(30));
        await engine.RunForEventEditTimeAsync("evt-1", oldStart, oldEnd);

        // The datapoint no longer has any overlapping event → its stale auto link is gone.
        (await GetLinkAsync(dpId)).Should().BeNull();
    }

    [Fact] // 8.12
    public async Task RunPipeline_AllOpsInRun_ShareOneActionGroup_AndAreUndoable()
    {
        var dp1 = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        var dp2 = await SeedDataPointAsync("spotify", BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));
        var dp3 = await SeedDataPointAsync("spotify", BaseUtc.AddMinutes(10), BaseUtc.AddMinutes(13));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var linkService = new LinkService(_contextFactory);
        var engine = CreateEngine(linkService, LinkAllRule("rule-x", "evt-1"));

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var groups = new[]
        {
            (await GetLinkAsync(dp1))!.ActionGroupId,
            (await GetLinkAsync(dp2))!.ActionGroupId,
            (await GetLinkAsync(dp3))!.ActionGroupId
        };
        groups.Distinct().Should().ContainSingle();

        await linkService.UndoActionGroupAsync(groups[0]);

        (await GetLinkAsync(dp1)).Should().BeNull();
        (await GetLinkAsync(dp2)).Should().BeNull();
        (await GetLinkAsync(dp3)).Should().BeNull();
    }

    [Fact]
    public async Task ReverseAndRerun_DoesNotDelete_NonEngineCandidateEvent()
    {
        var dpId = await SeedDataPointAsync("spotify", BaseUtc, BaseUtc.AddMinutes(3));
        await SeedEventAsync("candidate-1", BaseUtc, BaseUtc.AddMinutes(30), lifecycle: "candidate", sourceSystem: "manual");
        await new LinkService(_contextFactory).WriteAutoLinkAsync(dpId, "candidate-1", "rule-x");
        var engine = CreateEngine(EmptyRule("rule-x"));

        await engine.ReverseAndRerunAsync("candidate-1");

        (await GetLinkAsync(dpId)).Should().BeNull();
        (await GetEventAsync("candidate-1")).Should().NotBeNull();
    }

    // --- Engine + rule helpers ---

    private RuleEngineService CreateEngine(params ILinkRule[] rules)
    {
        return CreateEngine(new LinkService(_contextFactory), rules);
    }

    private RuleEngineService CreateEngine(ILinkService linkService, params ILinkRule[] rules)
    {
        var eventRepository = new EventRepository(_contextFactory);
        var identity = new EventIdentityService(eventRepository);
        return new RuleEngineService(
            _contextFactory, eventRepository, identity, linkService, rules,
            NullLogger<RuleEngineService>.Instance);
    }

    private static ILinkRule LinkAllRule(string ruleId, string eventId) =>
        new StubRule(ruleId, eligible => eligible
            .Select(dp => RuleProposedOp.Link(dp.DataPointId, eventId, ruleId))
            .ToList());

    private static ILinkRule EmptyRule(string ruleId) =>
        new StubRule(ruleId, _ => Array.Empty<RuleProposedOp>());

    private static ILinkRule GenerateCandidateRule(string ruleId, string summary) =>
        new StubRule(ruleId, eligible => eligible
            .Select(dp => RuleProposedOp.GenerateCandidate(
                dp.DataPointId, ruleId, summary, dp.StartUtc, dp.EndUtc))
            .ToList());

    private sealed class StubRule : ILinkRule
    {
        private readonly Func<IReadOnlyList<EligibleDataPoint>, IReadOnlyList<RuleProposedOp>> _logic;

        public StubRule(string ruleId, Func<IReadOnlyList<EligibleDataPoint>, IReadOnlyList<RuleProposedOp>> logic)
        {
            RuleId = ruleId;
            _logic = logic;
        }

        public string RuleId { get; }

        public Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(
            RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct) =>
            Task.FromResult(_logic(eligible));
    }

    /// <summary>Links each eligible datapoint to the single approved event overlapping its extent.</summary>
    private sealed class OverlapLinkRule : ILinkRule
    {
        private readonly IDbContextFactory<CalendarDbContext> _factory;

        public OverlapLinkRule(IDbContextFactory<CalendarDbContext> factory) => _factory = factory;

        public string RuleId => "overlap_link";

        public async Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(
            RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct)
        {
            var ops = new List<RuleProposedOp>();
            await using var context = await _factory.CreateDbContextAsync(ct);

            foreach (var dp in eligible)
            {
                var overlapping = await context.Events.AsNoTracking()
                    .Where(e => e.Lifecycle == "approved" && !e.IsDeleted
                        && e.StartDatetime < dp.EndUtc
                        && (e.EndDatetime ?? e.StartDatetime) > dp.StartUtc)
                    .Select(e => e.EventId)
                    .ToListAsync(ct);

                if (overlapping.Count == 1)
                {
                    ops.Add(RuleProposedOp.Link(dp.DataPointId, overlapping[0], RuleId));
                }
            }

            return ops;
        }
    }

    // --- Seed / query helpers ---

    private static RuleScope ScopeFor(DateTime utc)
    {
        var day = DateOnly.FromDateTime(utc.ToLocalTime());
        return new RuleScope(day, day);
    }

    private async Task<int> SeedDataPointAsync(string sourceKey, DateTime startUtc, DateTime endUtc)
    {
        var dp = new DataPoint
        {
            SourceKey = sourceKey,
            SourceRef = Guid.NewGuid().ToString("N"),
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAt = DateTime.UtcNow
        };

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.DataPoints.Add(dp);
        await context.SaveChangesAsync();
        return dp.DataPointId;
    }

    private async Task SeedEventAsync(
        string eventId,
        DateTime startUtc,
        DateTime endUtc,
        string lifecycle = "approved",
        string? sourceSystem = null)
    {
        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Events.Add(new Event
        {
            EventId = eventId,
            Lifecycle = lifecycle,
            Publish = "local_only",
            StartDatetime = startUtc,
            EndDatetime = endUtc,
            SourceSystem = sourceSystem,
            HasUnpublishedChanges = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private async Task MoveEventAsync(string eventId, DateTime startUtc, DateTime endUtc)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ev = await context.Events.SingleAsync(e => e.EventId == eventId);
        ev.StartDatetime = startUtc;
        ev.EndDatetime = endUtc;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task<Link?> GetLinkAsync(int dataPointId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Links.AsNoTracking()
            .SingleOrDefaultAsync(l => l.DataPointId == dataPointId);
    }

    private async Task<Event?> GetEventAsync(string eventId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Events.AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId);
    }

    private async Task<int> CountLinksAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Links.CountAsync();
    }

    private async Task<int> CountEventsAsync(Func<Event, bool> predicate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return context.Events.AsNoTracking().AsEnumerable().Count(predicate);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
