using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RiftVault.Models;
using RiftVault.Services;
using RiftVault.Win32;

namespace RiftVault.UI
{
    public partial class ToolbarCustomizeWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly MainWindow? _mainWindow;
        private Point _dragStartPoint;
        private ToolbarItemViewModel? _draggedItem;

        public ObservableCollection<ToolbarItemViewModel> ActiveItems { get; } = new();
        public ObservableCollection<ToolbarItemViewModel> AvailableItems { get; } = new();

        public ToolbarCustomizeWindow(ISettingsService settingsService, MainWindow? mainWindow = null)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _mainWindow = mainWindow;
            if (mainWindow != null)
            {
                Owner = mainWindow;
            }

            ActiveItemsList.ItemsSource = ActiveItems;
            AvailableItemsList.ItemsSource = AvailableItems;

            SourceInitialized += (s, e) => GlassHelper.EnableRoundedCorners(this);
            Loaded += (s, e) =>
            {
                AnimationHelper.ApplyWindowCorners(this, ToolbarRootBorder, _settingsService.Current.WindowCornerRadius);
                AnimationHelper.ApplyWindowEntrance(this, ToolbarRootBorder, _settingsService.Current);
            };

            LoadToolbarData();
        }

        private bool _isClosingAnimated = false;
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosingAnimated && _settingsService.Current.EnableAnimations && _settingsService.Current.WindowEntranceAnimation != "Instant")
            {
                e.Cancel = true;
                _isClosingAnimated = true;
                AnimationHelper.ApplyWindowExit(this, ToolbarRootBorder, _settingsService.Current, () =>
                {
                    Close();
                });
                return;
            }
            base.OnClosing(e);
        }

        private void LoadToolbarData()
        {
            ActiveItems.Clear();
            AvailableItems.Clear();

            var currentList = _settingsService.Current.ToolbarBlocks;
            if (currentList == null || !currentList.Any(b => b.IsVisible))
            {
                currentList = AppSettings.GetDefaultToolbarBlocks();
            }
            var active = currentList.Where(b => b.IsVisible).OrderBy(b => b.OrderIndex).ToList();
            var available = currentList.Where(b => !b.IsVisible).OrderBy(b => b.Title).ToList();

            int idx = 1;
            foreach (var b in active)
            {
                ActiveItems.Add(new ToolbarItemViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    IconGlyph = b.IconGlyph,
                    Description = b.Description,
                    IsVisible = true,
                    OrderIndex = idx - 1,
                    DisplayIndex = idx++
                });
            }

            foreach (var b in available)
            {
                AvailableItems.Add(new ToolbarItemViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    IconGlyph = b.IconGlyph,
                    Description = b.Description,
                    IsVisible = false,
                    OrderIndex = 999,
                    DisplayIndex = 0
                });
            }

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            for (int i = 0; i < ActiveItems.Count; i++)
            {
                ActiveItems[i].DisplayIndex = i + 1;
                ActiveItems[i].OrderIndex = i;
            }

            ActiveCountText.Text = $"{ActiveItems.Count} items";
            AvailableCountText.Text = $"{AvailableItems.Count} items";
        }

        private void LiveUpdateMainWindow()
        {
            SaveToSettingsModel();
            _mainWindow?.ApplyToolbarLayout();
        }

        private void SaveToSettingsModel()
        {
            var combined = new List<ToolbarBlockSetting>();
            for (int i = 0; i < ActiveItems.Count; i++)
            {
                var item = ActiveItems[i];
                combined.Add(new ToolbarBlockSetting
                {
                    Id = item.Id,
                    Title = item.Title,
                    IconGlyph = item.IconGlyph,
                    Description = item.Description,
                    IsVisible = true,
                    OrderIndex = i
                });
            }

            for (int i = 0; i < AvailableItems.Count; i++)
            {
                var item = AvailableItems[i];
                combined.Add(new ToolbarBlockSetting
                {
                    Id = item.Id,
                    Title = item.Title,
                    IconGlyph = item.IconGlyph,
                    Description = item.Description,
                    IsVisible = false,
                    OrderIndex = ActiveItems.Count + i
                });
            }

            _settingsService.Current.ToolbarBlocks = combined;
        }

        // ─── Actions: Move, Add, Remove ───────────────────────────────

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToolbarItemViewModel item)
            {
                int idx = ActiveItems.IndexOf(item);
                if (idx > 0)
                {
                    ActiveItems.Move(idx, idx - 1);
                    UpdateCounts();
                    LiveUpdateMainWindow();
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToolbarItemViewModel item)
            {
                int idx = ActiveItems.IndexOf(item);
                if (idx >= 0 && idx < ActiveItems.Count - 1)
                {
                    ActiveItems.Move(idx, idx + 1);
                    UpdateCounts();
                    LiveUpdateMainWindow();
                }
            }
        }

        private void RemoveBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToolbarItemViewModel item)
            {
                ActiveItems.Remove(item);
                item.IsVisible = false;
                item.DisplayIndex = 0;
                AvailableItems.Add(item);
                UpdateCounts();
                LiveUpdateMainWindow();
            }
        }

        private void AddBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToolbarItemViewModel item)
            {
                AvailableItems.Remove(item);
                item.IsVisible = true;
                ActiveItems.Add(item);
                UpdateCounts();
                LiveUpdateMainWindow();
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Current.ToolbarBlocks = AppSettings.GetDefaultToolbarBlocks();
            LoadToolbarData();
            LiveUpdateMainWindow();
        }

        private void ApplyAndClose_Click(object sender, RoutedEventArgs e)
        {
            SaveToSettingsModel();
            _settingsService.Save();
            _mainWindow?.ApplyToolbarLayout();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ─── Drag & Drop Reordering ───────────────────────────────────

        private void ActiveItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Do not initiate drag if clicking action buttons
            if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null)
                return;

            _dragStartPoint = e.GetPosition(null);
            if (sender is FrameworkElement elem && elem.DataContext is ToolbarItemViewModel vm)
            {
                _draggedItem = vm;
            }
        }

        private void ActiveItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point currentPos = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement elem)
                    {
                        DragDrop.DoDragDrop(elem, _draggedItem, DragDropEffects.Move);
                        _draggedItem = null;
                    }
                }
            }
        }

        private void AvailableItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null)
                return;

            _dragStartPoint = e.GetPosition(null);
            if (sender is FrameworkElement elem && elem.DataContext is ToolbarItemViewModel vm)
            {
                _draggedItem = vm;
            }
        }

        private void AvailableItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point currentPos = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement elem)
                    {
                        DragDrop.DoDragDrop(elem, _draggedItem, DragDropEffects.Move);
                        _draggedItem = null;
                    }
                }
            }
        }

        private void ActiveList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ToolbarItemViewModel)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void ActiveItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ToolbarItemViewModel)) is ToolbarItemViewModel dropped)
            {
                if (sender is FrameworkElement targetElem && targetElem.DataContext is ToolbarItemViewModel targetItem)
                {
                    if (ActiveItems.Contains(dropped))
                    {
                        int oldIndex = ActiveItems.IndexOf(dropped);
                        int newIndex = ActiveItems.IndexOf(targetItem);
                        if (oldIndex != newIndex && newIndex >= 0)
                        {
                            ActiveItems.Move(oldIndex, newIndex);
                            UpdateCounts();
                            LiveUpdateMainWindow();
                        }
                    }
                    else if (AvailableItems.Contains(dropped))
                    {
                        AvailableItems.Remove(dropped);
                        dropped.IsVisible = true;
                        int newIndex = ActiveItems.IndexOf(targetItem);
                        ActiveItems.Insert(Math.Max(0, newIndex), dropped);
                        UpdateCounts();
                        LiveUpdateMainWindow();
                    }
                }
                e.Handled = true;
            }
        }

        private void ActiveList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ToolbarItemViewModel)) is ToolbarItemViewModel dropped)
            {
                if (AvailableItems.Contains(dropped))
                {
                    AvailableItems.Remove(dropped);
                    dropped.IsVisible = true;
                    ActiveItems.Add(dropped);
                    UpdateCounts();
                    LiveUpdateMainWindow();
                }
                e.Handled = true;
            }
        }

        private void PresetDefault_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new[] { "navigation", "breadcrumb", "appmodes", "aiassistant", "overflow" });
        }

        private void PresetCompact_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new[] { "navigation", "breadcrumb", "overflow" });
        }

        private void PresetPowerUser_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new[] { "navigation", "breadcrumb", "actionstrip", "features", "appmodes", "aiassistant", "overflow" });
        }

        private void PresetMedia_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new[] { "navigation", "breadcrumb", "appmodes", "viewmode", "sort", "overflow" });
        }

        private void PresetDev_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(new[] { "navigation", "breadcrumb", "actionstrip", "terminal", "features", "aiassistant", "overflow" });
        }

        private void ApplyPreset(string[] activeIds)
        {
            var allItems = ActiveItems.Concat(AvailableItems).ToList();
            ActiveItems.Clear();
            AvailableItems.Clear();

            int order = 0;
            foreach (var id in activeIds)
            {
                var match = allItems.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.IsVisible = true;
                    match.OrderIndex = order;
                    match.DisplayIndex = ++order;
                    ActiveItems.Add(match);
                }
            }

            foreach (var item in allItems)
            {
                if (!activeIds.Any(id => id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    item.IsVisible = false;
                    item.OrderIndex = 999;
                    item.DisplayIndex = 0;
                    AvailableItems.Add(item);
                }
            }

            UpdateCounts();
            LiveUpdateMainWindow();
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typed) return typed;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }

    public class ToolbarItemViewModel : INotifyPropertyChanged
    {
        private int _displayIndex;
        private bool _isVisible;

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE700";
        public string Description { get; set; } = string.Empty;
        public int OrderIndex { get; set; }

        public int DisplayIndex
        {
            get => _displayIndex;
            set { _displayIndex = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public Visibility IsFlexibleBadgeVisible => (Id == "address") ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
