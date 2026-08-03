using FamilyTheater.Core.Enum;
using System.Windows;
using System.Windows.Media;

namespace LoginWindow.Views
{
    public partial class CustomMessageBox : System.Windows.Window
    {
        public static void Show(string message, LogLevel level = LogLevel.INFO, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dlg = new CustomMessageBox(message, level, button, icon);
            dlg.ShowDialog();
        }

        //public static Task<MessageBoxResult> ShowAsync(
        //    string message,
        //    LogLevel level = LogLevel.INFO,
        //    MessageBoxButton button = MessageBoxButton.OK,
        //    MessageBoxImage icon = MessageBoxImage.Information)
        //{
        //    var dlg = new CustomMessageBox(message, level, button, icon);
        //    var tcs = new TaskCompletionSource<MessageBoxResult>();

        //    dlg.Closed += (_, _) => tcs.SetResult(dlg.Result);
        //    dlg.Show(); 
        //    return tcs.Task;
        //}

        // ---------- 内部实现 ----------

        private MessageBoxResult Result { get; set; } = MessageBoxResult.Cancel;

        private CustomMessageBox(string message, LogLevel level,
                                  MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();
            this.Background = level switch
            {
                LogLevel.WARN => Brushes.White,
                LogLevel.ERROR or LogLevel.FATAL => Brushes.Red,
                _ => Brushes.DodgerBlue
            };
            DataContext = new { Message = message };

            // 根据按钮类型控制可见性
            BtnCancel.Visibility = button switch
            {
                MessageBoxButton.OK => Visibility.Collapsed,
                MessageBoxButton.OKCancel => Visibility.Visible,
                MessageBoxButton.YesNo => Visibility.Visible,
                MessageBoxButton.YesNoCancel => Visibility.Visible,
                _ => Visibility.Collapsed
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

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            Result = BtnConfirm.Content.ToString()!.StartsWith("是")
                ? MessageBoxResult.Yes
                : MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Result = BtnCancel.Content.ToString()!.StartsWith("否")
                ? MessageBoxResult.No
                : MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}