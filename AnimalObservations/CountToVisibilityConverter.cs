using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AnimalObservations
{
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            var count  = (int)value;
            return count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}