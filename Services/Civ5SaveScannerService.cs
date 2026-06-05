using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class Civ5SaveScannerService : ICiv5SaveScannerService
{
    public const string SourceKey = "civ5";

    private static readonly string[] SaveRootPaths =
    [
        @"C:\Users\Sarunas Budreckis\Documents\My Games\Sid Meier's Civilization 5\Saves",
        @"C:\Users\Sarunas Budreckis\Documents\My Games\Sid Meier's Civilization 5\ModdedSaves"
    ];

    private static readonly HashSet<string> KnownGameModes =
        new(StringComparer.OrdinalIgnoreCase) { "single", "multi", "hotseat", "pbem", "pitboss" };

    private static readonly string[] SaveExtensions = [".Civ5Save", ".CivBeyondSwordSave"];

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ICiv5SessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Civ5SaveScannerService> _logger;

    public Civ5SaveScannerService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ICiv5SessionRepository repository,
        ILogger<Civ5SaveScannerService> logger,
        TimeProvider? timeProvider = null)
    {
        _contextFactory = contextFactory;
        _repository = repository;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Civ5ScanResult> ScanAsync(CancellationToken ct = default)
    {
        var source = await EnsureDataSourceAsync(ct);
        var newPointsAdded = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var candidates = CollectCandidates();
            if (candidates.Count == 0)
            {
                success = true;
                return new Civ5ScanResult(true, 0, null);
            }

            var existingKeys = await _repository.GetExistingDedupKeysAsync(
                candidates.Select(c => c.FileModifiedAt).ToList(), ct);

            var newPoints = candidates
                .Where(c => !existingKeys.Contains((c.FileModifiedAt, c.GameMode)))
                .Select(c => new Civ5SessionPoint
                {
                    ScannedAt = _timeProvider.GetUtcNow().UtcDateTime,
                    FileModifiedAt = c.FileModifiedAt,
                    GameMode = c.GameMode
                })
                .ToList();

            await _repository.InsertPointsAsync(newPoints, ct);
            newPointsAdded = newPoints.Count;
            success = true;
            return new Civ5ScanResult(true, newPointsAdded, null);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Civ5 save scan failed");
            return new Civ5ScanResult(false, newPointsAdded, errorMessage);
        }
        finally
        {
            await WriteImportLogAsync(source.DataSourceId, newPointsAdded, success, errorMessage, ct);
            WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(source.DataSourceId, SourceKey, success));
        }
    }

    private List<(DateTime FileModifiedAt, string GameMode)> CollectCandidates()
    {
        var results = new List<(DateTime FileModifiedAt, string GameMode)>();

        foreach (var root in SaveRootPaths)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f => SaveExtensions.Any(ext =>
                        f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot enumerate saves under {Root}", root);
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    var modifiedAt = info.LastWriteTimeUtc;
                    var gameMode = DetermineGameMode(root, file);
                    results.Add((modifiedAt, gameMode));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot read metadata for {File}", file);
                }
            }
        }

        return results;
    }

    public static string DetermineGameMode(string rootPath, string filePath)
    {
        var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relativePath = filePath.Substring(normalized.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], 2);

        if (parts.Length < 2)
        {
            return "unknown";
        }

        var subfolder = parts[0];
        return KnownGameModes.Contains(subfolder) ? subfolder.ToLowerInvariant() : "unknown";
    }

    private async Task<DataSource> EnsureDataSourceAsync(CancellationToken ct)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await ctx.DataSources.SingleOrDefaultAsync(s => s.SourceKey == SourceKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var source = new DataSource
        {
            SourceKey = SourceKey,
            DisplayName = "Civilization 5",
            SupportsNoDataHint = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        ctx.DataSources.Add(source);
        await ctx.SaveChangesAsync(ct);
        return source;
    }

    private async Task WriteImportLogAsync(
        int dataSourceId, int recordsFetched, bool success, string? errorMessage, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        ctx.DataSourceImportLogs.Add(new DataSourceImportLog
        {
            DataSourceId = dataSourceId,
            CoveredStartDate = today,
            CoveredEndDate = today,
            ImportedAt = now,
            RecordsFetched = recordsFetched,
            Success = success,
            ErrorMessage = errorMessage
        });
        await ctx.SaveChangesAsync(ct);
    }
}
