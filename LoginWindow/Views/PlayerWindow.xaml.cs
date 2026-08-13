using FamilyTheater.Core.Logger;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class PlayerWindow : Window
    {
        private readonly IAppLogger _logger;
        private readonly string _videoPath;
        private readonly DispatcherTimer _progressTimer;
        private bool _isPlaying;
        private bool _wasPlayingBeforeDrag;

        public PlayerWindow(string videoPath, IAppLogger logger)
        {
            InitializeComponent();
            _videoPath = videoPath;
            _logger = logger;

            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            ProgressSlider.DragStarted += ProgressSlider_DragStarted;
            ProgressSlider.DragCompleted += ProgressSlider_DragCompleted;

            Loaded += PlayerWindow_Loaded;
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_videoPath))
            {
                return;
            }

            Player.Source = new Uri(_videoPath, UriKind.Absolute);
            Player.Play();
        }

        private void Player_MediaOpened(object sender, RoutedEventArgs e)
        {
            _isPlaying = true;
            PlayPauseIcon.Text = "⏸";
            _progressTimer.Start();

            if (Player.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
            }
        }

        private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var errorMessage = e.ErrorException?.Message ?? "未知错误";
            _logger.Error($"视频播放失败：{_videoPath}", e.ErrorException);

            CustomMessageBox.Show(
                $"无法播放该视频：{errorMessage}\n\n路径：{_videoPath}\n\n可能原因：\n1. 视频编码不受 Windows Media Player 支持（如 H.265/HEVC）\n2. 文件损坏或路径包含特殊字符\n3. 系统缺少对应解码器");
            Close();
        }

        private void Player_MediaEnded(object sender, RoutedEventArgs e)
        {
            Player.Stop();
            _isPlaying = false;
            PlayPauseIcon.Text = "▶";
            _progressTimer.Stop();
            ProgressSlider.Value = 0;
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                Player.Pause();
                _isPlaying = false;
                PlayPauseIcon.Text = "▶";
            }
            else
            {
                Player.Play();
                _isPlaying = true;
                PlayPauseIcon.Text = "⏸";
                _progressTimer.Start();
            }
        }

        private void ProgressSlider_DragStarted(object? sender, EventArgs e)
        {
            _wasPlayingBeforeDrag = _isPlaying;
            if (_isPlaying)
            {
                Player.Pause();
                _isPlaying = false;
                PlayPauseIcon.Text = "▶";
            }

            _progressTimer.Stop();
        }

        private void ProgressSlider_DragCompleted(object? sender, EventArgs e)
        {
            Player.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            if (_wasPlayingBeforeDrag)
            {
                Player.Play();
                _isPlaying = true;
                PlayPauseIcon.Text = "⏸";
                _progressTimer.Start();
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTimeDisplay();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (!ProgressSlider.IsDragging && Player.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = Player.Position.TotalSeconds;
                UpdateTimeDisplay();
            }
        }

        private void UpdateTimeDisplay()
        {
            var current = TimeSpan.FromSeconds(ProgressSlider.Value);
            var total = Player.NaturalDuration.HasTimeSpan
                ? Player.NaturalDuration.TimeSpan
                : TimeSpan.Zero;

            TimeDisplay.Text = $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _progressTimer.Stop();
            Player.Stop();
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                PlayPauseBtn_Click(this, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseBtn_Click(this, e);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
