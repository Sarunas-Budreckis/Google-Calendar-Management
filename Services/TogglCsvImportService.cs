using System.Globalization;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglCsvImportService : ITogglCsvImportService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ITogglPhoneRuleRepository _phoneRuleRepository;
    private readonly TimeProvider _timeProvider;

    public TogglCsvImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ITogglPhoneRuleRepository phoneRuleRepository,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _phoneRuleRepository = phoneRuleRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TogglCsvImportResult> ImportFromStreamAsync(Stream stream, CancellationToken ct = default)
    {
        List<TogglEntry> entries;
        var malformed = 0;

        try
        {
            var phoneRules = await _phoneRuleRepository.GetActiveRulesAsync(ct);
            (entries, malformed) = ParseCsv(stream, phoneRules);
        }
        catch (Exception ex) when (ex is IOException or FormatException)
        {
            return new TogglCsvImportResult(false, 0, 0, 0, $"Failed to read CSV: {ex.Message}");
        }

        if (entries.Count == 0 && malformed == 0)
        {
            return new TogglCsvImportResult(true, 0, 0, 0, null);
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var candidateIds = entries.Select(e => e.TogglId).ToList();
            var existingIds = await context.TogglEntries
                .Where(e => candidateIds.Contains(e.TogglId))
                .Select(e => e.TogglId)
                .ToHashSetAsync(ct);

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            // Also check for StartTime conflicts — same time slot may already exist under a real API TogglId
            var candidateStartTimes = entries.Select(e => e.StartTime).ToList();
            var conflictingStartTimes = await context.TogglEntries
                .Where(e => candidateStartTimes.Contains(e.StartTime) && !candidateIds.Contains(e.TogglId))
                .Select(e => e.StartTime)
                .ToHashSetAsync(ct);

            var inserted = 0;
            var skipped = 0;
            var seenInBatch = new HashSet<long>();

            foreach (var entry in entries)
            {
                if (existingIds.Contains(entry.TogglId) ||
                    conflictingStartTimes.Contains(entry.StartTime) ||
                    !seenInBatch.Add(entry.TogglId))
                {
                    skipped++;
                    continue;
                }

                entry.CreatedAt = nowUtc;
                context.TogglEntries.Add(entry);
                inserted++;
            }

            await context.SaveChangesAsync(ct);
            return new TogglCsvImportResult(true, inserted, skipped, malformed, null);
        }
        catch (Exception ex) when (ex is DbUpdateException or OperationCanceledException)
        {
            return new TogglCsvImportResult(false, 0, 0, malformed, $"Database error: {ex.Message}");
        }
    }

    private static (List<TogglEntry> entries, int malformed) ParseCsv(Stream stream, IReadOnlyList<TogglPhoneRule> phoneRules)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var entries = new List<TogglEntry>();
        var malformed = 0;

        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return (entries, malformed);
        }

        var headers = ParseCsvRow(headerLine);
        var colDescription = Array.FindIndex(headers, h => h.Equals("Description", StringComparison.OrdinalIgnoreCase));
        var colDuration = Array.FindIndex(headers, h => h.Equals("Duration", StringComparison.OrdinalIgnoreCase));
        var colProject = Array.FindIndex(headers, h => h.Equals("Project", StringComparison.OrdinalIgnoreCase));
        var colTags = Array.FindIndex(headers, h => h.Equals("Tags", StringComparison.OrdinalIgnoreCase));
        var colStartDate = Array.FindIndex(headers, h => h.Equals("Start date", StringComparison.OrdinalIgnoreCase));
        var colStartTime = Array.FindIndex(headers, h => h.Equals("Start time", StringComparison.OrdinalIgnoreCase));
        var colStopDate = Array.FindIndex(headers, h => h.Equals("Stop date", StringComparison.OrdinalIgnoreCase));
        var colStopTime = Array.FindIndex(headers, h => h.Equals("Stop time", StringComparison.OrdinalIgnoreCase));

        if (colStartDate < 0 || colStartTime < 0)
        {
            throw new FormatException("CSV is missing required 'Start date' or 'Start time' columns.");
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = ParseCsvRow(line);

            if (!TryParseLocalDateTime(cols, colStartDate, colStartTime, out var startLocal))
            {
                malformed++;
                continue;
            }

            var startUtc = startLocal.ToUniversalTime();
            var togglId = new DateTimeOffset(startUtc).ToUnixTimeMilliseconds();

            DateTime? endUtc = null;
            if (TryParseLocalDateTime(cols, colStopDate, colStopTime, out var endLocal))
            {
                endUtc = endLocal.ToUniversalTime();
            }

            int? durationSeconds = null;
            if (colDuration >= 0 && colDuration < cols.Length &&
                TimeSpan.TryParse(cols[colDuration], CultureInfo.InvariantCulture, out var duration))
            {
                durationSeconds = (int)duration.TotalSeconds;
            }

            var description = colDescription >= 0 && colDescription < cols.Length ? NullIfDash(cols[colDescription]) : null;
            var project = colProject >= 0 && colProject < cols.Length ? NullIfDash(cols[colProject]) : null;
            var tags = colTags >= 0 && colTags < cols.Length ? NullIfDash(cols[colTags]) : null;

            var dataType = ClassifyEntry(description, project, startUtc, durationSeconds, phoneRules);

            entries.Add(new TogglEntry
            {
                TogglId = togglId,
                Description = description,
                StartTime = startUtc,
                EndTime = endUtc,
                DurationSeconds = durationSeconds,
                ProjectName = project,
                Tags = tags,
                VisibleAsEvent = true,
                TogglDataType = dataType
            });
        }

        return (entries, malformed);
    }

    private static TogglDataType? ClassifyEntry(
        string? description,
        string? project,
        DateTime startUtc,
        int? durationSeconds,
        IReadOnlyList<TogglPhoneRule> phoneRules)
    {
        if (project?.Equals("Transit", StringComparison.OrdinalIgnoreCase) == true)
        {
            return TogglDataType.TogglTransit;
        }

        var entryDate = DateOnly.FromDateTime(startUtc.ToLocalTime());
        var durationMinutes = durationSeconds.HasValue
            ? durationSeconds.Value / 60.0
            : (DateTime.UtcNow - startUtc).TotalMinutes;

        foreach (var rule in phoneRules)
        {
            if (rule.DateFrom.HasValue && entryDate < rule.DateFrom.Value)
            {
                continue;
            }

            if (rule.DateTo.HasValue && entryDate > rule.DateTo.Value)
            {
                continue;
            }

            if (!string.Equals(description, rule.DescriptionPattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.MaxDurationMinutes.HasValue && durationMinutes > rule.MaxDurationMinutes.Value)
            {
                continue;
            }

            return TogglDataType.TogglPhone;
        }

        return null;
    }

    private static bool TryParseLocalDateTime(string[] cols, int dateCol, int timeCol, out DateTime result)
    {
        result = default;

        if (dateCol < 0 || dateCol >= cols.Length || timeCol < 0 || timeCol >= cols.Length)
        {
            return false;
        }

        var combined = $"{cols[dateCol].Trim()} {cols[timeCol].Trim()}";
        if (!DateTime.TryParseExact(combined, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        result = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
        return true;
    }

    private static string[] ParseCsvRow(string line)
    {
        var fields = new List<string>();
        var i = 0;

        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        i++;
                        if (i < line.Length && line[i] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',')
                {
                    i++;
                }
            }
            else
            {
                var end = line.IndexOf(',', i);
                if (end < 0)
                {
                    fields.Add(line[i..].Trim());
                    break;
                }
                fields.Add(line[i..end].Trim());
                i = end + 1;
                // trailing comma: append empty field for the missing last column
                if (i == line.Length)
                {
                    fields.Add(string.Empty);
                }
            }
        }

        return [.. fields];
    }

    private static string? NullIfDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;
    }
}
