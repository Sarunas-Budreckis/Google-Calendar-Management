using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GoogleCalendarManagement.Views.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && hex.StartsWith('#') && hex.Length >= 7)
        {
            try
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch
            {
                // fall through to default
            }
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
