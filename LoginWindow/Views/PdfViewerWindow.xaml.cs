using FamilyTheater.Core.Logger;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Data.Pdf;
using Windows.Storage;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class PdfViewerWindow : Window
    {
        private const double MinZoom = 0.1;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.1;
        private const double RenderPixelScale = 1.5;

        private readonly string _pdfPath;
        private readonly IAppLogger _logger;
        private readonly DispatcherTimer _autoPageTimer;
        private readonly string _cacheDirectory;
        private PdfDocument? _document;
        private int _currentPageIndex;
        private int _renderVersion;
        private double _zoom = 1.0;
        private bool _isUpdatingSlider;
        private bool _useFitToPage = true;
        private bool _isCtrlPanning;
        private System.Windows.Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public PdfViewerWindow(string pdfPath, IAppLogger logger)
        {
            InitializeComponent();
            _pdfPath = pdfPath;
            _logger = logger;
            _cacheDirectory = Path.Combine(Path.GetTempPath(), "FamilyTheater", "PdfViewer", Guid.NewGuid().ToString("N"));

            _autoPageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoPageTimer.Tick += AutoPageTimer_Tick;

            PageSlider.DragCompleted += PageSlider_DragCompleted;
            Title = Path.GetFileName(pdfPath);
        }

        private async void PdfViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDocumentAsync();
        }

        private async Task LoadDocumentAsync()
        {
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                var pdfFile = await StorageFile.GetFileFromPathAsync(_pdfPath);
                _document = await PdfDocument.LoadFromFileAsync(pdfFile);

                if (_document.PageCount == 0)
                {
                    LoadingText.Text = "PDF 没有可显示的页面";
                    return;
                }

                PageSlider.Maximum = _document.PageCount;
                _currentPageIndex = 0;
                await ApplyFitToPageAsync();
                UpdatePageDisplay();
            }
            catch (Exception ex)
            {
                _logger.Error($"PDF load failed. PdfPath={_pdfPath}", ex);
                LoadingText.Text = "PDF 加载失败";
                CustomMessageBox.Show($"无法打开 PDF：\n{ex.Message}");
            }
        }

        private async Task RenderCurrentPageAsync()
        {
            if (_document == null)
            {
                return;
            }

            var renderVersion = ++_renderVersion;
            LoadingText.Visibility = Visibility.Visible;

            try
            {
                using var page = _document.GetPage((uint)_currentPageIndex);
                var width = Math.Max(1, page.Size.Width * _zoom);
                var height = Math.Max(1, page.Size.Height * _zoom);
                var renderWidth = (uint)Math.Max(1, Math.Round(width * RenderPixelScale));
                var targetPath = Path.Combine(_cacheDirectory, $"page_{_currentPageIndex + 1}_{renderWidth}.png");

                if (!File.Exists(targetPath))
                {
                    var targetFolder = await StorageFolder.GetFolderFromPathAsync(_cacheDirectory);
                    var targetFile = await targetFolder.CreateFileAsync(Path.GetFileName(targetPath), CreationCollisionOption.ReplaceExisting);
                    using var stream = await targetFile.OpenAsync(FileAccessMode.ReadWrite);
                    var options = new PdfPageRenderOptions
                    {
                        DestinationWidth = renderWidth
                    };
                    await page.RenderToStreamAsync(stream, options);
                }

                if (renderVersion != _renderVersion)
                {
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(targetPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                PageImage.Source = bitmap;
                PageImage.Width = width;
                PageImage.Height = height;
                LoadingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _logger.Error($"PDF page render failed. PdfPath={_pdfPath}, Page={_currentPageIndex + 1}", ex);
                LoadingText.Text = "页面渲染失败";
                LoadingText.Visibility = Visibility.Visible;
            }
        }

        private async Task GoToPageAsync(int pageIndex)
        {
            if (_document == null)
            {
                return;
            }

            var normalizedPageIndex = Math.Clamp(pageIndex, 0, (int)_document.PageCount - 1);
            if (normalizedPageIndex == _currentPageIndex && PageImage.Source != null)
            {
                return;
            }

            _currentPageIndex = normalizedPageIndex;
            if (_useFitToPage)
            {
                await ApplyFitToPageAsync();
            }
            else
            {
                await RenderCurrentPageAsync();
            }
            UpdatePageDisplay();
        }

        private async Task MovePageAsync(int offset)
        {
            await GoToPageAsync(_currentPageIndex + offset);
        }

        private async Task SetZoomAsync(double zoom)
        {
            var normalizedZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            if (Math.Abs(normalizedZoom - _zoom) < 0.001)
            {
                return;
            }

            _zoom = normalizedZoom;
            _useFitToPage = false;
            ZoomDisplay.Text = $"{_zoom:P0}";
            await RenderCurrentPageAsync();
        }

        private async Task ApplyFitToPageAsync()
        {
            if (_document == null)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            using var page = _document.GetPage((uint)_currentPageIndex);
            var availableWidth = PageScrollViewer.ViewportWidth > 0
                ? PageScrollViewer.ViewportWidth
                : Math.Max(1, PageScrollViewer.ActualWidth);
            var availableHeight = PageScrollViewer.ViewportHeight > 0
                ? PageScrollViewer.ViewportHeight
                : Math.Max(1, PageScrollViewer.ActualHeight);

            var widthZoom = Math.Max(1, availableWidth - 32) / Math.Max(1, page.Size.Width);
            var heightZoom = Math.Max(1, availableHeight - 32) / Math.Max(1, page.Size.Height);
            _zoom = Math.Clamp(Math.Min(widthZoom, heightZoom), MinZoom, MaxZoom);
            ZoomDisplay.Text = "适应";
            await RenderCurrentPageAsync();
        }

        private void UpdatePageDisplay()
        {
            if (_document == null)
            {
                PageDisplay.Text = "0 / 0";
                return;
            }

            _isUpdatingSlider = true;
            PageSlider.Value = _currentPageIndex + 1;
            _isUpdatingSlider = false;
            PageDisplay.Text = $"{_currentPageIndex + 1} / {_document.PageCount}";
            ZoomDisplay.Text = _useFitToPage ? "适应" : $"{_zoom:P0}";
        }

        private async void PdfViewerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_document == null || !_useFitToPage)
            {
                return;
            }

            await ApplyFitToPageAsync();
            UpdatePageDisplay();
        }

        private async void Root_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var direction = e.Delta > 0 ? 1 : -1;
                await SetZoomAsync(_zoom + direction * ZoomStep);
                e.Handled = true;
                return;
            }

            await MovePageAsync(e.Delta < 0 ? 1 : -1);
            e.Handled = true;
        }

        private void PageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            _isCtrlPanning = true;
            _panStartPoint = e.GetPosition(PageScrollViewer);
            _panStartHorizontalOffset = PageScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = PageScrollViewer.VerticalOffset;
            PageScrollViewer.Cursor = System.Windows.Input.Cursors.SizeAll;
            PageScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void PageScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isCtrlPanning)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed ||
                (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                StopCtrlPanning();
                return;
            }

            var point = e.GetPosition(PageScrollViewer);
            var deltaX = point.X - _panStartPoint.X;
            var deltaY = point.Y - _panStartPoint.Y;
            PageScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
            PageScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
            e.Handled = true;
        }

        private void PageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isCtrlPanning)
            {
                return;
            }

            StopCtrlPanning();
            e.Handled = true;
        }

        private void PageScrollViewer_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StopCtrlPanning();
        }

        private void StopCtrlPanning()
        {
            if (!_isCtrlPanning)
            {
                return;
            }

            _isCtrlPanning = false;
            PageScrollViewer.Cursor = System.Windows.Input.Cursors.Arrow;
            if (PageScrollViewer.IsMouseCaptured)
            {
                PageScrollViewer.ReleaseMouseCapture();
            }
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            await MovePageAsync(-1);
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            await MovePageAsync(1);
        }

        private void PageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_document == null || _isUpdatingSlider)
            {
                return;
            }

            var pageNumber = Math.Clamp((int)Math.Round(PageSlider.Value), 1, (int)_document.PageCount);
            PageDisplay.Text = $"{pageNumber} / {_document.PageCount}";
        }

        private async void PageSlider_DragCompleted(object? sender, EventArgs e)
        {
            if (_document == null)
            {
                return;
            }

            var pageNumber = Math.Clamp((int)Math.Round(PageSlider.Value), 1, (int)_document.PageCount);
            await GoToPageAsync(pageNumber - 1);
        }

        private void AutoPageCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ConfigureAutoPageTimer();
        }

        private void AutoPageSecondsInput_LostFocus(object sender, RoutedEventArgs e)
        {
            ConfigureAutoPageTimer();
        }

        private void AutoPageSecondsInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfigureAutoPageTimer();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void ConfigureAutoPageTimer()
        {
            if (!double.TryParse(AutoPageSecondsInput.Text, out var seconds) || seconds <= 0)
            {
                seconds = 5;
                AutoPageSecondsInput.Text = "5";
            }

            _autoPageTimer.Interval = TimeSpan.FromSeconds(seconds);
            if (AutoPageCheckBox.IsChecked == true)
            {
                _autoPageTimer.Start();
            }
            else
            {
                _autoPageTimer.Stop();
            }
        }

        private async void AutoPageTimer_Tick(object? sender, EventArgs e)
        {
            if (_document == null)
            {
                return;
            }

            if (_currentPageIndex >= (int)_document.PageCount - 1)
            {
                _autoPageTimer.Stop();
                AutoPageCheckBox.IsChecked = false;
                return;
            }

            await MovePageAsync(1);
        }

        private void HideControlsButton_Click(object sender, RoutedEventArgs e)
        {
            ControlBar.Visibility = Visibility.Collapsed;
            ShowControlsButton.Visibility = Visibility.Visible;
            if (_useFitToPage)
            {
                _ = ApplyFitToPageAsync();
            }
        }

        private void ShowControlsButton_Click(object sender, RoutedEventArgs e)
        {
            ControlBar.Visibility = Visibility.Visible;
            ShowControlsButton.Visibility = Visibility.Collapsed;
            if (_useFitToPage)
            {
                _ = ApplyFitToPageAsync();
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
            else if (e.Key == Key.Left || e.Key == Key.PageUp)
            {
                _ = MovePageAsync(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.PageDown || e.Key == Key.Space)
            {
                _ = MovePageAsync(1);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoPageTimer.Stop();
            PageImage.Source = null;

            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"PDF cache cleanup failed. CacheDirectory={_cacheDirectory}", ex);
            }

            base.OnClosed(e);
        }
    }
}
