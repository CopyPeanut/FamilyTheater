using FamilyTheater.Core.Enum;
using System.Windows;

namespace LoginWindow.Views
{
    public partial class CustomMessageBox : Window
    {
        public static void Show(
            string message,
            LogLevel level = LogLevel.INFO,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dialog = new CustomMessageBox(message, level, button, icon);
            dialog.Show();
        }

        public static bool ShowDialog(
            string message,
            LogLevel level = LogLevel.WARN,
            MessageBoxButton button = MessageBoxButton.OKCancel,
            MessageBoxImage icon = MessageBoxImage.Warning)
        {
            var dialog = new CustomMessageBox(message, level, button, icon)
            {
                _result = false
            };
            dialog.ShowDialog();
            return dialog._result;
        }

        private bool _result;

        private CustomMessageBox(
            string message,
            LogLevel level,
            MessageBoxButton button,
            MessageBoxImage icon)
        {
            InitializeComponent();

            Title = level switch
            {
                LogLevel.WARN => "Warning",
                LogLevel.ERROR or LogLevel.FATAL => "Error",
                _ => "Information"
            };

            WindowMessage.Foreground = level switch
            {
                LogLevel.WARN => System.Windows.Media.Brushes.Yellow,
                LogLevel.ERROR or LogLevel.FATAL => System.Windows.Media.Brushes.Red,
                _ => System.Windows.Media.Brushes.White
            };

            DataContext = new { Message = message };

            BtnCancel.Visibility = button == MessageBoxButton.OK
                ? Visibility.Collapsed
                : Visibility.Visible;

            BtnConfirm.Content = button switch
            {
                MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel => "是(Y)",
                _ => "确定"
            };

            BtnCancel.Content = button switch
            {
                MessageBoxButton.YesNoCancel => "取消(C)",
                MessageBoxButton.YesNo => "否(N)",
                _ => "取消"
            };
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            _result = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            _result = false;
            Close();
        }
    }
}
