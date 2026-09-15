using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using RiftVault.Services;
using RiftVault.Win32;

namespace RiftVault.UI
{
    public partial class FirstRunSetupWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;

        private int _currentStep = 1;
        private string _selectedTheme = "SandstormPeach";
        private string _selectedAccent = "#E07A5F";
        private string _selectedCornerRadius = "16";
        private string _installPath = "";

        public FirstRunSetupWindow(ISettingsService settingsService, IThemeService themeService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _themeService = themeService;

            SourceInitialized += (s, e) => GlassHelper.EnableRoundedCorners(this);
            Loaded += (s, e) =>
            {
                AnimationHelper.ApplyWindowCorners(this, FirstRunRootBorder, _settingsService.Current.WindowCornerRadius);
                AnimationHelper.ApplyWindowEntrance(this, FirstRunRootBorder, _settingsService.Current);
            };

            InitializeDefaults();
            GoToStep(1);
        }

        private void InitializeDefaults()
        {
            var s = _settingsService.Current;
            _selectedTheme = string.IsNullOrEmpty(s.Theme) ? "SandstormPeach" : s.Theme;
            _selectedAccent = string.IsNullOrEmpty(s.CustomAccentColor) ? "#E07A5F" : s.CustomAccentColor;
            _selectedCornerRadius = string.IsNullOrEmpty(s.WindowCornerRadius) ? "16" : s.WindowCornerRadius;

            _installPath = string.IsNullOrEmpty(s.InstallationDirectory)
                ? SystemIntegrationHelper.GetDefaultInstallDirectory()
                : s.InstallationDirectory;
            InstallPathText.Text = _installPath;

            CheckDesktopShortcut.IsChecked = s.CreateDesktopShortcut;
            CheckContextMenu.IsChecked = s.IntegrateWithExplorerContextMenu;

            HighlightThemeCards();
            HighlightCornerButtons();
        }

        public void PublicGoToStep(int step)
        {
            GoToStep(step);
        }

        private void GoToStep(int step)
        {
            if (step < 1) step = 1;
            if (step > 3) step = 3;
            _currentStep = step;

            // Page Visibilities
            Step1Container.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Container.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Container.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Subtitle Header
            StepSubtitleHeader.Text = step switch
            {
                1 => "Step 1 of 3 · Welcome & Destination",
                2 => "Step 2 of 3 · Choose Your Aesthetic",
                3 => "Step 3 of 3 · Ready to Launch",
                _ => $"Step {step} of 3"
            };

            UpdateStepperNodes();

            // Navigation buttons
            BtnBack.IsEnabled = (step > 1);
            BtnNext.Content = step switch
            {
                1 => "Continue to Appearance →",
                2 => "Continue to Launch →",
                3 => "Launch Rift Vault →",
                _ => "Continue →"
            };

            if (step == 3)
            {
                UpdateRecap();
            }
        }

        private void UpdateStepperNodes()
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var cardBorder = (Brush)FindResource("CardBorderBrush");
                var surface = (Brush)FindResource("SurfaceBrush");
                var textPrimary = (Brush)FindResource("TextPrimaryBrush");
                var textDim = (Brush)FindResource("TextDimBrush");
                var activeBrush = (Brush)FindResource("TabActiveBrush");

                Border[] nodes = { StepNode1, StepNode2, StepNode3 };
                Border[] badges = { StepBadge1, StepBadge2, StepBadge3 };
                TextBlock[] badgeTexts = { StepBadgeText1, StepBadgeText2, StepBadgeText3 };
                TextBlock[] labels = { StepLabel1, StepLabel2, StepLabel3 };

