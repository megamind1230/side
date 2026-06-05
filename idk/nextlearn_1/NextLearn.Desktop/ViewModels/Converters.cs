using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NextLearn.Desktop.ViewModels;

public class ProgressConverter : IMultiValueConverter
{
    public static readonly ProgressConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is int current && values[1] is int total && total > 0)
        {
            return (double)current / total * 200;
        }
        return 0.0;
    }
}

public class ActivityLevelConverter : IMultiValueConverter
{
    public static readonly ActivityLevelConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 1 && values[0] is int level)
        {
            return level switch
            {
                0 => new SolidColorBrush(Color.Parse("#E2E8F0")),
                1 => new SolidColorBrush(Color.Parse("#BBF7D0")),
                2 => new SolidColorBrush(Color.Parse("#86EFAC")),
                3 => new SolidColorBrush(Color.Parse("#22C55E")),
                4 => new SolidColorBrush(Color.Parse("#16A34A")),
                _ => new SolidColorBrush(Color.Parse("#E2E8F0"))
            };
        }
        return new SolidColorBrush(Color.Parse("#E2E8F0"));
    }
}

public class CategoryConverter : IValueConverter
{
    public static readonly CategoryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selected && parameter is string category)
        {
            return selected == category ? "Primary" : "Secondary";
        }
        return "Secondary";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
