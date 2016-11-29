using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Converters
{
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Media;

    class BoolToErrorColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush _errorBrush = new SolidColorBrush(Colors.Red );
        private static readonly SolidColorBrush _normalBrush = new SolidColorBrush(Colors.Transparent);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isValid = (bool)value;
            return isValid ? _normalBrush : _errorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}

