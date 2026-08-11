using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LoginWindow.Views
{
    /// <summary>
    /// 把文件路径字符串转换为 BitmapImage。
    /// WPF 内置的 ImageSourceConverter 对某些路径（含中文、空格、反斜杠）会静默失败，
    /// 这里显式创建 BitmapImage 并设置 BitmapCacheOption.OnLoad 避免文件锁。
    /// </summary>
    public class StringToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
