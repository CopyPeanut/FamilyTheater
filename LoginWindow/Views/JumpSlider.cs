using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginWindow.Views
{
    /// <summary>
    /// 点击 track 任意位置直接跳转到该值（而非默认只移动一步）。
    /// 同时支持拖拽：按下即跳转，拖动时持续更新，松手后触发。
    /// </summary>
    public class JumpSlider : Slider
    {
        private bool _isDragging;

        static JumpSlider()
        {
            // 允许 PreviewMouseLeftButtonDown 事件覆盖默认行为
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JumpSlider),
                new FrameworkPropertyMetadata(typeof(Slider)));
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // 计算点击位置对应的值
            var point = e.GetPosition(this);
            var ratio = ActualWidth > 0 ? point.X / ActualWidth : 0;
            ratio = ratio < 0 ? 0 : (ratio > 1 ? 1 : ratio);

            var targetValue = Minimum + ratio * (Maximum - Minimum);
            Value = targetValue;

            // 捕获鼠标，后续移动持续更新
            _isDragging = true;
            CaptureMouse();

            // 暂停定时器更新（通过标记，外部 ProgressTimer_Tick 检查 IsDragging）
            IsDragging = true;

            // 通知外部：开始拖拽，暂停视频
            DragStarted?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var point = e.GetPosition(this);
                var ratio = ActualWidth > 0 ? point.X / ActualWidth : 0;
                ratio = ratio < 0 ? 0 : (ratio > 1 ? 1 : ratio);

                var targetValue = Minimum + ratio * (Maximum - Minimum);
                Value = targetValue;
            }
            base.OnPreviewMouseMove(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                IsDragging = false;
                // 触发跳转完成事件，外部更新播放位置
                DragCompleted?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
            base.OnPreviewMouseLeftButtonUp(e);
        }

        /// <summary>外部用于判断是否正在拖拽（暂停定时器更新）</summary>
        public bool IsDragging { get; private set; }

        /// <summary>拖拽完成时触发，外部在此更新播放位置</summary>
        public event EventHandler? DragCompleted;

        /// <summary>开始拖拽时触发，外部在此暂停视频</summary>
        public event EventHandler? DragStarted;
    }
}