                for (int i = 0; i < 3; i++)
                {
                    int nodeStep = i + 1;
                    if (nodeStep < _currentStep)
                    {
                        // Completed
                        nodes[i].Background = surface;
                        nodes[i].BorderBrush = accent;
                        badges[i].Background = accent;
                        badgeTexts[i].Text = "✓";
                        badgeTexts[i].Foreground = Brushes.White;
                        labels[i].FontWeight = FontWeights.SemiBold;
                        labels[i].Foreground = textPrimary;
                    }
                    else if (nodeStep == _currentStep)
                    {
                        // Active
                        nodes[i].Background = activeBrush;
                        nodes[i].BorderBrush = accent;
                        badges[i].Background = accent;
                        badgeTexts[i].Text = nodeStep.ToString();
                        badgeTexts[i].Foreground = Brushes.White;
                        labels[i].FontWeight = FontWeights.Bold;
                        labels[i].Foreground = textPrimary;
                    }
                    else
                    {
                        // Upcoming
                        nodes[i].Background = surface;
                        nodes[i].BorderBrush = cardBorder;
                        badges[i].Background = (Brush)FindResource("PillBackgroundBrush");
                        badgeTexts[i].Text = nodeStep.ToString();
                        badgeTexts[i].Foreground = textDim;
                        labels[i].FontWeight = FontWeights.Normal;
                        labels[i].Foreground = textDim;
                    }
                }
            }
            catch { }
        }

        private void StepNode_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && int.TryParse(b.Tag?.ToString(), out int step))
            {
                GoToStep(step);
            }
        }

        #region Step 1 Handlers
        private void BrowseInstallPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Rift Vault Installation Directory",
                InitialDirectory = _installPath
            };
            if (dialog.ShowDialog(this) == true)
            {
                _installPath = dialog.FolderName;
                InstallPathText.Text = _installPath;
            }
        }
        #endregion

        #region Step 2 Handlers
        private void ThemeCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string themeName)
            {
                _selectedTheme = themeName;
                _themeService.ApplyTheme(themeName);
                HighlightThemeCards();
                UpdateStepperNodes();
            }
        }

        private void HighlightThemeCards()
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var normal = (Brush)FindResource("CardBorderBrush");

                Border[] cards = { CardTheme_SandstormPeach, CardTheme_ObsidianAurora, CardTheme_ArcticMist, CardTheme_PureBlack };
                string[] themes = { "SandstormPeach", "ObsidianAurora", "ArcticMist", "PureBlack" };

                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    bool isMatch = string.Equals(_selectedTheme, themes[i], StringComparison.OrdinalIgnoreCase);
                    cards[i].BorderBrush = isMatch ? accent : normal;
                    cards[i].BorderThickness = isMatch ? new Thickness(2.0) : new Thickness(1.0);
                }
            }
            catch { }
        }

        private void CornerOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string rad)
            {
                _selectedCornerRadius = rad;
                HighlightCornerButtons();
            }
        }

        private void HighlightCornerButtons()
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var normal = (Brush)FindResource("SurfaceRaisedBrush");

                if (BtnCornerCurved != null)
                {
                    bool isCurved = _selectedCornerRadius == "16";
                    BtnCornerCurved.BorderBrush = isCurved ? accent : (Brush)FindResource("CardBorderBrush");
                    BtnCornerCurved.FontWeight = isCurved ? FontWeights.Bold : FontWeights.Normal;
                }
                if (BtnCornerSharp != null)
                {
                    bool isSharp = _selectedCornerRadius == "0";
                    BtnCornerSharp.BorderBrush = isSharp ? accent : (Brush)FindResource("CardBorderBrush");
                    BtnCornerSharp.FontWeight = isSharp ? FontWeights.Bold : FontWeights.Normal;
                }
            }
            catch { }
        }
        #endregion

        #region Step 3 Handlers
        private void UpdateRecap()
        {
            RecapThemeText.Text = _selectedTheme switch
            {
                "SandstormPeach" => "Sandstorm Peach (Bright Default)",
                "ObsidianAurora" => "Obsidian Aurora (Dark Glass)",
                "ArcticMist" => "Tokyo Cyber (Neon Cyan)",
                "PureBlack" => "Pure Black OLED",
                _ => _selectedTheme
            };

            RecapPathText.Text = string.IsNullOrEmpty(_installPath) ? "%LocalAppData%\\Programs\\RiftVault" : _installPath;

            string integrations = "";
            if (CheckDesktopShortcut.IsChecked == true) integrations += "Desktop Shortcut";
            if (CheckContextMenu.IsChecked == true)
            {
                if (!string.IsNullOrEmpty(integrations)) integrations += " · ";
                integrations += "Explorer Context Menu";
            }
            if (string.IsNullOrEmpty(integrations)) integrations = "Standard Standalone";
            RecapIntegrationsText.Text = integrations;
        }

        private async void Finish_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteFinish();
        }

        private async Task ExecuteFinish()
        {
            try
            {
                var s = _settingsService.Current;
                s.Theme = _selectedTheme;
                s.CustomAccentColor = _selectedTheme == "SandstormPeach" ? "#E07A5F" : s.CustomAccentColor;
                s.WindowCornerRadius = _selectedCornerRadius;
                s.InstallationDirectory = _installPath;
                s.CreateDesktopShortcut = CheckDesktopShortcut.IsChecked == true;
                s.IntegrateWithExplorerContextMenu = CheckContextMenu.IsChecked == true;
                s.IsFirstRunSetupComplete = true;
                _settingsService.Save();

                if (s.CreateDesktopShortcut)
                {
                    SystemIntegrationHelper.CreateDesktopShortcut();
                }

                if (s.IntegrateWithExplorerContextMenu)
                {
                    SystemIntegrationHelper.RegisterExplorerContextMenu();
                }
                else
                {
                    SystemIntegrationHelper.UnregisterExplorerContextMenu();
                }

                await Task.Delay(150);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Setup note: {ex.Message}", "Rift Vault Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
        }
        #endregion

        #region Navigation Buttons
        private void PrevStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                GoToStep(_currentStep - 1);
            }
        }

        private async void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 3)
            {
                GoToStep(_currentStep + 1);
            }
            else
            {
                await ExecuteFinish();
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool _isClosingAnimated = false;
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosingAnimated && _settingsService.Current.EnableAnimations && _settingsService.Current.WindowEntranceAnimation != "Instant")
            {
                e.Cancel = true;
                _isClosingAnimated = true;
                AnimationHelper.ApplyWindowExit(this, FirstRunRootBorder, _settingsService.Current, () =>
                {
                    Close();
                });
                return;
            }
            base.OnClosing(e);
        }
        #endregion
    }
}
