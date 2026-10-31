namespace DOOM.Presentation.Controls;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

/// <summary>
/// Reusable IValueConverter implementations for DOOM presentation layer.
/// </summary>
public static class VisibilityConverters
{
    public static readonly IValueConverter BoolToVisibility = new BooleanToVisibilityConverter();
    public static readonly IValueConverter InverseBoolToVisibility = new InverseBooleanToVisibilityConverter();
    public static readonly IValueConverter NullToCollapsed = new NullToVisibilityConverter();
    public static readonly IValueConverter EmptyToCollapsed = new StringEmptyToVisibilityConverter();
    public static readonly IValueConverter CopiedTextConverter = new BooleanToCopiedTextConverter();

    private sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class StringEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class BooleanToCopiedTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? "Copied!" : "Copy";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

