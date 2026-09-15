using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RiftVault.Animations
{
    public class RippleEffect : ContentControl
    {
        private Canvas? _canvas;
        private Ellipse? _ripple;

        public RippleEffect()
        {
            ClipToBounds = true;
            Background = Brushes.Transparent;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _canvas = new Canvas { IsHitTestVisible = false };
            
            if (VisualChildrenCount > 0 && GetVisualChild(0) is Grid grid)
            {
                grid.Children.Add(_canvas);
            }
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            if (_canvas == null) return;

            var pos = e.GetPosition(this);
            
            _ripple = new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Width = 0,
                Height = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            Canvas.SetLeft(_ripple, pos.X);
            Canvas.SetTop(_ripple, pos.Y);
            _canvas.Children.Add(_ripple);

            double targetSize = Math.Max(ActualWidth, ActualHeight) * 2.5;

            SpringEngine.AnimateDouble(0, targetSize, SpringConfig.Gentle, val =>
            {
                if (_ripple == null) return;
                _ripple.Width = val;
                _ripple.Height = val;
                Canvas.SetLeft(_ripple, pos.X - val / 2);
                Canvas.SetTop(_ripple, pos.Y - val / 2);
            });

            SpringEngine.AnimateDouble(1.0, 0.0, SpringConfig.Gentle, val =>
            {
                if (_ripple != null) _ripple.Opacity = val;
            }, () =>
            {
                if (_ripple != null && _canvas.Children.Contains(_ripple))
                {
                    _canvas.Children.Remove(_ripple);
                }
            });
        }
    }
}