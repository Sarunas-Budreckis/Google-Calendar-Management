using System.Data.Common;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class CoverageService : ICoverageService
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public CoverageService(IDbContextFactory<CalendarDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    public async Task<CoverageResult> GetDateSourceCoverageAsync(DateOnly date, string sourceKey, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(DISTINCT dp.data_point_id),
                       COUNT(DISTINCT CASE WHEN l.link_id IS NOT NULL THEN dp.data_point_id END)
                FROM data_point dp
                LEFT JOIN link l ON l.data_point_id = dp.data_point_id
                WHERE dp.source_key = @sk AND dp.start_utc >= @s AND dp.start_utc < @e";
            cmd.Parameters.Add(new SqliteParameter("@sk", sourceKey));
            cmd.Parameters.Add(new SqliteParameter("@s", start));
            cmd.Parameters.Add(new SqliteParameter("@e", end));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var total = reader.GetInt32(0);
                var covered = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return BuildResult(total, covered);
            }
            return new CoverageResult(0, 0, CoverageLevel.Full);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // no such table
        {
            return await FallbackCountOnlyAsync(conn, sourceKey, start, end, ct);
        }
    }

    public async Task<CoverageResult> GetDayCoverageAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(DISTINCT dp.data_point_id),
                       COUNT(DISTINCT CASE WHEN l.link_id IS NOT NULL THEN dp.data_point_id END)
                FROM data_point dp
                LEFT JOIN link l ON l.data_point_id = dp.data_point_id
                WHERE dp.start_utc >= @s AND dp.start_utc < @e";
            cmd.Parameters.Add(new SqliteParameter("@s", start));
            cmd.Parameters.Add(new SqliteParameter("@e", end));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var total = reader.GetInt32(0);
                var covered = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return BuildResult(total, covered);
            }
            return new CoverageResult(0, 0, CoverageLevel.Full);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            return await FallbackDayCountOnlyAsync(conn, start, end, ct);
        }
    }

    public Task<CoverageResult> GetEventCoverageAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
        => throw new NotImplementedException("GetEventCoverageAsync is implemented in Story 8.12 when the link table lands.");

    private static CoverageResult BuildResult(int total, int covered)
    {
        if (total == 0) return new CoverageResult(0, 0, CoverageLevel.Full);
        if (covered >= total) return new CoverageResult(total, covered, CoverageLevel.Full);
        if (covered > 0) return new CoverageResult(total, covered, CoverageLevel.Partial);
        return new CoverageResult(total, 0, CoverageLevel.None);
    }

    private static async Task<CoverageResult> FallbackCountOnlyAsync(DbConnection conn, string sourceKey, DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM data_point WHERE source_key=@sk AND start_utc>=@s AND start_utc<@e";
            cmd.Parameters.Add(new SqliteParameter("@sk", sourceKey));
            cmd.Parameters.Add(new SqliteParameter("@s", start));
            cmd.Parameters.Add(new SqliteParameter("@e", end));
            var total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return new CoverageResult(total, 0, total > 0 ? CoverageLevel.None : CoverageLevel.Full);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // data_point table also absent (pre-8.7)
        {
            return new CoverageResult(0, 0, CoverageLevel.Full);
        }
    }

    private static async Task<CoverageResult> FallbackDayCountOnlyAsync(DbConnection conn, DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM data_point WHERE start_utc>=@s AND start_utc<@e";
            cmd.Parameters.Add(new SqliteParameter("@s", start));
            cmd.Parameters.Add(new SqliteParameter("@e", end));
            var total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return new CoverageResult(total, 0, total > 0 ? CoverageLevel.None : CoverageLevel.Full);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // data_point table also absent (pre-8.7)
        {
            return new CoverageResult(0, 0, CoverageLevel.Full);
        }
    }
}
