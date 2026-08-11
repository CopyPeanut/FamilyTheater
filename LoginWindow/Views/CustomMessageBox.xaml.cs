using FamilyTheater.Core.Enum;
using System.Windows;
using System.Windows.Media;

namespace LoginWindow.Views
{
    public partial class CustomMessageBox : Window
    {
        // ✅ 纯展示，无返回值，fire-and-forget
        public static void Show(
            string message,
            LogLevel level = LogLevel.INFO,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dlg = new CustomMessageBox(message, level, button, icon);
            dlg.Show();
        }
        /// <summary>
        /// 模态弹窗，返回用户是否点了确认。
        /// </summary>
        public static bool ShowDialog(
            string message,
            LogLevel level = LogLevel.WARN,
            MessageBoxButton button = MessageBoxButton.OKCancel,
            MessageBoxImage icon = MessageBoxImage.Warning)
        {
            var dlg = new CustomMessageBox(message, level, button, icon);
            dlg._result = false;
            dlg.ShowDialog();
            return dlg._result;
        }

        private bool _result;

        private CustomMessageBox(string message, LogLevel level,
                                  MessageBoxButton button, MessageBoxImage icon)
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

            BtnCancel.Visibility = button switch
            {
                MessageBoxButton.OK => Visibility.Collapsed,
                _ => Visibility.Visible
            };

            BtnConfirm.Content = button switch
            {
                MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel => "是(Y)",
                _ => "确 定"
            };

            BtnCancel.Content = button switch
            {
                MessageBoxButton.YesNoCancel => "取消(C)",
                MessageBoxButton.YesNo => "否(N)",
                _ => "取 消"
            };
        }

        // ✅ 两个按钮都只做 Hide，不记录任何结果
        private void OnConfirm(object sender, RoutedEventArgs e) { _result = true; Close(); }
        private void OnCancel(object sender, RoutedEventArgs e) { _result = false; Close(); }
    }
}