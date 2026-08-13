using FamilyTheater.Core.Logger;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class PictureViewerWindow : Window
    {
        private readonly IAppLogger _logger;

        public PictureViewerWindow(string imagePath, IAppLogger logger)
        {
            InitializeComponent();
            _logger = logger;

            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            try
            {
                ImageViewer.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                FileNameDisplay.Text = Path.GetFileName(imagePath);
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载图片失败：{imagePath}", ex);
                CustomMessageBox.Show($"无法加载图片：{imagePath}");
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
