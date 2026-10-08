using FamilyTheater.Core.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        private bool _showPreciseTime;
        private List<SubtitleCue> _subtitleCues = new();
        private int _activeSubtitleIndex = -1;

        public PlayerWindow(string videoPath, IAppLogger logger, bool showPreciseTimeByDefault = false)
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
            SetPreciseTimeMode(showPreciseTimeByDefault);

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
            ClearSubtitleDisplay();
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void VideoSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TogglePlayPause();
            e.Handled = true;
        }

        private void LoadSubtitleBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 SRT 字幕文件",
                Filter = "SRT 字幕 (*.srt)|*.srt|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                var cues = LoadSrt(dialog.FileName);
                if (cues.Count == 0)
                {
                    CustomMessageBox.Show("没有从该 SRT 文件中读取到可用字幕。");
                    return;
                }

                _subtitleCues = cues;
                _activeSubtitleIndex = -1;
                LoadSubtitleBtn.Content = "字幕✓";
                UpdateSubtitleDisplay(Player.Position);
            }
            catch (Exception ex)
            {
                _logger.Error($"加载字幕失败：{dialog.FileName}", ex);
                CustomMessageBox.Show($"加载字幕失败：\n{ex.Message}");
            }
        }

        private void TogglePlayPause()
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
            UpdateSubtitleDisplay(Player.Position);
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
            UpdateSubtitleDisplay(TimeSpan.FromSeconds(e.NewValue));
        }

        private void SetPreciseTimeMode(bool enabled)
        {
            _showPreciseTime = enabled;
            _progressTimer.Interval = TimeSpan.FromMilliseconds(enabled ? 100 : 500);
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (!ProgressSlider.IsDragging && Player.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = Player.Position.TotalSeconds;
                UpdateTimeDisplay();
                UpdateSubtitleDisplay(Player.Position);
            }
        }

        private void UpdateTimeDisplay()
        {
            var current = TimeSpan.FromSeconds(ProgressSlider.Value);
            var total = Player.NaturalDuration.HasTimeSpan
                ? Player.NaturalDuration.TimeSpan
                : TimeSpan.Zero;

            TimeDisplay.Text = _showPreciseTime
                ? $"{FormatPreciseTime(current)} / {FormatPreciseTime(total)}"
                : $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
        }

        private static string FormatPreciseTime(TimeSpan time)
        {
            var hours = (int)time.TotalHours;
            return $"{hours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
        }

        private static List<SubtitleCue> LoadSrt(string path)
        {
            var text = ReadSubtitleFileText(path);
            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            var cues = new List<SubtitleCue>();
            var index = 0;
            while (index < lines.Length)
            {
                while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
                {
                    index++;
                }

                if (index >= lines.Length)
                {
                    break;
                }

                if (int.TryParse(lines[index].Trim().TrimStart('\uFEFF'), out _))
                {
                    index++;
                }

                if (index >= lines.Length || !lines[index].Contains("-->", StringComparison.Ordinal))
                {
                    index++;
                    continue;
                }

                if (!TryParseTimeRange(lines[index], out var start, out var end))
                {
                    index++;
                    continue;
                }

                index++;
                var textLines = new List<string>();
                while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
                {
                    textLines.Add(lines[index].TrimEnd());
                    index++;
                }

                var subtitleText = string.Join(Environment.NewLine, textLines).Trim();
                if (!string.IsNullOrWhiteSpace(subtitleText) && end > start)
                {
                    cues.Add(new SubtitleCue(start, end, subtitleText));
                }
            }

            return cues.OrderBy(cue => cue.Start).ToList();
        }

        private static string ReadSubtitleFileText(string path)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding("GB18030").GetString(bytes);
            }
        }

        private static bool TryParseTimeRange(string line, out TimeSpan start, out TimeSpan end)
        {
            start = TimeSpan.Zero;
            end = TimeSpan.Zero;

            var parts = line.Split(new[] { "-->" }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return false;
            }

            return TryParseSrtTimestamp(parts[0], out start) &&
                   TryParseSrtTimestamp(parts[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty, out end);
        }

        private static bool TryParseSrtTimestamp(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            var parts = value.Trim().Replace('.', ',').Split(',', 2);
            if (parts.Length == 0 ||
                !TimeSpan.TryParse(parts[0], CultureInfo.InvariantCulture, out var baseTime))
            {
                return false;
            }

            var milliseconds = 0;
            if (parts.Length > 1)
            {
                var millisText = new string(parts[1].TakeWhile(char.IsDigit).ToArray());
                if (millisText.Length > 0)
                {
                    milliseconds = int.Parse(millisText.PadRight(3, '0')[..3], CultureInfo.InvariantCulture);
                }
            }

            time = baseTime + TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        private void UpdateSubtitleDisplay(TimeSpan position)
        {
            if (_subtitleCues.Count == 0)
            {
                ClearSubtitleDisplay();
                return;
            }

            if (_activeSubtitleIndex >= 0 &&
                _activeSubtitleIndex < _subtitleCues.Count &&
                _subtitleCues[_activeSubtitleIndex].Contains(position))
            {
                return;
            }

            var nextIndex = _subtitleCues.FindIndex(cue => cue.Contains(position));
            if (nextIndex < 0)
            {
                ClearSubtitleDisplay();
                return;
            }

            _activeSubtitleIndex = nextIndex;
            SubtitleText.Text = _subtitleCues[nextIndex].Text;
            SubtitleContainer.Visibility = Visibility.Visible;
        }

        private void ClearSubtitleDisplay()
        {
            _activeSubtitleIndex = -1;
            SubtitleText.Text = string.Empty;
            SubtitleContainer.Visibility = Visibility.Collapsed;
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

        private sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text)
        {
            public bool Contains(TimeSpan position) => position >= Start && position <= End;
        }
    }
}
