using System.Globalization;
using System.Windows.Data;

namespace GTA5Optimizer.UI.Converters;

public class BytesToMBConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
            return $"{bytes / 1024.0 / 1024.0:F0} MB";
        return "0 MB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GreaterThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d && parameter is string s && double.TryParse(s, out var threshold))
            return d > threshold;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
