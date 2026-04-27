namespace GoogleCalendarManagement.Services;

public sealed record CalendarColorOption(
    string Key,
    string DisplayName,
    string Hex,
    string ContrastTextHex);

public interface IColorMappingService
{
    IReadOnlyList<CalendarColorOption> PickerColors { get; }
    string GetHexColor(string? colorId);
    string GetDisplayName(string? colorId);
    string NormalizeColorKey(string? colorId);
    string GetColorName(string? colorId);
    IReadOnlyDictionary<string, string> AllColors { get; }
}
