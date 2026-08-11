using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LoginWindow.Views
{
    public partial class PictureViewerWindow : Window
    {
        public PictureViewerWindow(string imagePath)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    ImageViewer.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                    FileNameDisplay.Text = Path.GetFileName(imagePath);
                }
                catch
                {
                    CustomMessageBox.Show($"无法加载图片：{imagePath}");
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
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
