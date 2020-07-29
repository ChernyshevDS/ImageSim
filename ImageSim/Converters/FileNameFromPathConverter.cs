using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ImageSim.Converters
{
    public class FileNameFromPathConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = System.Convert.ToString(value);
            return System.IO.Path.GetFileName(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
