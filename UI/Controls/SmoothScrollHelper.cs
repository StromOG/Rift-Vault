using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RiftVault.UI.Controls
{
    /// <summary>
    /// High-performance momentum smooth scrolling engine for WPF ScrollViewers and ListViews.
    /// Intercepts mouse wheel events and smoothly animates vertical/horizontal offsets with cubic/quartic easing,
    /// eliminating rigid row-by-row snapping and stuttering.
    /// </summary>
    public static class SmoothScrollHelper
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static readonly DependencyProperty CurrentVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "CurrentVerticalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0.0, OnCurrentVerticalOffsetChanged));

        public static double GetCurrentVerticalOffset(DependencyObject obj) => (double)obj.GetValue(CurrentVerticalOffsetProperty);
        public static void SetCurrentVerticalOffset(DependencyObject obj, double value) => obj.SetValue(CurrentVerticalOffsetProperty, value);

        public static readonly DependencyProperty TargetVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetVerticalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(double.NaN));

        public static double GetTargetVerticalOffset(DependencyObject obj) => (double)obj.GetValue(TargetVerticalOffsetProperty);
        public static void SetTargetVerticalOffset(DependencyObject obj, double value) => obj.SetValue(TargetVerticalOffsetProperty, value);

        public static readonly DependencyProperty CurrentHorizontalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "CurrentHorizontalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0.0, OnCurrentHorizontalOffsetChanged));

        public static double GetCurrentHorizontalOffset(DependencyObject obj) => (double)obj.GetValue(CurrentHorizontalOffsetProperty);
        public static void SetCurrentHorizontalOffset(DependencyObject obj, double value) => obj.SetValue(CurrentHorizontalOffsetProperty, value);

        public static readonly DependencyProperty TargetHorizontalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetHorizontalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(double.NaN));

        public static double GetTargetHorizontalOffset(DependencyObject obj) => (double)obj.GetValue(TargetHorizontalOffsetProperty);
        public static void SetTargetHorizontalOffset(DependencyObject obj, double value) => obj.SetValue(TargetHorizontalOffsetProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseWheel += Element_PreviewMouseWheel;
                }
                else
                {
                    element.PreviewMouseWheel -= Element_PreviewMouseWheel;
                }
            }
        }

        private static void OnCurrentVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        private static void OnCurrentHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset((double)e.NewValue);
            }
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer? scrollViewer = null;
            if (sender is ScrollViewer sv)
            {
                scrollViewer = sv;
            }
            else if (sender is DependencyObject d)
            {
                scrollViewer = FindVisualChild<ScrollViewer>(d);
            }

            if (scrollViewer == null) return;

            bool isHorizontal = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isHorizontal)
            {
                if (scrollViewer.ScrollableWidth <= 0) return;
                e.Handled = true;

                double delta = e.Delta;
                double existingTarget = GetTargetHorizontalOffset(scrollViewer);
                if (double.IsNaN(existingTarget) || Math.Abs(existingTarget - scrollViewer.HorizontalOffset) > 200)
                {
                    existingTarget = scrollViewer.HorizontalOffset;
                }

                double step = -(delta / 120.0) * 140.0;
                double newTarget = Math.Clamp(existingTarget + step, 0, scrollViewer.ScrollableWidth);
                SetTargetHorizontalOffset(scrollViewer, newTarget);

                SetCurrentHorizontalOffset(scrollViewer, scrollViewer.HorizontalOffset);

                var anim = new DoubleAnimation
                {
                    From = scrollViewer.HorizontalOffset,
                    To = newTarget,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (s, ev) =>
                {
                    SetTargetHorizontalOffset(scrollViewer, double.NaN);
                };

                scrollViewer.BeginAnimation(CurrentHorizontalOffsetProperty, anim, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                if (scrollViewer.ScrollableHeight <= 0) return;
                e.Handled = true;

                double delta = e.Delta;
                double existingTarget = GetTargetVerticalOffset(scrollViewer);
                if (double.IsNaN(existingTarget) || Math.Abs(existingTarget - scrollViewer.VerticalOffset) > 200)
                {
                    existingTarget = scrollViewer.VerticalOffset;
                }

                // 120 delta per wheel notch -> 130px smooth stride
                double step = -(delta / 120.0) * 130.0;
                double newTarget = Math.Clamp(existingTarget + step, 0, scrollViewer.ScrollableHeight);
                SetTargetVerticalOffset(scrollViewer, newTarget);

                SetCurrentVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);

                var anim = new DoubleAnimation
                {
                    From = scrollViewer.VerticalOffset,
                    To = newTarget,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (s, ev) =>
                {
                    SetTargetVerticalOffset(scrollViewer, double.NaN);
                };

                scrollViewer.BeginAnimation(CurrentVerticalOffsetProperty, anim, HandoffBehavior.SnapshotAndReplace);
            }
        }

        internal static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var nested = FindVisualChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
