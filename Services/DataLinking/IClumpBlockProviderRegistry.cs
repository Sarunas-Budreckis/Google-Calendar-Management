namespace GoogleCalendarManagement.Services.DataLinking;

public interface IClumpBlockProviderRegistry
{
    IClumpBlockProvider? GetProvider(string sourceKey);

    IReadOnlyList<IClumpBlockProvider> AllProviders { get; }
}
