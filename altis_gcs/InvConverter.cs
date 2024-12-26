using System;
using System.Globalization;
using System.Windows.Data;

namespace altis_gcs
{
    public class InvConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return -d;  // 양수를 음수로
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return -d;
            return 0.0;
        }
    }
}
