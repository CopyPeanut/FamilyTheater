using LoginWindow.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LoginWindow.Views
{
    public class BatchContentSelectionToBrushConverter : IMultiValueConverter
    {
        private static readonly System.Windows.Media.Brush SelectedBrush =
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 90, 95));
        private static readonly System.Windows.Media.Brush DefaultBrush = System.Windows.Media.Brushes.Transparent;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 ||
                parameter is not string category ||
                values[0] is not int id ||
                values[1] is not HomeWindowModel viewModel ||
                values[3] is not bool isBatchDeleteMode ||
                !isBatchDeleteMode)
            {
                return DefaultBrush;
            }

            return viewModel.IsBatchContentSelected(category, id)
                ? SelectedBrush
                : DefaultBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
