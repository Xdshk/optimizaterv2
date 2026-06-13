using System.Globalization;
using System.Windows.Data;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace GTA5Optimizer.UI.Converters;

public class BytesToMBConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
            return $"{bytes / 1024.0 / 1024.0:F0} MB";
        if (value is double d)
            return $"{d / 1024.0 / 1024.0:F0} MB";
        return "0 MB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class HasContentToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int count => count > 0,
            bool flag => flag,
            null => false,
            _ => true
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double severity)
        {
            if (severity > 80)
                return new MediaSolidColorBrush(MediaColor.FromRgb(0xFF, 0x44, 0x44));
            if (severity > 50)
                return new MediaSolidColorBrush(MediaColor.FromRgb(0xFF, 0xAA, 0x00));
            return new MediaSolidColorBrush(MediaColor.FromRgb(0x00, 0xFF, 0x88));
        }
        return new MediaSolidColorBrush(MediaColor.FromRgb(0x88, 0x88, 0x88));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
