using System;
using System.Windows;
using System.Windows.Controls;

namespace LoginWindow.Views
{
    public class WholeLineScrollViewer : ScrollViewer
    {
        public static readonly DependencyProperty LineHeightProperty =
            DependencyProperty.Register(
                nameof(LineHeight),
                typeof(double),
                typeof(WholeLineScrollViewer),
                new PropertyMetadata(34.0));

        public double LineHeight
        {
            get => (double)GetValue(LineHeightProperty);
            set => SetValue(LineHeightProperty, value);
        }

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeSize)
        {
            var viewportSize = arrangeSize;
            if (LineHeight > 0 &&
                !double.IsInfinity(arrangeSize.Height) &&
                !double.IsNaN(arrangeSize.Height))
            {
                var visibleRows = Math.Floor(arrangeSize.Height / LineHeight);
                if (visibleRows >= 1)
                {
                    viewportSize.Height = visibleRows * LineHeight;
                }
            }

            base.ArrangeOverride(viewportSize);
            return arrangeSize;
        }
    }
}
