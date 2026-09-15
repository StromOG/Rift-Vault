using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.Models;
using RiftVault.Services;

namespace RiftVault.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;

        [ObservableProperty]
        private AppSettings _settings;

        public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
        {
            _settingsService = settingsService;
            _themeService = themeService;
            _settings = _settingsService.Current;

            SaveCommand = new RelayCommand(SaveSettings);
            ResetCommand = new RelayCommand(ResetSettings);
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        private void SaveSettings()
        {
            _settingsService.Save();
            _themeService.ApplyTheme(Settings.Theme);
            // Other services would be notified of changes here
        }

        private void ResetSettings()
        {
            _settingsService.ResetToDefault();
            Settings = _settingsService.Current;
            SaveSettings();
        }
    }
}