using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LoginWindow.Views
{
    /// <summary>
    /// 纯代码生成的轻量级星空背景控件
    /// </summary>
    public class StarfieldBackground : Canvas
    {
        private readonly Random _random = new();
        private readonly List<Storyboard> _activeStoryboards = new();

        // ★ 可配置参数
        public int StarCount { get; set; } = 60;
        public double MinSize { get; set; } = 0.5;
        public double MaxSize { get; set; } = 3.0;
        public bool EnableDrift { get; set; } = true;
        public bool RespectSystemAnimationSetting { get; set; } = true;

        public StarfieldBackground()
        {
            IsHitTestVisible = false; // 关键：不拦截鼠标事件
            ClipToBounds = true;
            Background = System.Windows.Media.Brushes.Transparent; // 透明底，由父容器提供黑色背景
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 尊重系统"减少动画"偏好 & 远程桌面降级
            if (RespectSystemAnimationSetting &&
                (!SystemParameters.ClientAreaAnimation || SystemParameters.IsRemoteSession))
            {
                GenerateStaticStars();
                return;
            }

            GenerateAnimatedStars();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 防止内存泄漏：卸载时停止所有动画
            foreach (var sb in _activeStoryboards)
            {
                sb.Stop();
            }
            _activeStoryboards.Clear();
            Children.Clear();
        }

        /// <summary>
        /// 生成带动画的星星
        /// </summary>
        private void GenerateAnimatedStars()
        {
            for (int i = 0; i < StarCount; i++)
            {
                var star = CreateStarEllipse();
                PositionStar(star);

                // 闪烁动画（每颗星独立实例，错开起始时间）
                var twinkle = new DoubleAnimation
                {
                    From = _random.NextDouble() * 0.3 + 0.1,
                    To = _random.NextDouble() * 0.5 + 0.5,
                    Duration = TimeSpan.FromSeconds(_random.NextDouble() * 3 + 2),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(_random.NextDouble() * 5)
                };

                var twinkleSb = new Storyboard();
                twinkleSb.Children.Add(twinkle);
                Storyboard.SetTarget(twinkle, star);
                Storyboard.SetTargetProperty(twinkle, new PropertyPath(OpacityProperty));
                twinkleSb.Begin();
                _activeStoryboards.Add(twinkleSb);

                // 漂移动画（约40%的星拥有）
                if (EnableDrift && _random.NextDouble() > 0.6)
                {
                    var drift = new DoubleAnimation
                    {
                        By = -(_random.NextDouble() * 30 + 10),
                        Duration = TimeSpan.FromSeconds(_random.NextDouble() * 40 + 30),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(_random.NextDouble() * 10)
                    };

                    var driftSb = new Storyboard();
                    driftSb.Children.Add(drift);
                    Storyboard.SetTarget(drift, star);
                    Storyboard.SetTargetProperty(drift, new PropertyPath(Canvas.TopProperty));
                    driftSb.Begin();
                    _activeStoryboards.Add(driftSb);
                }

                Children.Add(star);
            }
        }

        /// <summary>
        /// 降级模式：仅生成静态星点，无动画
        /// </summary>
        private void GenerateStaticStars()
        {
            for (int i = 0; i < StarCount; i++)
            {
                var star = CreateStarEllipse();
                PositionStar(star);
                Children.Add(star);
            }
        }

        private Ellipse CreateStarEllipse()
        {
            double size = _random.NextDouble() * (MaxSize - MinSize) + MinSize;

            // 偶尔生成暖色/冷色星星，增加层次感
            System.Windows.Media.Brush fill = System.Windows.Media.Brushes.White;
            double colorRoll = _random.NextDouble();
            if (colorRoll > 0.85) fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 238, 221)); // 暖黄
            else if (colorRoll > 0.70) fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 238, 255)); // 冷蓝

            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Opacity = _random.NextDouble() * 0.6 + 0.2
            };
        }

        private void PositionStar(Ellipse star)
        {
            // 使用 ActualWidth/ActualHeight，Loaded 时已有正确尺寸
            double w = ActualWidth > 0 ? ActualWidth : 800;
            double h = ActualHeight > 0 ? ActualHeight : 450;

            Canvas.SetLeft(star, _random.NextDouble() * w);
            Canvas.SetTop(star, _random.NextDouble() * h);
        }
    }
}