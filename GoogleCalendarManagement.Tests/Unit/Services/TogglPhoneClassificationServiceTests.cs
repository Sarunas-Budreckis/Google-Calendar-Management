using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class TogglPhoneClassificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<ITogglPhoneRuleRepository> _ruleRepository = new();

    public TogglPhoneClassificationServiceTests()
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

    public void Dispose()
    {
        _connection.Dispose();
    }

    private TogglPhoneClassificationService CreateService()
    {
        return new TogglPhoneClassificationService(_contextFactory, _ruleRepository.Object);
    }

    private static TogglEntry MakeEntry(long id, string description, int durationSeconds, DateTime? startTime = null)
    {
        return new TogglEntry
        {
            TogglId = id,
            Description = description,
            DurationSeconds = durationSeconds,
            StartTime = startTime ?? new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            EndTime = (startTime ?? new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)).AddSeconds(durationSeconds),
            CreatedAt = DateTime.UtcNow
        };
    }

    private void SetupRules(IReadOnlyList<TogglPhoneRule> rules)
    {
        _ruleRepository
            .Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);
    }

    [Fact]
    public async Task ClassifyAllAsync_WhenNoRules_LeavesEntriesUnclassified()
    {
        SetupRules([]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 300));
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAllAsync_MatchingDescriptionAndDuration_TagsAsTogglPhone()
    {
        SetupRules([new TogglPhoneRule { Id = 1, DescriptionPattern = "Phone", MaxDurationMinutes = 10, IsActive = true }]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 300)); // 5 min → matches
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().Be(TogglDataType.TogglPhone);
    }

    [Fact]
    public async Task ClassifyAllAsync_MatchingDescriptionButTooLong_LeavesUnclassified()
    {
        SetupRules([new TogglPhoneRule { Id = 1, DescriptionPattern = "Phone", MaxDurationMinutes = 10, IsActive = true }]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 900)); // 15 min → exceeds max 10 min
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAllAsync_CaseInsensitiveMatch_TagsEntry()
    {
        SetupRules([new TogglPhoneRule { Id = 1, DescriptionPattern = "Phone", MaxDurationMinutes = 10, IsActive = true }]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "PHONE", 300)); // case-insensitive match
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().Be(TogglDataType.TogglPhone);
    }

    [Fact]
    public async Task ClassifyAllAsync_NoMaxDuration_MatchesAnyDuration()
    {
        SetupRules([new TogglPhoneRule { Id = 1, DescriptionPattern = "Phone", MaxDurationMinutes = null, IsActive = true }]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 3600)); // 1 hour → no limit
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().Be(TogglDataType.TogglPhone);
    }

    [Fact]
    public async Task ClassifyAllAsync_DateRangeRule_OnlyMatchesEntriesInRange()
    {
        var inRangeDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var outOfRangeDate = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        SetupRules([new TogglPhoneRule
        {
            Id = 1,
            DescriptionPattern = "Phone",
            MaxDurationMinutes = 10,
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = null,
            IsActive = true
        }]);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 300, inRangeDate));    // in range
        context.TogglEntries.Add(MakeEntry(2, "Phone", 300, outOfRangeDate)); // before range
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entries = await verify.TogglEntries.OrderBy(e => e.TogglId).ToListAsync();
        entries[0].TogglDataType.Should().Be(TogglDataType.TogglPhone);   // in range → tagged
        entries[1].TogglDataType.Should().BeNull();                        // out of range → not tagged
    }

    [Fact]
    public async Task ClassifyAllAsync_IsIdempotent()
    {
        SetupRules([new TogglPhoneRule { Id = 1, DescriptionPattern = "Phone", MaxDurationMinutes = 10, IsActive = true }]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "Phone", 300));
        await context.SaveChangesAsync();

        var service = CreateService();
        await service.ClassifyAllAsync();
        await service.ClassifyAllAsync(); // run again

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entry = await verify.TogglEntries.SingleAsync();
        entry.TogglDataType.Should().Be(TogglDataType.TogglPhone);
    }

    [Fact]
    public async Task ClassifyAllAsync_MultipleRules_OrLogic()
    {
        SetupRules([
            new TogglPhoneRule { Id = 1, DescriptionPattern = "ToDelete", MaxDurationMinutes = 10, IsActive = true },
            new TogglPhoneRule { Id = 2, DescriptionPattern = "Phone", MaxDurationMinutes = 10, IsActive = true }
        ]);
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TogglEntries.Add(MakeEntry(1, "ToDelete", 300));
        context.TogglEntries.Add(MakeEntry(2, "Phone", 300));
        context.TogglEntries.Add(MakeEntry(3, "Sleep", 300)); // no rule → not tagged
        await context.SaveChangesAsync();

        await CreateService().ClassifyAllAsync();

        await using var verify = await _contextFactory.CreateDbContextAsync();
        var entries = await verify.TogglEntries.OrderBy(e => e.TogglId).ToListAsync();
        entries[0].TogglDataType.Should().Be(TogglDataType.TogglPhone);
        entries[1].TogglDataType.Should().Be(TogglDataType.TogglPhone);
        entries[2].TogglDataType.Should().BeNull();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(new CalendarDbContext(_options));
    }
}
