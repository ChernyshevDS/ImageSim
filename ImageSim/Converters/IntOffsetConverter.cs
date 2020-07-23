using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ImageSim.Converters
{
    public class IntOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var binded = System.Convert.ToInt32(value);
                var offset = 0;
                if(parameter != null)
                    offset = System.Convert.ToInt32(parameter);
                return binded + offset;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var binded = System.Convert.ToInt32(value);
                var offset = 0;
                if (parameter != null)
                    offset = System.Convert.ToInt32(parameter);
                return binded - offset;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
