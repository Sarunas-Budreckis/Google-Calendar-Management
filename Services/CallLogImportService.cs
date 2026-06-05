using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class CallLogImportService : ICallLogImportService
{
    public const string SourceKey = "call_log";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public CallLogImportService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CallLogImportResult> ImportFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var newInserted = 0;
        var duplicatesSkipped = 0;
        var success = false;
        string? errorMessage = null;
        DateOnly? coveredFrom = null;
        DateOnly? coveredTo = null;

        try
        {
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            List<CallLogCsvRow> rows;
            using (var reader = new StreamReader(stream, leaveOpen: true))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                rows = csv.GetRecords<CallLogCsvRow>().ToList();
            }

            if (rows.Count == 0)
            {
                success = true;
                return new CallLogImportResult(true, 0, 0, null);
            }

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var existingKeys = await context.CallLogEntries
                .AsNoTracking()
                .Select(e => new { e.Date, e.Number, e.DurationSeconds })
                .ToListAsync(ct);
            var existingSet = existingKeys
                .Select(k => (k.Date, k.Number, k.DurationSeconds))
                .ToHashSet();

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var dates = rows.Select(r => DateOnly.FromDateTime(r.Date)).ToList();
            coveredFrom = dates.Min();
            coveredTo = dates.Max();

            var import = new CallLogImport
            {
                ImportedAt = nowUtc,
                FileName = Path.GetFileName(fileName),
                RecordCount = 0,
                DateMin = dates.Min(),
                DateMax = dates.Max()
            };
            context.CallLogImports.Add(import);
            await context.SaveChangesAsync(ct);

            foreach (var row in rows)
            {
                var durationSeconds = (int)row.Duration.TotalSeconds;
                var dedupKey = (row.Date, row.Number, durationSeconds);

                if (existingSet.Contains(dedupKey))
                {
                    duplicatesSkipped++;
                    continue;
                }

                var entry = new CallLogEntry
                {
                    ImportId = import.Id,
                    CallType = row.CallType ?? "",
                    Date = row.Date,
                    DurationSeconds = durationSeconds,
                    Number = NullIfEmpty(row.Number),
                    Contact = NullIfEmpty(row.Contact),
                    Location = NullIfEmpty(row.Location),
                    Service = row.Service ?? ""
                };
                context.CallLogEntries.Add(entry);
                existingSet.Add(dedupKey);
                newInserted++;
            }

            import.RecordCount = newInserted;
            await context.SaveChangesAsync(ct);
            success = true;
            return new CallLogImportResult(true, newInserted, duplicatesSkipped, null);
        }
        catch (Exception ex) when (ex is CsvHelperException or IOException or InvalidDataException)
        {
            errorMessage = $"Failed to parse call log CSV: {ex.Message}";
            return new CallLogImportResult(false, newInserted, duplicatesSkipped, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, coveredFrom, coveredTo, newInserted, success, errorMessage, ct);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private async Task<DataSource> EnsureDataSourceAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.DataSources.SingleOrDefaultAsync(s => s.SourceKey == SourceKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var source = new DataSource
        {
            SourceKey = SourceKey,
            DisplayName = "iOS Call Log",
            SupportsNoDataHint = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        context.DataSources.Add(source);
        await context.SaveChangesAsync(ct);
        return source;
    }

    private async Task WriteImportLogAsync(
        int dataSourceId,
        DateOnly? coveredFrom,
        DateOnly? coveredTo,
        int recordsFetched,
        bool success,
        string? errorMessage,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(nowUtc);
        context.DataSourceImportLogs.Add(new DataSourceImportLog
        {
            DataSourceId = dataSourceId,
            CoveredStartDate = coveredFrom ?? today,
            CoveredEndDate = coveredTo ?? today,
            ImportedAt = nowUtc,
            RecordsFetched = recordsFetched,
            Success = success,
            ErrorMessage = errorMessage
        });
        await context.SaveChangesAsync(ct);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class CallLogCsvRow
    {
        [Name("Call type")]
        public string? CallType { get; set; }

        [Name("Date")]
        public DateTime Date { get; set; }

        [Name("Duration")]
        public TimeSpan Duration { get; set; }

        [Name("Number")]
        public string? Number { get; set; }

        [Name("Contact")]
        public string? Contact { get; set; }

        [Name("Location")]
        public string? Location { get; set; }

        [Name("Service")]
        public string? Service { get; set; }
    }
}
