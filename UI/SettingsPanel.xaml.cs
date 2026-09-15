using System.Windows;
using System.Windows.Controls;
using RiftVault.ViewModels;

namespace RiftVault.UI
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
        }

        private MainViewModel? GetMainVM()
        {
            var window = Window.GetWindow(this);
            return window?.DataContext as MainViewModel;
        }

        private void ThemeObsidian_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GetMainVM()?.ApplyTheme("ObsidianAurora");
        }

        private void ThemeSolar_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GetMainVM()?.ApplyTheme("SolarFusion");
        }

        private void ThemeArctic_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GetMainVM()?.ApplyTheme("ArcticMist");
        }
    }
}
