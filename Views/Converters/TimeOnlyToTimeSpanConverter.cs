using Microsoft.UI.Xaml.Data;

namespace GoogleCalendarManagement.Views.Converters;

public sealed class TimeOnlyToTimeSpanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is TimeOnly time ? time.ToTimeSpan() : TimeSpan.Zero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is TimeSpan timeSpan
            ? TimeOnly.FromTimeSpan(timeSpan)
            : TimeOnly.MinValue;
    }
}
