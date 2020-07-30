using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ImageSim.Converters
{
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public bool IsInverted { get; set; } = false;
        public Visibility HiddenState { get; set; } = Visibility.Collapsed;
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bValue = false;
            if (value is bool val)
            {
                bValue = val;
            }
            else if (value is bool?)
            {
                bValue = (bool?)value ?? false;
            }
            return (bValue ^ IsInverted) ? Visibility.Visible : HiddenState;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                var bval = visibility == Visibility.Visible;
                return bval ^ IsInverted;
            }
            else
            {
                return false ^ IsInverted;
            }
        }
    }
}
