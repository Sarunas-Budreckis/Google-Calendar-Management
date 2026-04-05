namespace GoogleCalendarManagement.Services;

public sealed class ColorMappingService : IColorMappingService
{
    private const string FallbackHex = "#0088CC";
    private const string FallbackName = "Azure";

    private static readonly IReadOnlyDictionary<string, string> ColorMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // GCal event colorId "1" = Lavender (#7986CB).
            // "azure" is kept as a legacy string alias only; numeric ID 1 is lavender.
            { "lavender", "#7986CB" },
            { "1",        "#7986CB" },
            { "purple",   "#3F51B5" },
            { "9",        "#3F51B5" },
            { "grey",     "#616161" },
            { "8",        "#616161" },
            { "yellow",   "#F6BF26" },
            { "5",        "#F6BF26" },
            { "navy",     "#33B679" },
            { "2",        "#33B679" },
            { "sage",     "#0B8043" },
            { "10",       "#0B8043" },
            { "flamingo", "#E67C73" },
            { "4",        "#E67C73" },
            { "orange",   "#F4511E" },
            { "6",        "#F4511E" },
            { "azure",    "#0088CC" },
            { "3",        "#8E24AA" },
        };

    private static readonly IReadOnlyDictionary<string, string> NameMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1",        "Lavender" },
            { "lavender", "Lavender" },
            { "2",        "Navy" },
            { "navy",     "Navy" },
            { "3",        "Grape" },
            { "4",        "Flamingo" },
            { "flamingo", "Flamingo" },
            { "5",        "Yellow" },
            { "yellow",   "Yellow" },
            { "6",        "Orange" },
            { "orange",   "Orange" },
            { "8",        "Grey" },
            { "grey",     "Grey" },
            { "9",        "Purple" },
            { "purple",   "Purple" },
            { "10",       "Sage" },
            { "sage",     "Sage" },
            { "azure",    "Azure" },
        };

    public IReadOnlyDictionary<string, string> AllColors => ColorMap;

    public string GetHexColor(string? colorId)
    {
        if (string.IsNullOrEmpty(colorId))
            return FallbackHex;

        return ColorMap.TryGetValue(colorId, out var hex) ? hex : FallbackHex;
    }

    public string GetColorName(string? colorId)
    {
        if (string.IsNullOrEmpty(colorId))
            return FallbackName;

        return NameMap.TryGetValue(colorId, out var name) ? name : FallbackName;
    }
}
