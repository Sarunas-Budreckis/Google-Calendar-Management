namespace GoogleCalendarManagement.Services;

public interface IColorMappingService
{
    string GetHexColor(string? colorId);
    IReadOnlyDictionary<string, string> AllColors { get; }
}
