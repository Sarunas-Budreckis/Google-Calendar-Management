using Microsoft.UI.Xaml.Data;

namespace GoogleCalendarManagement.Views.Converters;

public sealed class DateOnlyToDateTimeOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateOnly date)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Local));
        }

        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return new DateTimeOffset(localDateTime);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is DateTimeOffset offset
            ? DateOnly.FromDateTime(offset.LocalDateTime.Date)
            : DateOnly.FromDateTime(DateTime.Today);
    }
}
