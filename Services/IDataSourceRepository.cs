using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IDataSourceRepository
{
    Task<IReadOnlyList<DataSource>> GetAllSourcesAsync(CancellationToken ct = default);
    Task<DataSource?> GetSourceByKeyAsync(string sourceKey, CancellationToken ct = default);
    Task<DataSource> UpsertSourceAsync(DataSource source, CancellationToken ct = default);
    Task<DateSourceIntegration?> GetIntegrationAsync(DateOnly date, int dataSourceId, CancellationToken ct = default);
    Task<DateSourceIntegration> SetIntegrationAsync(DateOnly date, int dataSourceId, bool integrated, CancellationToken ct = default);
    Task<DataSourceImportLog?> GetLastImportAsync(int dataSourceId, CancellationToken ct = default);
    Task<DataSourceImportLog> AddImportLogAsync(DataSourceImportLog log, CancellationToken ct = default);
    Task UpdateSourceColorAsync(int dataSourceId, string? colorHex, CancellationToken ct = default);
}
