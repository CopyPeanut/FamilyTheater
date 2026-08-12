using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LoginWindow.Views
{
    public partial class PlayerWindow : Window
    {
        private readonly string _videoPath;
        private bool _isPlaying;
        private bool _wasPlayingBeforeDrag;  // 按下进度条时视频是否在播放，松手后据此恢复
        private readonly DispatcherTimer _progressTimer;

        public PlayerWindow(string videoPath)
        {
            InitializeComponent();
            _videoPath = videoPath;

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += ProgressTimer_Tick;

            // JumpSlider 拖拽开始时暂停视频，完成时跳转并恢复
            ProgressSlider.DragStarted += ProgressSlider_DragStarted;
            ProgressSlider.DragCompleted += ProgressSlider_DragCompleted;

            this.Loaded += PlayerWindow_Loaded;
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_videoPath))
            {
                Player.Source = new Uri(_videoPath, UriKind.Absolute);
                Player.Play();
            }
        }

        private void Player_MediaOpened(object sender, RoutedEventArgs e)
        {
            _isPlaying = true;
            PlayPauseIcon.Text = "⏸";
            _progressTimer.Start();

            if (Player.NaturalDuration.HasTimeSpan)
                ProgressSlider.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var errorMsg = e.ErrorException?.Message ?? "未知错误";
            CustomMessageBox.Show($"无法播放该视频：{errorMsg}\n\n路径：{_videoPath}\n\n可能原因：\n1. 视频编码不被 Windows Media Player 支持（如 H.265/HEVC）\n2. 文件损坏或路径含特殊字符\n3. 系统缺少对应解码器");
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

        /// <summary>
        /// 按下进度条 → 暂停视频，记录之前是否在播放。
        /// </summary>
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

        /// <summary>
        /// 松手 → 跳转播放位置，如果之前在播放则恢复。
        /// </summary>
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

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            // 拖拽期间不更新 Slider，避免互相拉扯抽搐
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

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
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
