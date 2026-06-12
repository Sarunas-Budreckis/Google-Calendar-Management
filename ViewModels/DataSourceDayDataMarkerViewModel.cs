using Microsoft.UI;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceDayDataMarkerViewModel
{
    private static readonly Brush NoDataBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 88, 88, 88));

    private readonly Brush _hasDataBrush;

    public DataSourceDayDataMarkerViewModel(DateOnly date, bool hasData, int? count, Func<DateOnly, Task>? openAction = null, string? sourceColorHex = null)
    {
        Date = date;
        HasData = hasData;
        Count = count;
        _hasDataBrush = ParseHasDataBrush(sourceColorHex);
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
    public Brush BackgroundBrush => HasData ? _hasDataBrush : NoDataBrush;
    public IAsyncRelayCommand OpenCommand { get; }

    private static Brush ParseHasDataBrush(string? colorHex)
    {
        if (!string.IsNullOrEmpty(colorHex))
        {
            try
            {
                var s = colorHex.TrimStart('#');
                var r = Convert.ToByte(s.Substring(0, 2), 16);
                var g = Convert.ToByte(s.Substring(2, 2), 16);
                var b = Convert.ToByte(s.Substring(4, 2), 16);
                return new SolidColorBrush(ColorHelper.FromArgb(0xFF, r, g, b));
            }
            catch (FormatException)
            {
            }
        }

        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, 34, 139, 73));
    }
}
