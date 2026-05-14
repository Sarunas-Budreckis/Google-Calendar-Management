using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceDayDataMarkerViewModel
{
    private static readonly Brush HasDataBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 34, 139, 73));
    private static readonly Brush NoDataBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 88, 88, 88));

    public DataSourceDayDataMarkerViewModel(DateOnly date, bool hasData, int? count)
    {
        Date = date;
        HasData = hasData;
        Count = count;
    }

    public DateOnly Date { get; }
    public bool HasData { get; }
    public int? Count { get; }
    public string DayLabel => Date.ToString("ddd");
    public string CountLabel => Count.HasValue ? Count.Value.ToString() : string.Empty;
    public Brush BackgroundBrush => HasData ? HasDataBrush : NoDataBrush;
}
