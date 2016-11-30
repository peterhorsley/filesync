using System;

namespace FileSync.Converters
{
    using System.Globalization;
    using System.Windows.Data;
    public class SyncActiveToIconOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var syncActive = (bool)value;
            return syncActive ? 0.4 : 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
