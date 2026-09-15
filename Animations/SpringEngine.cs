using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace RiftVault.Animations
{
    public struct SpringConfig
    {
        public double Stiffness { get; }
        public double Damping { get; }
        public double Mass { get; }

        public SpringConfig(double stiffness, double damping, double mass = 1.0)
        {
            Stiffness = stiffness;
            Damping = damping;
            Mass = mass;
        }

        public static SpringConfig UI = new SpringConfig(280, 26);
        public static SpringConfig Snappy = new SpringConfig(400, 30);
        public static SpringConfig Gentle = new SpringConfig(180, 20);
        public static SpringConfig Bounce = new SpringConfig(300, 18);
    }

    public class SpringState
    {
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double Velocity { get; set; }
        public SpringConfig Config { get; set; }
        public Action<double>? OnUpdate { get; set; }
        public Action? OnComplete { get; set; }
        public bool IsComplete { get; set; }
    }

    public static class SpringEngine
    {
        private static readonly List<SpringState> _activeSprings = new();
        private static bool _isRendering = false;
        private static DateTime _lastFrameTime;

        public static void AnimateDouble(double from, double to, SpringConfig config, Action<double> onUpdate, Action? onComplete = null)
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                onUpdate(to);
                onComplete?.Invoke();
                return;
            }

            var state = new SpringState
            {
                CurrentValue = from,
                TargetValue = to,
                Velocity = 0,
                Config = config,
                OnUpdate = onUpdate,
                OnComplete = onComplete
            };

            _activeSprings.Add(state);
            StartRendering();
        }

        public static void AnimateTranslateX(UIElement element, double targetX, SpringConfig config)
        {
            if (element.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                element.RenderTransform = tt;
            }
            AnimateDouble(tt.X, targetX, config, val => tt.X = val);
        }

        public static void AnimateTranslateY(UIElement element, double targetY, SpringConfig config)
        {
            if (element.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                element.RenderTransform = tt;
            }
            AnimateDouble(tt.Y, targetY, config, val => tt.Y = val);
        }

        public static void AnimateOpacity(UIElement element, double targetOpacity, SpringConfig config)
        {
            AnimateDouble(element.Opacity, targetOpacity, config, val => element.Opacity = Math.Clamp(val, 0, 1));
        }

        public static void AnimateScale(UIElement element, double targetScale, SpringConfig config)
        {
            if (element.RenderTransform is not ScaleTransform st)
            {
                st = new ScaleTransform();
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = st;
            }
            AnimateDouble(st.ScaleX, targetScale, config, val =>
            {
                st.ScaleX = val;
                st.ScaleY = val;
            });
        }

        private static void StartRendering()
        {
            if (!_isRendering)
            {
                _lastFrameTime = DateTime.Now;
                CompositionTarget.Rendering += OnRendering;
                _isRendering = true;
            }
        }

        private static void StopRendering()
        {
            if (_isRendering)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isRendering = false;
            }
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            double dt = (now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            if (dt > 0.064) dt = 0.016; // Cap dt to prevent explosion on lag spikes

            for (int i = _activeSprings.Count - 1; i >= 0; i--)
            {
                var spring = _activeSprings[i];
                StepRK4(spring, dt);

                spring.OnUpdate?.Invoke(spring.CurrentValue);

                if (Math.Abs(spring.Velocity) < 0.01 && Math.Abs(spring.TargetValue - spring.CurrentValue) < 0.01)
                {
                    spring.CurrentValue = spring.TargetValue;
                    spring.OnUpdate?.Invoke(spring.CurrentValue);
                    spring.IsComplete = true;
                    spring.OnComplete?.Invoke();
                    _activeSprings.RemoveAt(i);
                }
            }

            if (_activeSprings.Count == 0)
            {
                StopRendering();
            }
        }

        private static void StepRK4(SpringState state, double dt)
        {
            double x = state.CurrentValue;
            double v = state.Velocity;
            double k = state.Config.Stiffness;
            double c = state.Config.Damping;
            double m = state.Config.Mass;
            double target = state.TargetValue;

            double a1 = Acceleration(x, v, target, k, c, m);
            double v1 = v;

            double a2 = Acceleration(x + v1 * dt * 0.5, v + a1 * dt * 0.5, target, k, c, m);
            double v2 = v + a1 * dt * 0.5;

            double a3 = Acceleration(x + v2 * dt * 0.5, v + a2 * dt * 0.5, target, k, c, m);
            double v3 = v + a2 * dt * 0.5;

            double a4 = Acceleration(x + v3 * dt, v + a3 * dt, target, k, c, m);
            double v4 = v + a3 * dt;

            double dxdt = (v1 + 2 * v2 + 2 * v3 + v4) / 6.0;
            double dvdt = (a1 + 2 * a2 + 2 * a3 + a4) / 6.0;

            state.CurrentValue += dxdt * dt;
            state.Velocity += dvdt * dt;
        }

        private static double Acceleration(double x, double v, double target, double k, double c, double m)
        {
            return (-k * (x - target) - c * v) / m;
        }
    }
}