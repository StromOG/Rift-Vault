using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RiftVault.Core;
using RiftVault.Models;
using RiftVault.Services;
using RiftVault.Win32;

namespace RiftVault.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly IThumbnailCache? _thumbnailCache;
        private bool _isInitializing = true;

        public SettingsWindow(ISettingsService settingsService, IThemeService themeService, IThumbnailCache? thumbnailCache = null)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _themeService = themeService;
            _thumbnailCache = thumbnailCache;

            SourceInitialized += (s, e) => GlassHelper.EnableRoundedCorners(this);
            Loaded += (s, e) => AnimationHelper.ApplyWindowEntrance(this, SettingsRootBorder, _settingsService.Current);

            LoadSettingsIntoUI();
            _isInitializing = false;
        }

        private void LoadSettingsIntoUI()
        {
            var s = _settingsService.Current;

            // General
            SelectComboByTag(StartupBehaviorCombo, s.StartupBehavior);
            if (CustomStartupBox != null) CustomStartupBox.Text = s.CustomStartupPath;
            RestoreTabsToggle.IsChecked = s.RestorePreviousTabsOnLaunch;
            ConfirmDeleteToggle.IsChecked = s.ConfirmBeforeDelete;
            SingleClickToggle.IsChecked = s.SingleClickOpen;
            AutoRefreshToggle.IsChecked = s.AutoRefreshFileSystemChanges;
            if (DoubleClickBlankToggle != null) DoubleClickBlankToggle.IsChecked = s.DoubleClickBlankToGoUp;
            if (MiddleClickTabToggle != null) MiddleClickTabToggle.IsChecked = s.MiddleClickOpensInNewTab;
            if (ShowFullPathToggle != null) ShowFullPathToggle.IsChecked = s.ShowFullPathInTitleBar;
            if (ShowStatusBarToggle != null) ShowStatusBarToggle.IsChecked = s.ShowStatusBar;

            // Appearance
            SelectComboByTag(DensityCombo, s.UIDensity);
            SelectComboByTag(AnimationSpeedCombo, s.AnimationSpeed);
            // Window Curvature
            if (WindowCornersCombo != null) SelectComboByTag(WindowCornersCombo, s.WindowCornerRadius);
            UpdateCornerCardHighlights(s.WindowCornerRadius);
            AnimationHelper.ApplyWindowCorners(this, SettingsRootBorder, s.WindowCornerRadius);

            // Motion & Animation Studio
            if (EnableAnimationsToggle != null) EnableAnimationsToggle.IsChecked = s.EnableAnimations;
            if (WindowEntranceCombo != null) SelectComboByTag(WindowEntranceCombo, string.IsNullOrEmpty(s.WindowEntranceAnimation) ? "FluentSpring" : s.WindowEntranceAnimation);
            if (WindowDurationSlider != null)
            {
                WindowDurationSlider.Value = s.WindowAnimationDurationMs;
                if (WindowDurationText != null) WindowDurationText.Text = $"{(int)s.WindowAnimationDurationMs} ms";
            }
            if (WindowCloseDurationSlider != null)
            {
                WindowCloseDurationSlider.Value = s.WindowCloseAnimationDurationMs;
                if (WindowCloseDurationText != null) WindowCloseDurationText.Text = $"{(int)s.WindowCloseAnimationDurationMs} ms";
            }
            if (EasingFunctionCombo != null) SelectComboByTag(EasingFunctionCombo, s.AnimationEasingFunction);
            if (SpringIntensitySlider != null)
            {
                SpringIntensitySlider.Value = s.AnimationSpringIntensity;
                if (SpringIntensityText != null) SpringIntensityText.Text = s.AnimationSpringIntensity.ToString("F2");
            }
            WallpaperPathBox.Text = s.CustomBackgroundPath;
            WallpaperOpacitySlider.Value = s.BackgroundOpacity;
            if (WallpaperOpacityText != null) WallpaperOpacityText.Text = $"{(int)(s.BackgroundOpacity * 100)}%";
            WallpaperBlurSlider.Value = s.BackgroundBlur;
            if (WallpaperBlurText != null) WallpaperBlurText.Text = $"{(int)s.BackgroundBlur} px";
            if (WallpaperDimmerSlider != null) WallpaperDimmerSlider.Value = s.BackgroundDimmer;
            if (WallpaperDimmerText != null) WallpaperDimmerText.Text = $"{(int)(s.BackgroundDimmer * 100)}%";
            if (FileAreaGlassSlider != null) FileAreaGlassSlider.Value = s.FileAreaGlassOpacity;
            if (FileAreaGlassText != null) FileAreaGlassText.Text = $"{(int)(s.FileAreaGlassOpacity * 100)}%";
            SelectComboByTag(WallpaperStretchCombo, s.BackgroundStretch);
            UpdateThemeCardHighlights(s.Theme);
            UpdateAccentSwatchHighlights(s.CustomAccentColor);
            UpdateWallpaperCardHighlights(s.CustomBackgroundPath);
            UpdateLivePreview();

            // Files
            HiddenFilesToggle.IsChecked = s.ShowHiddenFiles;
            if (ProtectedSystemFilesToggle != null) ProtectedSystemFilesToggle.IsChecked = s.ShowProtectedSystemFiles;
            ExtensionsToggle.IsChecked = s.ShowFileExtensions;
            FolderSizesToggle.IsChecked = s.CalculateFolderSizesInBackground;
            SelectComboByTag(DateFormatCombo, s.DateFormat);
            PreviewPaneDefaultToggle.IsChecked = s.IsPreviewPaneOpen;
            AutoplayVideoToggle.IsChecked = s.AutoPlayVideoPreviews;
            CodeWrapToggle.IsChecked = s.WrapCodeText;

            // Tools & Terminal
            if (PreferredTerminalCombo != null) SelectComboByTag(PreferredTerminalCombo, s.PreferredTerminal);
            if (CustomTerminalPathBox != null) CustomTerminalPathBox.Text = s.CustomTerminalPath;
            if (PreferredEditorCombo != null) SelectComboByTag(PreferredEditorCombo, s.PreferredEditorApp);
            if (CustomEditorPathBox != null) CustomEditorPathBox.Text = s.CustomEditorPath;
            if (ArchiveFormatCombo != null) SelectComboByTag(ArchiveFormatCombo, s.DefaultArchiveFormat);
            if (CompressionLevelCombo != null) SelectComboByTag(CompressionLevelCombo, s.CompressionLevel);

            // AI
            if (AIProviderBuiltInRadio != null) AIProviderBuiltInRadio.IsChecked = s.AIProvider == "BuiltIn";
            if (AIProviderOllamaRadio != null) AIProviderOllamaRadio.IsChecked = s.AIProvider == "Ollama";
            if (AIProviderCloudRadio != null) AIProviderCloudRadio.IsChecked = s.AIProvider == "Cloud";
            if (AIProviderDisabledRadio != null) AIProviderDisabledRadio.IsChecked = s.AIProvider == "Disabled";

            if (LlmEndpointBox != null) LlmEndpointBox.Text = string.IsNullOrEmpty(s.OllamaEndpoint) ? "http://localhost:11434" : s.OllamaEndpoint;
            if (LlmModelBox != null) LlmModelBox.Text = string.IsNullOrEmpty(s.OllamaModel) ? "phi3:mini" : s.OllamaModel;

            if (CloudProviderCombo != null) SelectComboByTag(CloudProviderCombo, s.CloudProvider);
            if (CloudApiKeyBox != null) CloudApiKeyBox.Text = s.CloudApiKey;
            if (CloudModelBox != null) CloudModelBox.Text = string.IsNullOrEmpty(s.CloudModel) ? "gpt-4o-mini" : s.CloudModel;

            if (AIPersonalityCombo != null) SelectComboByTag(AIPersonalityCombo, s.AIPersonality);
            if (AITemperatureSlider != null)
            {
                AITemperatureSlider.Value = s.AITemperature;
                if (AITemperatureText != null) AITemperatureText.Text = s.AITemperature.ToString("F1");
            }
            if (AICustomPromptBox != null) AICustomPromptBox.Text = s.AICustomSystemPrompt;
            if (AIAllowFileOpsToggle != null) AIAllowFileOpsToggle.IsChecked = s.AIAllowFileOperations;
            if (AIMaxFilesCombo != null) SelectComboByTag(AIMaxFilesCombo, s.AIMaxFilesAnalyzed.ToString());

            SmartShelfToggle.IsChecked = s.EnableSmartShelf;
            DuplicateBrainToggle.IsChecked = s.EnableDuplicateBrain;
            AutoOrganizerToggle.IsChecked = s.EnableAutoOrganizer;
            FileDNAToggle.IsChecked = s.EnableFileDNA;
            SelectComboByTag(ComputeDeviceCombo, s.AIComputeDevice);
            UpdateAIProviderPanels();

            // Performance
            SelectComboByTag(ThumbnailCacheCombo, s.ThumbnailCacheSizeMB.ToString());
            SelectComboByTag(IOThreadsCombo, s.MaxConcurrentIOThreads.ToString());
            MemoryTrimToggle.IsChecked = s.EnableMemoryTrimmingOnIdle;

            // Privacy
            SelectComboByTag(ShreddingCombo, s.SecureShreddingPasses.ToString());

            // Audio
            UISoundsToggle.IsChecked = s.EnableUISounds;
            VolumeSlider.Value = s.SoundVolume;
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            if (combo == null) return;
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateThemeCardHighlights(string currentTheme)
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var normal = (Brush)FindResource("CardBorderBrush");

                Border[] cards = { ThemeCardObsidian, ThemeCardCrimson, ThemeCardPink, ThemeCardPureBlack, ThemeCardSolar, ThemeCardArctic };
                string[] themeNames = { "ObsidianAurora", "CyberCrimson", "MidnightPink", "PureBlack", "SolarFusion", "ArcticMist" };

                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    bool isSelected = string.Equals(currentTheme, themeNames[i], StringComparison.OrdinalIgnoreCase);
                    cards[i].BorderThickness = isSelected ? new Thickness(2.0) : new Thickness(1.0);
                    cards[i].BorderBrush = isSelected ? accent : normal;
                }
            }
            catch { }
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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Category_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.Tag is not string tag) return;

            // Hide all sections
            if (SectionGeneral != null) SectionGeneral.Visibility = Visibility.Collapsed;
            if (SectionAppearance != null) SectionAppearance.Visibility = Visibility.Collapsed;
            if (SectionFiles != null) SectionFiles.Visibility = Visibility.Collapsed;
            if (SectionTools != null) SectionTools.Visibility = Visibility.Collapsed;
            if (SectionShortcuts != null) SectionShortcuts.Visibility = Visibility.Collapsed;
            if (SectionAI != null) SectionAI.Visibility = Visibility.Collapsed;
            if (SectionPerformance != null) SectionPerformance.Visibility = Visibility.Collapsed;
            if (SectionPrivacy != null) SectionPrivacy.Visibility = Visibility.Collapsed;
            if (SectionAudio != null) SectionAudio.Visibility = Visibility.Collapsed;
            if (SectionAdvanced != null) SectionAdvanced.Visibility = Visibility.Collapsed;
            if (SectionAbout != null) SectionAbout.Visibility = Visibility.Collapsed;

            // Show selected
            switch (tag)
            {
                case "General": if (SectionGeneral != null) SectionGeneral.Visibility = Visibility.Visible; break;
                case "Appearance": if (SectionAppearance != null) SectionAppearance.Visibility = Visibility.Visible; break;
                case "Files": if (SectionFiles != null) SectionFiles.Visibility = Visibility.Visible; break;
                case "Tools": if (SectionTools != null) SectionTools.Visibility = Visibility.Visible; break;
                case "Shortcuts": if (SectionShortcuts != null) SectionShortcuts.Visibility = Visibility.Visible; break;
                case "AI": if (SectionAI != null) SectionAI.Visibility = Visibility.Visible; break;
                case "Performance": if (SectionPerformance != null) SectionPerformance.Visibility = Visibility.Visible; break;
                case "Privacy": if (SectionPrivacy != null) SectionPrivacy.Visibility = Visibility.Visible; break;
                case "Audio": if (SectionAudio != null) SectionAudio.Visibility = Visibility.Visible; break;
                case "Advanced": if (SectionAdvanced != null) SectionAdvanced.Visibility = Visibility.Visible; break;
                case "About": if (SectionAbout != null) SectionAbout.Visibility = Visibility.Visible; break;
            }
        }

        public void SelectCategory(string tag)
        {
            RadioButton? rb = tag switch
            {
                "General" => NavGeneral,
                "Appearance" => NavAppearance,
                "Files" => NavFiles,
                "Tools" => NavTools,
                "Shortcuts" => NavShortcuts,
                "AI" => NavAI,
                "Performance" => NavPerf,
                "Privacy" => NavPrivacy,
                "Audio" => NavAudio,
                "Advanced" => NavAdvanced,
                "About" => NavAbout,
                _ => NavGeneral
            };
            if (rb != null)
            {
                rb.IsChecked = true;
            }
        }

        public void ScrollToOffset(double offset)
        {
            SettingsContentScrollViewer?.ScrollToVerticalOffset(offset);
        }

        private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SettingsSearchBox.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                if (NavGeneral.IsChecked == true) Category_Checked(NavGeneral, null!);
                else if (NavAppearance.IsChecked == true) Category_Checked(NavAppearance, null!);
                else if (NavFiles.IsChecked == true) Category_Checked(NavFiles, null!);
                else if (NavTools.IsChecked == true) Category_Checked(NavTools, null!);
                else if (NavShortcuts.IsChecked == true) Category_Checked(NavShortcuts, null!);
                else if (NavAI.IsChecked == true) Category_Checked(NavAI, null!);
                else if (NavPerf.IsChecked == true) Category_Checked(NavPerf, null!);
                else if (NavPrivacy.IsChecked == true) Category_Checked(NavPrivacy, null!);
                else if (NavAudio.IsChecked == true) Category_Checked(NavAudio, null!);
                else if (NavAdvanced.IsChecked == true) Category_Checked(NavAdvanced, null!);
                else if (NavAbout.IsChecked == true) Category_Checked(NavAbout, null!);
                return;
            }

            StackPanel[] sections = { SectionGeneral, SectionAppearance, SectionFiles, SectionTools, SectionShortcuts, SectionAI, SectionPerformance, SectionPrivacy, SectionAudio, SectionAdvanced, SectionAbout };
            foreach (var section in sections)
            {
                if (section == null) continue;
                bool sectionHasMatch = false;

                foreach (var child in section.Children)
                {
                    if (child is Border card)
                    {
                        string content = ExtractText(card).ToLowerInvariant();
                        bool match = content.Contains(query);
                        card.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                        if (match) sectionHasMatch = true;
                    }
                }
                section.Visibility = sectionHasMatch ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ShortcutSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SectionShortcuts == null) return;
            string query = ShortcutSearchBox.Text.Trim().ToLowerInvariant();

            foreach (var child in SectionShortcuts.Children)
            {
                if (child is Border card)
                {
                    string text = ExtractText(card).ToLowerInvariant();
                    card.Visibility = string.IsNullOrEmpty(query) || text.Contains(query) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private static string ExtractText(DependencyObject obj)
        {
            var text = "";
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is TextBlock tb) text += " " + tb.Text;
                text += " " + ExtractText(child);
            }
            return text;
        }

        private void ThemeObsidian_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("ObsidianAurora");
        private void ThemeCrimson_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("CyberCrimson");
        private void ThemePink_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("MidnightPink");
        private void ThemePureBlack_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("PureBlack");
        private void ThemeSolar_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("SolarFusion");
        private void ThemeArctic_Click(object sender, MouseButtonEventArgs e) => ApplyTheme("ArcticMist");

        private void ApplyTheme(string themeName)
        {
            _themeService.ApplyTheme(themeName);
            _settingsService.Current.Theme = themeName;
            _settingsService.Save();
            UpdateThemeCardHighlights(themeName);
        }

        private void StartupBehavior_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || StartupBehaviorCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.StartupBehavior = item.Tag?.ToString() ?? "Home";
            _settingsService.Save();
        }

        private void Density_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || DensityCombo.SelectedItem is not ComboBoxItem item) return;
            string density = item.Tag?.ToString() ?? "Comfortable";
            _settingsService.Current.UIDensity = density;
            _settingsService.Save();
            _themeService.ApplyUIDensity(density);
        }

        private void AnimationSpeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || AnimationSpeedCombo.SelectedItem is not ComboBoxItem item) return;
            string speed = item.Tag?.ToString() ?? "Normal";
            _settingsService.Current.AnimationSpeed = speed;
            _settingsService.Save();
            _themeService.ApplyAnimationSpeed(speed);
        }

        private void WindowBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || WindowBackdropCombo.SelectedItem is not ComboBoxItem item) return;
            string backdrop = item.Tag?.ToString() ?? "Default";
            _settingsService.Current.WindowBackdrop = backdrop;
            _settingsService.Save();
            if (Application.Current?.MainWindow is MainWindow mw)
            {
                _themeService.ApplyBackdrop(mw, backdrop);
            }
            _themeService.ApplyBackdrop(this, backdrop);
        }

        #region Window Curvature & Fluid Motion
        private void EnableAnimations_ToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _settingsService.Current.EnableAnimations = EnableAnimationsToggle.IsChecked == true;
            _settingsService.Save();
        }

        private void WindowCorners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || WindowCornersCombo.SelectedItem is not ComboBoxItem item) return;
            string tag = item.Tag?.ToString() ?? "16";
            ApplyWindowCorners(tag);
        }

        private void CornerCard24_Click(object sender, MouseButtonEventArgs e) => ApplyWindowCorners("24");
        private void CornerCard16_Click(object sender, MouseButtonEventArgs e) => ApplyWindowCorners("16");
        private void CornerCard8_Click(object sender, MouseButtonEventArgs e) => ApplyWindowCorners("8");
        private void CornerCard0_Click(object sender, MouseButtonEventArgs e) => ApplyWindowCorners("0");

        private void ApplyWindowCorners(string radius)
        {
            _settingsService.Current.WindowCornerRadius = radius;
            _settingsService.Save();

            SelectComboByTag(WindowCornersCombo, radius);
            UpdateCornerCardHighlights(radius);
            AnimationHelper.ApplyWindowCorners(this, SettingsRootBorder, radius);

            if (Application.Current?.MainWindow is MainWindow mw && mw.RootBorder != null)
            {
                AnimationHelper.ApplyWindowCorners(mw, mw.RootBorder, radius);
            }
        }

        private void UpdateCornerCardHighlights(string currentRadius)
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var cardBorder = (Brush)FindResource("CardBorderBrush");

                if (CornerCard24 != null)
                {
                    CornerCard24.BorderBrush = currentRadius == "24" ? accent : cardBorder;
                    CornerCard24.BorderThickness = new Thickness(currentRadius == "24" ? 2 : 1.5);
                }
                if (CornerCard16 != null)
                {
                    CornerCard16.BorderBrush = currentRadius == "16" ? accent : cardBorder;
                    CornerCard16.BorderThickness = new Thickness(currentRadius == "16" ? 2 : 1.5);
                }
                if (CornerCard8 != null)
                {
                    CornerCard8.BorderBrush = currentRadius == "8" ? accent : cardBorder;
                    CornerCard8.BorderThickness = new Thickness(currentRadius == "8" ? 2 : 1.5);
                }
                if (CornerCard0 != null)
                {
                    CornerCard0.BorderBrush = currentRadius == "0" ? accent : cardBorder;
                    CornerCard0.BorderThickness = new Thickness(currentRadius == "0" ? 2 : 1.5);
                }
            }
            catch { }
        }

        private void WindowEntrance_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || WindowEntranceCombo.SelectedItem is not ComboBoxItem item) return;
            string anim = item.Tag?.ToString() ?? "FluentSpring";
            _settingsService.Current.WindowEntranceAnimation = anim;
            _settingsService.Save();
        }

        private void WindowDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            double val = Math.Round(WindowDurationSlider.Value);
            if (WindowDurationText != null) WindowDurationText.Text = $"{(int)val} ms";
            _settingsService.Current.WindowAnimationDurationMs = val;
            _settingsService.Save();
        }

        private void WindowCloseDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            double val = Math.Round(WindowCloseDurationSlider.Value);
            if (WindowCloseDurationText != null) WindowCloseDurationText.Text = $"{(int)val} ms";
            _settingsService.Current.WindowCloseAnimationDurationMs = val;
            _settingsService.Save();
        }

        private void EasingFunction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || EasingFunctionCombo.SelectedItem is not ComboBoxItem item) return;
            string easing = item.Tag?.ToString() ?? "Cubic";
            _settingsService.Current.AnimationEasingFunction = easing;
            _settingsService.Save();
        }

        private void SpringIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            double val = Math.Round(SpringIntensitySlider.Value, 2);
            if (SpringIntensityText != null) SpringIntensityText.Text = val.ToString("F2");
            _settingsService.Current.AnimationSpringIntensity = val;
            _settingsService.Save();
        }

        private void TestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (AnimationPreviewTarget != null)
            {
                AnimationHelper.PlayPreviewAnimation(AnimationPreviewTarget, _settingsService.Current);
            }
        }

        private bool _isClosingAnimated = false;
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosingAnimated && _settingsService.Current.EnableAnimations && _settingsService.Current.WindowEntranceAnimation != "Instant")
            {
                e.Cancel = true;
                _isClosingAnimated = true;
                AnimationHelper.ApplyWindowExit(this, SettingsRootBorder, _settingsService.Current, () =>
                {
                    Close();
                });
                return;
            }
            base.OnClosing(e);
        }
        #endregion

        private void DateFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || DateFormatCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.DateFormat = item.Tag?.ToString() ?? "Relative";
            _settingsService.Save();
        }

        private void ComputeDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ComputeDeviceCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.AIComputeDevice = item.Tag?.ToString() ?? "DirectML GPU";
            _settingsService.Save();
        }

        private void ThumbnailCache_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ThumbnailCacheCombo.SelectedItem is not ComboBoxItem item) return;
            if (int.TryParse(item.Tag?.ToString(), out int val))
            {
                _settingsService.Current.ThumbnailCacheSizeMB = val;
                _settingsService.Save();
            }
        }

        private void IOThreads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || IOThreadsCombo.SelectedItem is not ComboBoxItem item) return;
            if (int.TryParse(item.Tag?.ToString(), out int val))
            {
                _settingsService.Current.MaxConcurrentIOThreads = val;
                _settingsService.Save();
            }
        }

        private void Shredding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ShreddingCombo.SelectedItem is not ComboBoxItem item) return;
            if (int.TryParse(item.Tag?.ToString(), out int val))
            {
                _settingsService.Current.SecureShreddingPasses = val;
                _settingsService.Save();
            }
        }

        private void AIProvider_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _settingsService.Current.AIProvider = tag;
                _settingsService.Save();
                UpdateAIProviderPanels();
            }
        }

        private void UpdateAIProviderPanels()
        {
            try
            {
                string provider = _settingsService.Current.AIProvider;
                var accent = (Brush)FindResource("AccentBrush");
                var border = (Brush)FindResource("CardBorderBrush");

                if (OllamaCardBorder != null)
                {
                    OllamaCardBorder.BorderBrush = provider == "Ollama" ? accent : border;
                }
                if (CloudCardBorder != null)
                {
                    CloudCardBorder.BorderBrush = provider == "Cloud" ? accent : border;
                }
            }
            catch { }
        }

        private void AITemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            if (AITemperatureText != null) AITemperatureText.Text = e.NewValue.ToString("F1");
            _settingsService.Current.AITemperature = Math.Round(e.NewValue, 1);
            _settingsService.Save();
        }

        private void AIConfig_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var s = _settingsService.Current;
            if (LlmEndpointBox != null) s.OllamaEndpoint = LlmEndpointBox.Text.Trim();
            if (LlmModelBox != null) s.OllamaModel = LlmModelBox.Text.Trim();
            if (CloudApiKeyBox != null) s.CloudApiKey = CloudApiKeyBox.Text.Trim();
            if (CloudModelBox != null) s.CloudModel = CloudModelBox.Text.Trim();
            if (AICustomPromptBox != null) s.AICustomSystemPrompt = AICustomPromptBox.Text;
            _settingsService.Save();
        }

        private void CloudProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CloudProviderCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _settingsService.Current.CloudProvider = tag;
                _settingsService.Save();
            }
        }

        private void AIPersonality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (AIPersonalityCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _settingsService.Current.AIPersonality = tag;
                _settingsService.Save();
            }
        }

        private void AIMaxFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (AIMaxFilesCombo?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                _settingsService.Current.AIMaxFilesAnalyzed = val;
                _settingsService.Save();
            }
        }

        private void LaunchSetupWizard_Click(object sender, RoutedEventArgs e)
        {
            var setupWin = new FirstRunSetupWindow(_settingsService, _themeService)
            {
                Owner = this
            };
            if (setupWin.ShowDialog() == true)
            {
                LoadSettingsIntoUI();
            }
        }

        private async void TestLlmConnection_Click(object sender, RoutedEventArgs e)
        {
            string endpoint = LlmEndpointBox?.Text?.Trim() ?? "http://localhost:11434";
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                string baseUri = endpoint.Contains("/api") ? endpoint.Substring(0, endpoint.IndexOf("/api")) : endpoint;
                var res = await client.GetAsync(new Uri(new Uri(baseUri), "/api/version"));
                if (res.IsSuccessStatusCode)
                {
                    string content = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Successfully connected to Ollama!\nServer Response: {content}", "Ollama Connection OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Server responded with HTTP {res.StatusCode}.", "Ollama Server Status", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show("No Ollama server detected on this port.\n\nTip: Start Ollama or use the Built-In Local Neural Core which runs 100% offline with zero dependencies.", "Ollama Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Setting_ToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            var s = _settingsService.Current;
            s.RestorePreviousTabsOnLaunch = RestoreTabsToggle.IsChecked == true;
            s.ConfirmBeforeDelete = ConfirmDeleteToggle.IsChecked == true;
            s.SingleClickOpen = SingleClickToggle.IsChecked == true;
            s.AutoRefreshFileSystemChanges = AutoRefreshToggle.IsChecked == true;

            if (DoubleClickBlankToggle != null) s.DoubleClickBlankToGoUp = DoubleClickBlankToggle.IsChecked == true;
            if (MiddleClickTabToggle != null) s.MiddleClickOpensInNewTab = MiddleClickTabToggle.IsChecked == true;
            if (ShowFullPathToggle != null) s.ShowFullPathInTitleBar = ShowFullPathToggle.IsChecked == true;
            if (ShowStatusBarToggle != null) s.ShowStatusBar = ShowStatusBarToggle.IsChecked == true;
            if (ProtectedSystemFilesToggle != null) s.ShowProtectedSystemFiles = ProtectedSystemFilesToggle.IsChecked == true;

            s.ShowHiddenFiles = HiddenFilesToggle.IsChecked == true;
            s.ShowFileExtensions = ExtensionsToggle.IsChecked == true;
            s.CalculateFolderSizesInBackground = FolderSizesToggle.IsChecked == true;
            s.IsPreviewPaneOpen = PreviewPaneDefaultToggle.IsChecked == true;
            s.AutoPlayVideoPreviews = AutoplayVideoToggle.IsChecked == true;
            s.WrapCodeText = CodeWrapToggle.IsChecked == true;

            s.EnableSmartShelf = SmartShelfToggle.IsChecked == true;
            s.EnableDuplicateBrain = DuplicateBrainToggle.IsChecked == true;
            s.EnableAutoOrganizer = AutoOrganizerToggle.IsChecked == true;
            s.EnableFileDNA = FileDNAToggle.IsChecked == true;
            if (AIAllowFileOpsToggle != null) s.AIAllowFileOperations = AIAllowFileOpsToggle.IsChecked == true;

            s.EnableMemoryTrimmingOnIdle = MemoryTrimToggle.IsChecked == true;
            s.EnableUISounds = UISoundsToggle.IsChecked == true;

            _settingsService.Save();
            SyncLiveBackground();

            if (Application.Current?.MainWindow is MainWindow mw)
            {
                mw.UpdateWindowTitle();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _settingsService.Current.SoundVolume = e.NewValue;
            _settingsService.Save();
        }

        #region Custom Wallpaper & Preview Settings Handlers
        private void BrowseWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Custom Background Image",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                ApplyWallpaperPath(dialog.FileName);
            }
        }

        private void PresetAurora_Click(object sender, MouseButtonEventArgs e)
        {
            string path = ResolveWallpaperPath("cyber_aurora.jpg");
            ApplyWallpaperPath(path);
        }

        private void PresetPrism_Click(object sender, MouseButtonEventArgs e)
        {
            string path = ResolveWallpaperPath("abstract_prism.jpg");
            ApplyWallpaperPath(path);
        }

        private void PresetSpace_Click(object sender, MouseButtonEventArgs e)
        {
            string path = ResolveWallpaperPath("deep_space.jpg");
            ApplyWallpaperPath(path);
        }

        private void PresetWaves_Click(object sender, MouseButtonEventArgs e)
        {
            string path = ResolveWallpaperPath("minimal_waves.jpg");
            ApplyWallpaperPath(path);
        }

        private void ResetWallpaper_Click(object sender, RoutedEventArgs e)
        {
            ApplyWallpaperPath("");
        }

        private void ApplyWallpaperPath(string path)
        {
            var s = _settingsService.Current;
            s.CustomBackgroundPath = path;
            if (WallpaperPathBox != null) WallpaperPathBox.Text = path;
            _settingsService.Save();
            UpdateWallpaperCardHighlights(path);
            UpdateLivePreview();
            SyncLiveBackground();
        }

        private static string ResolveWallpaperPath(string fileName)
        {
            try
            {
                string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Wallpapers", fileName);
                if (File.Exists(binPath)) return binPath;

                string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..", "Assets", "Wallpapers", fileName));
                if (File.Exists(projectPath)) return projectPath;

                string hardcoded = Path.Combine(@"C:\Users\polic\Desktop\Antigravity\Rift VAult\Assets\Wallpapers", fileName);
                if (File.Exists(hardcoded)) return hardcoded;

                return binPath;
            }
            catch
            {
                return fileName;
            }
        }

        private void WallpaperOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            var s = _settingsService.Current;
            s.BackgroundOpacity = e.NewValue;
            if (WallpaperOpacityText != null)
            {
                WallpaperOpacityText.Text = $"{(int)(e.NewValue * 100)}%";
            }
            _settingsService.Save();
            UpdateLivePreview();
            SyncLiveBackground();
        }

        private void WallpaperBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            var s = _settingsService.Current;
            s.BackgroundBlur = e.NewValue;
            if (WallpaperBlurText != null)
            {
                WallpaperBlurText.Text = $"{(int)e.NewValue} px";
            }
            _settingsService.Save();
            UpdateLivePreview();
            SyncLiveBackground();
        }

        private void WallpaperDimmerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            var s = _settingsService.Current;
            s.BackgroundDimmer = e.NewValue;
            if (WallpaperDimmerText != null)
            {
                WallpaperDimmerText.Text = $"{(int)(e.NewValue * 100)}%";
            }
            _settingsService.Save();
            UpdateLivePreview();
            SyncLiveBackground();
        }

        private void FileAreaGlassSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            var s = _settingsService.Current;
            s.FileAreaGlassOpacity = e.NewValue;
            if (FileAreaGlassText != null)
            {
                FileAreaGlassText.Text = $"{(int)(e.NewValue * 100)}%";
            }
            _settingsService.Save();
            UpdateLivePreview();
            SyncLiveBackground();
        }

        private void WallpaperStretch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || WallpaperStretchCombo.SelectedItem is not ComboBoxItem item) return;
            var s = _settingsService.Current;
            s.BackgroundStretch = item.Tag?.ToString() ?? "UniformToFill";
            _settingsService.Save();
            SyncLiveBackground();
        }

        private void UpdateWallpaperCardHighlights(string? path)
        {
            try
            {
                var accent = (Brush)FindResource("AccentBrush");
                var normal = (Brush)FindResource("CardBorderBrush");

                bool isAurora = !string.IsNullOrEmpty(path) && path.IndexOf("cyber_aurora", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPrism = !string.IsNullOrEmpty(path) && path.IndexOf("abstract_prism", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSpace = !string.IsNullOrEmpty(path) && path.IndexOf("deep_space", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isWaves = !string.IsNullOrEmpty(path) && path.IndexOf("minimal_waves", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isNone = string.IsNullOrEmpty(path);
                bool isCustom = !isAurora && !isPrism && !isSpace && !isWaves && !isNone;

                if (PresetCardAurora != null)
                {
                    PresetCardAurora.BorderBrush = isAurora ? accent : normal;
                    PresetCardAurora.BorderThickness = isAurora ? new Thickness(2.0) : new Thickness(1.0);
                }
                if (PresetCardPrism != null)
                {
                    PresetCardPrism.BorderBrush = isPrism ? accent : normal;
                    PresetCardPrism.BorderThickness = isPrism ? new Thickness(2.0) : new Thickness(1.0);
                }
                if (PresetCardSpace != null)
                {
                    PresetCardSpace.BorderBrush = isSpace ? accent : normal;
                    PresetCardSpace.BorderThickness = isSpace ? new Thickness(2.0) : new Thickness(1.0);
                }
                if (PresetCardWaves != null)
                {
                    PresetCardWaves.BorderBrush = isWaves ? accent : normal;
                    PresetCardWaves.BorderThickness = isWaves ? new Thickness(2.0) : new Thickness(1.0);
                }
                if (PresetCardCustom != null)
                {
                    PresetCardCustom.BorderBrush = isCustom ? accent : normal;
                    PresetCardCustom.BorderThickness = isCustom ? new Thickness(2.0) : new Thickness(1.0);
                }
                if (PresetCardNone != null)
                {
                    PresetCardNone.BorderBrush = isNone ? accent : normal;
                    PresetCardNone.BorderThickness = isNone ? new Thickness(2.0) : new Thickness(1.0);
                }

                if (WallpaperFileNameText != null)
                {
                    if (isNone)
                    {
                        WallpaperFileNameText.Text = "None (Pure Solid Theme Active)";
                    }
                    else if (isAurora)
                    {
                        WallpaperFileNameText.Text = "Cyber Aurora · Neon Nebula (Curated 4K)";
                    }
                    else if (isPrism)
                    {
                        WallpaperFileNameText.Text = "Abstract Prism · Prism Glass (Curated 4K)";
                    }
                    else if (isSpace)
                    {
                        WallpaperFileNameText.Text = "Deep Space · Cosmic Stars (Curated 4K)";
                    }
                    else if (isWaves)
                    {
                        WallpaperFileNameText.Text = "Minimal Waves · Gradient Silk (Curated 4K)";
                    }
                    else
                    {
                        WallpaperFileNameText.Text = Path.GetFileName(path) ?? path;
                    }
                }
            }
            catch { }
        }

        private void AccentSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border swatch || swatch.Tag is not string hexColor) return;
            _themeService.ApplyCustomAccent(hexColor);
            UpdateAccentSwatchHighlights(hexColor);
        }

        private void UpdateAccentSwatchHighlights(string? activeHex)
        {
            try
            {
                if (string.IsNullOrEmpty(activeHex)) activeHex = "#6366F1";
                Border[] swatches = { AccentSwatchCyan, AccentSwatchEmerald, AccentSwatchAmber, AccentSwatchRose, AccentSwatchMagenta, AccentSwatchIndigo, AccentSwatchSky, AccentSwatchCoral };

                foreach (var b in swatches)
                {
                    if (b == null) continue;
                    string tag = b.Tag?.ToString() ?? "";
                    bool isMatch = string.Equals(tag, activeHex, StringComparison.OrdinalIgnoreCase);
                    b.BorderThickness = isMatch ? new Thickness(3.0) : new Thickness(1.5);
                    if (isMatch)
                    {
                        b.BorderBrush = Brushes.White;
                        var col = (Color)ColorConverter.ConvertFromString(tag);
                        b.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            BlurRadius = 14,
                            ShadowDepth = 0,
                            Opacity = 0.85,
                            Color = col
                        };
                    }
                    else
                    {
                        b.BorderBrush = (Brush)FindResource("CardBorderBrush");
                        b.Effect = null;
                    }
                }
            }
            catch { }
        }

        private void UpdateLivePreview()
        {
            try
            {
                if (PreviewWallpaperImage == null) return;
                var s = _settingsService.Current;

                if (!string.IsNullOrEmpty(s.CustomBackgroundPath) && File.Exists(s.CustomBackgroundPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(s.CustomBackgroundPath, UriKind.RelativeOrAbsolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    PreviewWallpaperImage.Source = bmp;
                    PreviewWallpaperImage.Opacity = s.BackgroundOpacity;
                    PreviewWallpaperImage.Visibility = Visibility.Visible;
                }
                else
                {
                    PreviewWallpaperImage.Source = null;
                    PreviewWallpaperImage.Visibility = Visibility.Collapsed;
                }

                if (PreviewWallpaperBlur != null)
                {
                    PreviewWallpaperBlur.Radius = s.BackgroundBlur;
                }
                if (PreviewDimmerOverlay != null)
                {
                    PreviewDimmerOverlay.Opacity = string.IsNullOrEmpty(s.CustomBackgroundPath) ? 0 : s.BackgroundDimmer;
                }
                if (PreviewFileAreaGlass != null)
                {
                    PreviewFileAreaGlass.Opacity = string.IsNullOrEmpty(s.CustomBackgroundPath) ? 1.0 : s.FileAreaGlassOpacity;
                }
            }
            catch { }
        }

        private void BrowseCustomStartup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Custom Startup Folder"
            };
            if (dialog.ShowDialog(this) == true)
            {
                _settingsService.Current.CustomStartupPath = dialog.FolderName;
                if (CustomStartupBox != null) CustomStartupBox.Text = dialog.FolderName;
                _settingsService.Save();
            }
        }

        private void BrowseCustomTerminal_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Terminal Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                _settingsService.Current.CustomTerminalPath = dialog.FileName;
                if (CustomTerminalPathBox != null) CustomTerminalPathBox.Text = dialog.FileName;
                _settingsService.Save();
            }
        }

        private void BrowseCustomEditor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Code / Text Editor Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                _settingsService.Current.CustomEditorPath = dialog.FileName;
                if (CustomEditorPathBox != null) CustomEditorPathBox.Text = dialog.FileName;
                _settingsService.Save();
            }
        }

        private void CustomTerminalPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || CustomTerminalPathBox == null) return;
            _settingsService.Current.CustomTerminalPath = CustomTerminalPathBox.Text;
            _settingsService.Save();
        }

        private void CustomizeToolbar_Click(object sender, RoutedEventArgs e)
        {
            var win = new ToolbarCustomizeWindow(_settingsService);
            win.Owner = this;
            win.ShowDialog();
        }

        private void CustomEditorPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || CustomEditorPathBox == null) return;
            _settingsService.Current.CustomEditorPath = CustomEditorPathBox.Text;
            _settingsService.Save();
        }

        private void PreferredTerminal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || PreferredTerminalCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.PreferredTerminal = item.Tag?.ToString() ?? "WindowsTerminal";
            _settingsService.Save();
        }

        private void PreferredEditor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || PreferredEditorCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.PreferredEditorApp = item.Tag?.ToString() ?? "BuiltIn";
            _settingsService.Save();
        }

        private void ArchiveFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ArchiveFormatCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.DefaultArchiveFormat = item.Tag?.ToString() ?? "zip";
            _settingsService.Save();
        }

        private void CompressionLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || CompressionLevelCombo.SelectedItem is not ComboBoxItem item) return;
            _settingsService.Current.CompressionLevel = item.Tag?.ToString() ?? "Optimal";
            _settingsService.Save();
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Rift Vault Configuration",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = "riftvault_settings.json"
            };
            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    _settingsService.Export(dialog.FileName);
                    MessageBox.Show($"Settings successfully exported to:\n{dialog.FileName}", "Export Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export settings: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Rift Vault Configuration",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    if (_settingsService.Import(dialog.FileName))
                    {
                        _isInitializing = true;
                        LoadSettingsIntoUI();
                        _isInitializing = false;
                        _themeService.ApplyTheme(_settingsService.Current.Theme);
                        SyncLiveBackground();
                        MessageBox.Show("Settings imported and applied successfully!", "Import Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Could not read valid settings from the selected JSON file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import settings: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearThumbnailCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _thumbnailCache?.Clear();
                MessageBox.Show("Thumbnail cache has been cleared successfully.", "Thumbnail Cache", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not clear cache: {ex.Message}", "Cache Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "logs");
                Directory.CreateDirectory(logDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open logs folder: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault");
                Directory.CreateDirectory(configDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = configDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings folder: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SyncLiveBackground()
        {
            try
            {
                if (Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    var s = _settingsService.Current;
                    vm.UpdateCustomBackground(s.CustomBackgroundPath, s.BackgroundOpacity, s.BackgroundBlur, s.BackgroundStretch, s.BackgroundDimmer, s.FileAreaGlassOpacity);
                }
            }
            catch { }
        }
        #endregion

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to restore all settings and preferences to default values?",
                "Reset Preferences",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _settingsService.ResetToDefault();
            _isInitializing = true;
            LoadSettingsIntoUI();
            _isInitializing = false;
            _themeService.ApplyTheme(_settingsService.Current.Theme);
            SyncLiveBackground();
            MessageBox.Show("All preferences have been restored to defaults.", "Settings Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
