using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using RiftVault.Models;
using RiftVault.Win32;

namespace RiftVault.UI
{
    public static class AnimationHelper
    {
        public static IEasingFunction GetEasingFunction(string? easingName, double springIntensity = 0.25)
        {
            return (easingName ?? "Cubic").ToLowerInvariant() switch
            {
                "quintic" => new QuinticEase { EasingMode = EasingMode.EaseOut },
                "back" => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = Math.Clamp(springIntensity, 0.05, 0.85) },
                "elastic" => new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 },
                "quadratic" => new QuadraticEase { EasingMode = EasingMode.EaseOut },
                _ => new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }

        public static void ApplyWindowCorners(Window window, Border? rootBorder, string? radiusStr)
        {
            if (!double.TryParse(radiusStr, out double radius)) radius = 16.0;
            if (radius < 0) radius = 0;

            if (rootBorder != null)
            {
                rootBorder.CornerRadius = new CornerRadius(radius);
            }

            try
            {
                var chrome = WindowChrome.GetWindowChrome(window);
                if (chrome != null)
                {
                    chrome.CornerRadius = new CornerRadius(radius);
                }
                GlassHelper.EnableRoundedCorners(window, radius > 0);
            }
            catch { }
        }

        public static void ApplyWindowEntrance(Window window, Border rootBorder, AppSettings settings)
        {
            if (rootBorder == null) return;

            // Apply corner radius according to settings
            ApplyWindowCorners(window, rootBorder, settings.WindowCornerRadius);

            if (!settings.EnableAnimations || string.Equals(settings.WindowEntranceAnimation, "Instant", StringComparison.OrdinalIgnoreCase))
            {
                rootBorder.Opacity = 1.0;
                EnsureTransforms(rootBorder, out var scale, out var translate);
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
                translate.Y = 0;
                return;
            }

            EnsureTransforms(rootBorder, out var scaleT, out var translateT);
            rootBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            string style = string.IsNullOrEmpty(settings.WindowEntranceAnimation) ? "FluentSpring" : settings.WindowEntranceAnimation;
            double durationMs = Math.Clamp(settings.WindowAnimationDurationMs, 80.0, 1000.0);
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var easing = GetEasingFunction(settings.AnimationEasingFunction, settings.AnimationSpringIntensity);

            var sb = new Storyboard();

            // Opacity fade in
            rootBorder.Opacity = 0.0;
            var opacityAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(opacityAnim, rootBorder);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            sb.Children.Add(opacityAnim);

            switch (style.ToLowerInvariant())
            {
                case "fluentspring":
                    scaleT.ScaleX = 0.93;
                    scaleT.ScaleY = 0.93;
                    translateT.Y = 8;
                    var springEasing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = Math.Clamp(settings.AnimationSpringIntensity, 0.1, 0.6) };
                    
                    var scaleXSpring = new DoubleAnimation(0.93, 1.0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(scaleXSpring, scaleT);
                    Storyboard.SetTargetProperty(scaleXSpring, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSpring);

                    var scaleYSpring = new DoubleAnimation(0.93, 1.0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(scaleYSpring, scaleT);
                    Storyboard.SetTargetProperty(scaleYSpring, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSpring);

                    var transYSpring = new DoubleAnimation(8, 0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(transYSpring, translateT);
                    Storyboard.SetTargetProperty(transYSpring, new PropertyPath("Y"));
                    sb.Children.Add(transYSpring);
                    break;

                case "smoothscale":
                    scaleT.ScaleX = 0.95;
                    scaleT.ScaleY = 0.95;
                    translateT.Y = 10;
                    
                    var scaleXSmooth = new DoubleAnimation(0.95, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleXSmooth, scaleT);
                    Storyboard.SetTargetProperty(scaleXSmooth, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSmooth);

                    var scaleYSmooth = new DoubleAnimation(0.95, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleYSmooth, scaleT);
                    Storyboard.SetTargetProperty(scaleYSmooth, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSmooth);

                    var transYSmooth = new DoubleAnimation(10, 0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(transYSmooth, translateT);
                    Storyboard.SetTargetProperty(transYSmooth, new PropertyPath("Y"));
                    sb.Children.Add(transYSmooth);
                    break;

                case "slideup":
                    scaleT.ScaleX = 1.0;
                    scaleT.ScaleY = 1.0;
                    translateT.Y = 32;

                    var transYSlide = new DoubleAnimation(32, 0, duration) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(transYSlide, translateT);
                    Storyboard.SetTargetProperty(transYSlide, new PropertyPath("Y"));
                    sb.Children.Add(transYSlide);
                    break;

                case "softzoom":
                    scaleT.ScaleX = 0.98;
                    scaleT.ScaleY = 0.98;
                    translateT.Y = 0;

                    var scaleXSoft = new DoubleAnimation(0.98, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleXSoft, scaleT);
                    Storyboard.SetTargetProperty(scaleXSoft, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSoft);

                    var scaleYSoft = new DoubleAnimation(0.98, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleYSoft, scaleT);
                    Storyboard.SetTargetProperty(scaleYSoft, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSoft);
                    break;

                case "fadeonly":
                default:
                    scaleT.ScaleX = 1.0;
                    scaleT.ScaleY = 1.0;
                    translateT.Y = 0;
                    break;
            }

            sb.Begin();
        }

        public static void ApplyWindowExit(Window window, Border rootBorder, AppSettings settings, Action onComplete)
        {
            if (rootBorder == null || !settings.EnableAnimations || string.Equals(settings.WindowEntranceAnimation, "Instant", StringComparison.OrdinalIgnoreCase))
            {
                onComplete();
                return;
            }

            double durationMs = Math.Clamp(settings.WindowCloseAnimationDurationMs, 60.0, 500.0);
            var duration = TimeSpan.FromMilliseconds(durationMs);
            EnsureTransforms(rootBorder, out var scaleT, out var translateT);

            var sb = new Storyboard();
            var opacityAnim = new DoubleAnimation(rootBorder.Opacity, 0.0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnim, rootBorder);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            sb.Children.Add(opacityAnim);

            var scaleX = new DoubleAnimation(scaleT.ScaleX, 0.96, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleX, scaleT);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("ScaleX"));
            sb.Children.Add(scaleX);

            var scaleY = new DoubleAnimation(scaleT.ScaleY, 0.96, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleY, scaleT);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("ScaleY"));
            sb.Children.Add(scaleY);

            sb.Completed += (s, e) => onComplete();
            sb.Begin();
        }

        public static void PlayPreviewAnimation(FrameworkElement target, AppSettings settings)
        {
            if (target == null) return;

            EnsureTransforms(target, out var scaleT, out var translateT);
            target.RenderTransformOrigin = new Point(0.5, 0.5);

            string style = string.IsNullOrEmpty(settings.WindowEntranceAnimation) ? "FluentSpring" : settings.WindowEntranceAnimation;
            double durationMs = Math.Clamp(settings.WindowAnimationDurationMs, 80.0, 1000.0);
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var easing = GetEasingFunction(settings.AnimationEasingFunction, settings.AnimationSpringIntensity);

            var sb = new Storyboard();

            target.Opacity = 0.0;
            var opacityAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(opacityAnim, target);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            sb.Children.Add(opacityAnim);

            switch (style.ToLowerInvariant())
            {
                case "fluentspring":
                    scaleT.ScaleX = 0.91;
                    scaleT.ScaleY = 0.91;
                    translateT.Y = 12;
                    var springEasing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = Math.Clamp(settings.AnimationSpringIntensity, 0.1, 0.7) };

                    var scaleXSpring = new DoubleAnimation(0.91, 1.0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(scaleXSpring, scaleT);
                    Storyboard.SetTargetProperty(scaleXSpring, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSpring);

                    var scaleYSpring = new DoubleAnimation(0.91, 1.0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(scaleYSpring, scaleT);
                    Storyboard.SetTargetProperty(scaleYSpring, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSpring);

                    var transYSpring = new DoubleAnimation(12, 0, duration) { EasingFunction = springEasing };
                    Storyboard.SetTarget(transYSpring, translateT);
                    Storyboard.SetTargetProperty(transYSpring, new PropertyPath("Y"));
                    sb.Children.Add(transYSpring);
                    break;

                case "smoothscale":
                    scaleT.ScaleX = 0.94;
                    scaleT.ScaleY = 0.94;
                    translateT.Y = 10;

                    var scaleXSmooth = new DoubleAnimation(0.94, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleXSmooth, scaleT);
                    Storyboard.SetTargetProperty(scaleXSmooth, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSmooth);

                    var scaleYSmooth = new DoubleAnimation(0.94, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleYSmooth, scaleT);
                    Storyboard.SetTargetProperty(scaleYSmooth, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSmooth);

                    var transYSmooth = new DoubleAnimation(10, 0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(transYSmooth, translateT);
                    Storyboard.SetTargetProperty(transYSmooth, new PropertyPath("Y"));
                    sb.Children.Add(transYSmooth);
                    break;

                case "slideup":
                    scaleT.ScaleX = 1.0;
                    scaleT.ScaleY = 1.0;
                    translateT.Y = 36;

                    var transYSlide = new DoubleAnimation(36, 0, duration) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTarget(transYSlide, translateT);
                    Storyboard.SetTargetProperty(transYSlide, new PropertyPath("Y"));
                    sb.Children.Add(transYSlide);
                    break;

                case "softzoom":
                    scaleT.ScaleX = 0.97;
                    scaleT.ScaleY = 0.97;
                    translateT.Y = 0;

                    var scaleXSoft = new DoubleAnimation(0.97, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleXSoft, scaleT);
                    Storyboard.SetTargetProperty(scaleXSoft, new PropertyPath("ScaleX"));
                    sb.Children.Add(scaleXSoft);

                    var scaleYSoft = new DoubleAnimation(0.97, 1.0, duration) { EasingFunction = easing };
                    Storyboard.SetTarget(scaleYSoft, scaleT);
                    Storyboard.SetTargetProperty(scaleYSoft, new PropertyPath("ScaleY"));
                    sb.Children.Add(scaleYSoft);
                    break;

                case "fadeonly":
                default:
                    scaleT.ScaleX = 1.0;
                    scaleT.ScaleY = 1.0;
                    translateT.Y = 0;
                    break;
            }

            sb.Begin();
        }

        private static void EnsureTransforms(FrameworkElement element, out ScaleTransform scale, out TranslateTransform translate)
        {
            if (element.RenderTransform is TransformGroup group)
            {
                scale = FindTransform<ScaleTransform>(group) ?? new ScaleTransform(1.0, 1.0);
                translate = FindTransform<TranslateTransform>(group) ?? new TranslateTransform(0, 0);
            }
            else
            {
                var newGroup = new TransformGroup();
                scale = new ScaleTransform(1.0, 1.0);
                translate = new TranslateTransform(0, 0);
                newGroup.Children.Add(scale);
                newGroup.Children.Add(translate);
                element.RenderTransform = newGroup;
            }
        }

        private static T? FindTransform<T>(TransformGroup group) where T : Transform
        {
            foreach (var child in group.Children)
            {
                if (child is T match) return match;
            }
            var created = (T?)Activator.CreateInstance(typeof(T));
            if (created != null) group.Children.Add(created);
            return created;
        }
    }
}
