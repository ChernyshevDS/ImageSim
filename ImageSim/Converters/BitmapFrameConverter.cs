using System;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ImageSim.Converters
{
    public sealed class BitmapFrameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    //create new stream and create bitmap frame
                    var uriSource = new Uri(path, UriKind.Absolute);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    //load the image now so we can immediately dispose of the stream
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmapImage.UriSource = uriSource;
                    bitmapImage.EndInit();
                    return bitmapImage;
                }
                catch (Exception)
                {
                    return DependencyProperty.UnsetValue;
                }
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
