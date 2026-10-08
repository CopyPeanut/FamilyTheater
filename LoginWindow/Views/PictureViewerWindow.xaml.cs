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
        private const double MinZoom = 0.05;
        private const double MaxZoom = 8.0;
        private const double ZoomStep = 1.15;

        private readonly IAppLogger _logger;
        private Forms.PictureBox? _gifPictureBox;
        private double _imagePixelWidth;
        private double _imagePixelHeight;
        private double _zoom = 1.0;
        private bool _fitToWindow = true;
        private bool _isDragging;
        private System.Windows.Point _dragStartPoint;
        private double _dragStartHorizontalOffset;
        private double _dragStartVerticalOffset;

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

            _imagePixelWidth = image.PixelWidth;
            _imagePixelHeight = image.PixelHeight;
            ImageViewer.Source = image;
            ImageScrollViewer.Visibility = Visibility.Visible;
            GifHost.Visibility = Visibility.Collapsed;
            ResizeWindowForImage(_imagePixelWidth, _imagePixelHeight);
            Dispatcher.BeginInvoke(new Action(FitImageToWindow));
        }

        private void LoadGif(string imagePath)
        {
            var gifImage = System.Drawing.Image.FromFile(imagePath);
            ResizeWindowForImage(gifImage.Width, gifImage.Height);

            _gifPictureBox = new Forms.PictureBox
            {
                BackColor = System.Drawing.Color.FromArgb(30, 30, 29),
                Dock = Forms.DockStyle.Fill,
                SizeMode = Forms.PictureBoxSizeMode.Zoom,
                Image = gifImage
            };

            GifHost.Child = _gifPictureBox;
            GifHost.Visibility = Visibility.Visible;
            ImageScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_zoom / ZoomStep);
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_zoom * ZoomStep);
        }

        private void FitBtn_Click(object sender, RoutedEventArgs e)
        {
            FitImageToWindow();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_fitToWindow && ImageScrollViewer.Visibility == Visibility.Visible)
            {
                FitImageToWindow();
            }
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var oldZoom = _zoom;
            var position = e.GetPosition(ImageSurface);
            var nextZoom = e.Delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep;

            SetZoom(nextZoom);
            if (Math.Abs(oldZoom - _zoom) < 0.0001)
            {
                e.Handled = true;
                return;
            }

            var factor = _zoom / oldZoom;
            ImageScrollViewer.ScrollToHorizontalOffset((ImageScrollViewer.HorizontalOffset + position.X) * factor - position.X);
            ImageScrollViewer.ScrollToVerticalOffset((ImageScrollViewer.VerticalOffset + position.Y) * factor - position.Y);
            e.Handled = true;
        }

        private void ImageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(ImageScrollViewer);
            _dragStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _dragStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            ImageScrollViewer.Cursor = System.Windows.Input.Cursors.SizeAll;
            ImageScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void ImageScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                StopDragging();
                return;
            }

            var point = e.GetPosition(ImageScrollViewer);
            ImageScrollViewer.ScrollToHorizontalOffset(_dragStartHorizontalOffset - (point.X - _dragStartPoint.X));
            ImageScrollViewer.ScrollToVerticalOffset(_dragStartVerticalOffset - (point.Y - _dragStartPoint.Y));
            e.Handled = true;
        }

        private void ImageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopDragging();
            e.Handled = true;
        }

        private void ImageScrollViewer_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StopDragging();
        }

        private void ImageScrollViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FitImageToWindow();
            e.Handled = true;
        }

        private void StopDragging()
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ImageScrollViewer.Cursor = System.Windows.Input.Cursors.Arrow;
            if (ImageScrollViewer.IsMouseCaptured)
            {
                ImageScrollViewer.ReleaseMouseCapture();
            }
        }

        private void FitImageToWindow()
        {
            if (_imagePixelWidth <= 0 || _imagePixelHeight <= 0)
            {
                return;
            }

            var viewportWidth = ImageScrollViewer.ViewportWidth > 0
                ? ImageScrollViewer.ViewportWidth
                : Math.Max(1, ImageScrollViewer.ActualWidth);
            var viewportHeight = ImageScrollViewer.ViewportHeight > 0
                ? ImageScrollViewer.ViewportHeight
                : Math.Max(1, ImageScrollViewer.ActualHeight);
            var fitZoom = Math.Min(viewportWidth / _imagePixelWidth, viewportHeight / _imagePixelHeight);
            SetZoom(Math.Clamp(fitZoom, MinZoom, 1.0), fitToWindow: true);
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
        }

        private void SetZoom(double zoom, bool fitToWindow = false)
        {
            _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            _fitToWindow = fitToWindow;

            ImageViewer.Width = Math.Max(1, _imagePixelWidth * _zoom);
            ImageViewer.Height = Math.Max(1, _imagePixelHeight * _zoom);
            ImageSurface.MinWidth = Math.Max(ImageScrollViewer.ViewportWidth, ImageViewer.Width);
            ImageSurface.MinHeight = Math.Max(ImageScrollViewer.ViewportHeight, ImageViewer.Height);
            ZoomDisplay.Text = $"{_zoom:P0}";
        }

        private void ResizeWindowForImage(double imageWidth, double imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            var workArea = SystemParameters.WorkArea;
            var maxWidth = Math.Max(MinWidth, workArea.Width * 0.92);
            var maxHeight = Math.Max(MinHeight, workArea.Height * 0.92);
            var chromeHeight = 72;
            var width = Math.Clamp(imageWidth + 32, MinWidth, maxWidth);
            var height = Math.Clamp(imageHeight + chromeHeight, MinHeight, maxHeight);

            Width = width;
            Height = height;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                SetZoom(_zoom * ZoomStep);
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                SetZoom(_zoom / ZoomStep);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                FitImageToWindow();
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
