namespace GoogleCalendarManagement.Services;

public sealed class ColorMappingService : IColorMappingService
{
    private const string FallbackKey = "azure";
    private const string FallbackHex = "#00AAFF";
    private const string FallbackName = "Azure";
    private const string ContrastTextHex = "#FFFFFF";

    private static readonly IReadOnlyList<CalendarColorOption> OrderedPickerColors =
    [
        new("red",      "Red",      "#CC0000", "#DA5234", ContrastTextHex),
        new("flamingo", "Flamingo", "#DF7B71", "#D6837A", ContrastTextHex),
        new("orange",   "Orange",   "#EA4F0A", "#E3683E", ContrastTextHex),
        new("banana",   "Banana",   "#F6BF26", "#E7BA51", ContrastTextHex),
        new("sage",     "Sage",     "#33B679", "#55B080", ContrastTextHex),
        new("basil",    "Basil",    "#0B8043", "#489160", ContrastTextHex),
        new("peacock",  "Peacock",  "#039BE5", "#4B99D2", ContrastTextHex),
        new("navy",     "Navy",     "#3F51B5", "#6E72C3", ContrastTextHex),
        new("lavender", "Lavender", "#7986CB", "#828BC2", ContrastTextHex),
        new("grape",    "Grape",    "#8E24AA", "#A75ABA", ContrastTextHex),
        new("graphite", "Graphite", "#616161", "#7C7C7C", ContrastTextHex),
        new("azure",    "Azure",    "#00AAFF", "#00AAFF", ContrastTextHex)
    ];

    private static readonly IReadOnlyDictionary<string, CalendarColorOption> PickerColorMap =
        OrderedPickerColors.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> AliasToCanonicalKeyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "red",      "red" },
            { "tomato",   "red" },
            { "11",       "red" },
            { "sage",     "sage" },
            { "2",        "sage" },
            { "flamingo", "flamingo" },
            { "4",        "flamingo" },
            { "orange",   "orange" },
            { "tangerine", "orange" },
            { "6",        "orange" },
            { "banana",   "banana" },
            { "yellow",   "banana" },
            { "5",        "banana" },
            { "basil",    "basil" },
            { "10",       "basil" },
            { "peacock",  "peacock" },
            { "7",        "peacock" },
            { "navy",     "navy" },
            { "blueberry", "navy" },
            { "blue",     "navy" },
            { "purple",   "navy" },
            { "9",        "navy" },
            { "lavender", "lavender" },
            { "1",        "lavender" },
            { "grape",    "grape" },
            { "3",        "grape" },
            { "graphite", "graphite" },
            { "grey",     "graphite" },
            { "gray",     "graphite" },
            { "8",        "graphite" },
            { "azure",    "azure" },
            { "0",        "azure" }
        };

    private static readonly IReadOnlyDictionary<string, string> ColorMap =
        AliasToCanonicalKeyMap.ToDictionary(
            entry => entry.Key,
            entry => PickerColorMap[entry.Value].Hex,
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> CanonicalToGoogleColorIdMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", "11" },
            { "flamingo", "4" },
            { "orange", "6" },
            { "banana", "5" },
            { "sage", "2" },
            { "basil", "10" },
            { "peacock", "7" },
            { "navy", "9" },
            { "lavender", "1" },
            { "grape", "3" },
            { "graphite", "8" },
            { "azure", "0" }
        };

    public IReadOnlyList<CalendarColorOption> PickerColors => OrderedPickerColors;

    public IReadOnlyDictionary<string, string> AllColors => ColorMap;

    public string GetHexColor(string? colorId)
    {
        var colorKey = NormalizeColorKey(colorId);
        return PickerColorMap.TryGetValue(colorKey, out var option)
            ? option.DisplayHex
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

    public string? GetGoogleColorId(string? colorId)
    {
        var colorKey = NormalizeColorKey(colorId);
        // Azure is the calendar's default color — send null so Google uses the calendar color.
        if (colorKey == FallbackKey)
        {
            return null;
        }

        return CanonicalToGoogleColorIdMap.TryGetValue(colorKey, out var googleColorId)
            ? googleColorId
            : null;
    }
}
