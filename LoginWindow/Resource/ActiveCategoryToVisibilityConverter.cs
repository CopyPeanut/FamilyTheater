using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LoginWindow.Views
{
    /// <summary>
    /// 将 ActiveCategory 字符串与 ConverterParameter 比较，
    /// 匹配返回 Visible，不匹配返回 Collapsed。
    /// 用于在 movie/picture 视图间切换。
    /// </summary>
    public class ActiveCategoryToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string category && parameter is string target)
            {
                return string.Equals(category, target, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
