using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneClassificationService : ITogglPhoneClassificationService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ITogglPhoneRuleRepository _ruleRepository;

    public TogglPhoneClassificationService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ITogglPhoneRuleRepository ruleRepository)
    {
        _contextFactory = contextFactory;
        _ruleRepository = ruleRepository;
    }

    public async Task ClassifyAllAsync(CancellationToken ct = default)
    {
        var activeRules = await _ruleRepository.GetActiveRulesAsync(ct);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var candidates = await context.TogglEntries
            .Where(e => e.TogglDataType == null || e.TogglDataType == TogglDataType.TogglPhone)
            .ToListAsync(ct);

        foreach (var entry in candidates)
        {
            entry.TogglDataType = MatchesAnyRule(entry, activeRules) ? TogglDataType.TogglPhone : null;
        }

        await context.SaveChangesAsync(ct);
    }

    private static bool MatchesAnyRule(TogglEntry entry, IReadOnlyList<TogglPhoneRule> rules)
    {
        var entryDate = DateOnly.FromDateTime(
            entry.StartTime.Kind == DateTimeKind.Utc
                ? entry.StartTime.ToLocalTime()
                : entry.StartTime);

        var startUtc = entry.StartTime.Kind == DateTimeKind.Utc
            ? entry.StartTime
            : entry.StartTime.ToUniversalTime();
        var durationMinutes = entry.DurationSeconds.HasValue
            ? entry.DurationSeconds.Value / 60.0
            : (DateTime.UtcNow - startUtc).TotalMinutes;

        foreach (var rule in rules)
        {
            if (rule.DateFrom.HasValue && entryDate < rule.DateFrom.Value)
            {
                continue;
            }

            if (rule.DateTo.HasValue && entryDate > rule.DateTo.Value)
            {
                continue;
            }

            if (!string.Equals(entry.Description, rule.DescriptionPattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.MaxDurationMinutes.HasValue && durationMinutes > rule.MaxDurationMinutes.Value)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
