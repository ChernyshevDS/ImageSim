using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ImageSim.Converters
{
    public class ArithmeticComparisonConverter : MarkupExtension, IMultiValueConverter
    {
        /// <summary>
        /// Преобразование из пары объектов в bool
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var v1 = values[0] as IComparable;
            var v2 = values[1] as IComparable;
            if (v1 == null || v2 == null)
                return false;
            if (v1.GetType() != v2.GetType())
                return false;
            return ((string)parameter) switch
            {
                "G" => v1.CompareTo(v2) > 0,
                "GE" => v1.CompareTo(v2) >= 0,
                "L" => v1.CompareTo(v2) < 0,
                "LE" => v1.CompareTo(v2) <= 0,
                "E" => v1.CompareTo(v2) == 0,
                "NE" => v1.CompareTo(v2) != 0,
                _ => throw new ArgumentNullException("parameter"),
            };
        }

        /// <summary>
        /// Не имеет смысла
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
