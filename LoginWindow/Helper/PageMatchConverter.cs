using System;
using System.Globalization;
using System.Windows.Data;

namespace LoginWindow.Views
{
    public class PageMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = 按钮页码(int?，可能为 null), values[1] = 当前页(int)
            if (values.Length == 2 && values[1] is int current)
            {
                if (values[0] is int page)
                    return page == current;
                // null 页码不匹配任何当前页
                return false;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}