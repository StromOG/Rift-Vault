using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RiftVault.Win32;

namespace RiftVault.Services
{
    public interface IThemeService
    {
        void InitializeTheme();
        void ApplyTheme(string themeName);
        void ApplyGlassEffect(Window window);
        void ApplyCustomAccent(string hexColor);
        void ApplyUIDensity(string density);
        void ApplyAnimationSpeed(string speed);
        void ApplyBackdrop(Window window, string backdrop);
    }

    public class ThemeService : IThemeService
    {
        private readonly ISettingsService _settingsService;

        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void InitializeTheme()
        {
            ApplyTheme(_settingsService.Current.Theme);
            ApplyUIDensity(_settingsService.Current.UIDensity);
            ApplyAnimationSpeed(_settingsService.Current.AnimationSpeed);
            if (!string.IsNullOrEmpty(_settingsService.Current.CustomAccentColor))
            {
                ApplyCustomAccent(_settingsService.Current.CustomAccentColor);
            }
        }

        public void ApplyTheme(string themeName)
        {
            var app = Application.Current;
            if (app == null) return;

            var existingTheme = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/") && !d.Source.OriginalString.Contains("SharedStyles"));
            if (existingTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(existingTheme);
            }

            string sourceUri = $"UI/Themes/{themeName}.xaml";
            try
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(sourceUri, UriKind.Relative) });
                _settingsService.Current.Theme = themeName;
                _settingsService.Save();

                // Apply custom accent if set
                if (!string.IsNullOrEmpty(_settingsService.Current.CustomAccentColor))
                {
                    ApplyCustomAccent(_settingsService.Current.CustomAccentColor);
                }
            }
            catch { /* Fallback to default */ }
        }

        public void ApplyCustomAccent(string hexColor)
        {
            var app = Application.Current;
            if (app == null || string.IsNullOrWhiteSpace(hexColor)) return;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                app.Resources["AccentColor"] = color;
                app.Resources["AccentBrush"] = new SolidColorBrush(color);
                app.Resources["SelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x44, color.R, color.G, color.B));
                app.Resources["HoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, color.R, color.G, color.B));
                app.Resources["GlassAccentGlowBrush"] = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B));
                _settingsService.Current.CustomAccentColor = hexColor;
                _settingsService.Save();
            }
            catch { }
        }

        public void ApplyUIDensity(string density)
        {
            var app = Application.Current;
            if (app == null) return;
            try
            {
                double minHeight = 36;
                Thickness padding = new Thickness(6, 2, 6, 2);
                Thickness margin = new Thickness(2, 1, 2, 1);
                double fontSize = 13.0;

                if (string.Equals(density, "Compact", StringComparison.OrdinalIgnoreCase))
                {
                    minHeight = 28;
                    padding = new Thickness(4, 1, 4, 1);
                    margin = new Thickness(1, 0.5, 1, 0.5);
                    fontSize = 12.0;
                }
                else if (string.Equals(density, "Spacious", StringComparison.OrdinalIgnoreCase))
                {
                    minHeight = 44;
                    padding = new Thickness(8, 4, 8, 4);
                    margin = new Thickness(3, 2, 3, 2);
                    fontSize = 14.0;
                }

                app.Resources["FileItemMinHeight"] = minHeight;
                app.Resources["FileItemPadding"] = padding;
                app.Resources["FileItemMargin"] = margin;
                app.Resources["FileItemFontSize"] = fontSize;

                _settingsService.Current.UIDensity = density;
                _settingsService.Save();
            }
            catch { }
        }

        public void ApplyAnimationSpeed(string speed)
        {
            var app = Application.Current;
            if (app == null) return;
            try
            {
                Duration dur = new Duration(TimeSpan.FromMilliseconds(200));
                if (string.Equals(speed, "Instant", StringComparison.OrdinalIgnoreCase))
                    dur = new Duration(TimeSpan.FromMilliseconds(0));
                else if (string.Equals(speed, "Fast", StringComparison.OrdinalIgnoreCase))
                    dur = new Duration(TimeSpan.FromMilliseconds(100));
                else if (string.Equals(speed, "Smooth", StringComparison.OrdinalIgnoreCase) || string.Equals(speed, "Relaxed", StringComparison.OrdinalIgnoreCase))
                    dur = new Duration(TimeSpan.FromMilliseconds(350));

                app.Resources["AnimationDuration"] = dur;
                _settingsService.Current.AnimationSpeed = speed;
                _settingsService.Save();
            }
            catch { }
        }

        public void ApplyBackdrop(Window window, string backdrop)
        {
            bool isDark = _settingsService.Current.Theme != "ArcticMist";
            GlassHelper.ApplyBackdrop(window, backdrop, isDark);
            _settingsService.Current.WindowBackdrop = backdrop;
            _settingsService.Save();
        }

        public void ApplyGlassEffect(Window window)
        {
            bool isDark = _settingsService.Current.Theme != "ArcticMist";
            GlassHelper.ApplyBackdrop(window, _settingsService.Current.WindowBackdrop, isDark);
        }
    }
}