using FamilyTheater.Core.Logger;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class PictureViewerWindow : Window
    {
        private readonly IAppLogger _logger;
        private Forms.PictureBox? _gifPictureBox;

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
                if (Path.GetExtension(imagePath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    LoadGif(imagePath);
                }
                else
                {
                    LoadStaticImage(imagePath);
                }

                FileNameDisplay.Text = Path.GetFileName(imagePath);
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载图片失败：{imagePath}", ex);
                CustomMessageBox.Show($"无法加载图片：{imagePath}");
            }
        }

        private void LoadStaticImage(string imagePath)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imagePath, UriKind.Absolute);
            image.EndInit();

            ImageViewer.Source = image;
            ImageScrollViewer.Visibility = Visibility.Visible;
            GifHost.Visibility = Visibility.Collapsed;
        }

        private void LoadGif(string imagePath)
        {
            _gifPictureBox = new Forms.PictureBox
            {
                BackColor = System.Drawing.Color.FromArgb(30, 30, 29),
                Dock = Forms.DockStyle.Fill,
                SizeMode = Forms.PictureBoxSizeMode.Zoom,
                Image = System.Drawing.Image.FromFile(imagePath)
            };

            GifHost.Child = _gifPictureBox;
            GifHost.Visibility = Visibility.Visible;
            ImageScrollViewer.Visibility = Visibility.Collapsed;
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

        protected override void OnClosed(EventArgs e)
        {
            _gifPictureBox?.Image?.Dispose();
            _gifPictureBox?.Dispose();
            base.OnClosed(e);
        }
    }
}
