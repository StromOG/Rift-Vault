using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RiftVault.Models
{
    public partial class TabState : ObservableObject
    {
        [ObservableProperty]
        private string _id = System.Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private string _title = "New Tab";

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private ObservableCollection<string> _history = new();

        [ObservableProperty]
        private int _historyIndex = -1;

        [ObservableProperty]
        private ViewMode _currentViewMode = ViewMode.IconGrid;

        public enum ViewMode
        {
            IconGrid,
            DetailsList,
            MoodBoard,
            Timeline,
            Columns
        }
    }
}