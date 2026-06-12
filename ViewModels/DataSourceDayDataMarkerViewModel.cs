using Microsoft.UI;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceDayDataMarkerViewModel
{
    private const string DefaultHasDataColorHex = "#228B49";
    private const string NoDataColorHex = "#585858";

    private readonly string _hasDataColorHex;

    public DataSourceDayDataMarkerViewModel(DateOnly date, bool hasData, int? count, Func<DateOnly, Task>? openAction = null, string? sourceColorHex = null)
    {
        Date = date;
        HasData = hasData;
        Count = count;
        _hasDataColorHex = NormalizeColorHex(sourceColorHex, DefaultHasDataColorHex);
        OpenCommand = new AsyncRelayCommand(
            async () =>
            {
                if (openAction is not null)
                {
                    await openAction(Date);
                }
            },
            () => HasData && openAction is not null);
    }

    public DateOnly Date { get; }
    public bool HasData { get; }
    public int? Count { get; }
    public string DayLabel => Date.ToString("ddd");
    public string CountLabel => Count.HasValue ? Count.Value.ToString() : string.Empty;
    public Brush BackgroundBrush => CreateBrush(HasData ? _hasDataColorHex : NoDataColorHex);
    public IAsyncRelayCommand OpenCommand { get; }

    private static string NormalizeColorHex(string? colorHex, string fallback)
    {
        if (!string.IsNullOrEmpty(colorHex))
        {
            var s = colorHex.TrimStart('#');
            if (s.Length == 6 &&
                byte.TryParse(s[..2], System.Globalization.NumberStyles.HexNumber, null, out _) &&
                byte.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out _) &&
                byte.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                return $"#{s.ToUpperInvariant()}";
            }
        }

        return fallback;
    }

    private static Brush CreateBrush(string colorHex)
    {
        var s = colorHex.TrimStart('#');
        var r = Convert.ToByte(s[..2], 16);
        var g = Convert.ToByte(s.Substring(2, 2), 16);
        var b = Convert.ToByte(s.Substring(4, 2), 16);
        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, r, g, b));
    }
}
