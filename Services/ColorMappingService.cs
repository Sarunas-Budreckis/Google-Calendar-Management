namespace GoogleCalendarManagement.Services;

public sealed class ColorMappingService : IColorMappingService
{
    private const string FallbackKey = "azure";
    private const string FallbackHex = "#0088CC";
    private const string FallbackName = "Azure";
    private const string ContrastTextHex = "#FFFFFF";

    private static readonly IReadOnlyList<CalendarColorOption> OrderedPickerColors =
    [
        new("azure", "Azure", "#0088CC", ContrastTextHex),
        new("purple", "Purple", "#3F51B5", ContrastTextHex),
        new("grey", "Grey", "#616161", ContrastTextHex),
        new("yellow", "Yellow", "#F6BF26", ContrastTextHex),
        new("navy", "Navy", "#33B679", ContrastTextHex),
        new("sage", "Sage", "#0B8043", ContrastTextHex),
        new("flamingo", "Flamingo", "#E67C73", ContrastTextHex),
        new("orange", "Orange", "#F4511E", ContrastTextHex),
        new("lavender", "Lavender", "#8E24AA", ContrastTextHex)
    ];

    private static readonly IReadOnlyDictionary<string, CalendarColorOption> PickerColorMap =
        OrderedPickerColors.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> AliasToCanonicalKeyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "azure",    "azure" },
            { "1",        "azure" },
            { "purple",   "purple" },
            { "9",        "purple" },
            { "grey",     "grey" },
            { "8",        "grey" },
            { "yellow",   "yellow" },
            { "5",        "yellow" },
            { "navy",     "navy" },
            { "2",        "navy" },
            { "sage",     "sage" },
            { "10",       "sage" },
            { "flamingo", "flamingo" },
            { "4",        "flamingo" },
            { "orange",   "orange" },
            { "6",        "orange" },
            { "lavender", "lavender" },
            { "3",        "lavender" }
        };

    private static readonly IReadOnlyDictionary<string, string> ColorMap =
        AliasToCanonicalKeyMap.ToDictionary(
            entry => entry.Key,
            entry => PickerColorMap[entry.Value].Hex,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CalendarColorOption> PickerColors => OrderedPickerColors;

    public IReadOnlyDictionary<string, string> AllColors => ColorMap;

    public string GetHexColor(string? colorId)
    {
        var colorKey = NormalizeColorKey(colorId);
        return PickerColorMap.TryGetValue(colorKey, out var option)
            ? option.Hex
            : FallbackHex;
    }

    public string GetDisplayName(string? colorId)
    {
        var colorKey = NormalizeColorKey(colorId);
        return PickerColorMap.TryGetValue(colorKey, out var option)
            ? option.DisplayName
            : FallbackName;
    }

    public string NormalizeColorKey(string? colorId)
    {
        if (string.IsNullOrWhiteSpace(colorId))
        {
            return FallbackKey;
        }

        return AliasToCanonicalKeyMap.TryGetValue(colorId, out var normalizedKey)
            ? normalizedKey
            : FallbackKey;
    }

    public string GetColorName(string? colorId)
    {
        return GetDisplayName(colorId);
    }
}
