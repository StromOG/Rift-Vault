using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RiftVault.Services;
using RiftVault.ViewModels;
using RiftVault.Win32;

namespace RiftVault.UI
{
    public partial class InvestigatorWindow : Window
    {
        private readonly InvestigatorViewModel _viewModel;
        private readonly IThemeService _themeService;
        private readonly ISettingsService? _settingsService;

        public InvestigatorWindow(InvestigatorViewModel viewModel, IThemeService themeService, ISettingsService? settingsService = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _themeService = themeService;
            _settingsService = settingsService;
            DataContext = _viewModel;

            SourceInitialized += (s, e) => GlassHelper.EnableRoundedCorners(this);
            Loaded += InvestigatorWindow_Loaded;
            KeyDown += InvestigatorWindow_KeyDown;
        }

        private void InvestigatorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _themeService.ApplyGlassEffect(this);
                if (_settingsService != null)
                {
                    AnimationHelper.ApplyWindowCorners(this, InvestigatorRootBorder, _settingsService.Current.WindowCornerRadius);
                    AnimationHelper.ApplyWindowEntrance(this, InvestigatorRootBorder, _settingsService.Current);
                }
            }
            catch { }
        }

        private bool _isClosingAnimated = false;
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosingAnimated && _settingsService?.Current.EnableAnimations == true && _settingsService.Current.WindowEntranceAnimation != "Instant")
            {
                e.Cancel = true;
                _isClosingAnimated = true;
                AnimationHelper.ApplyWindowExit(this, InvestigatorRootBorder, _settingsService.Current, () =>
                {
                    Close();
                });
                return;
            }
            base.OnClosing(e);
        }

        public async void InspectTarget(string path, string initialTab = "Properties")
        {
            await _viewModel.LoadTargetAsync(path, initialTab);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InvestigatorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = _viewModel.TargetPath;
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }
    }
}
