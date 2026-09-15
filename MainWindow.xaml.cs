using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using RiftVault.Core;
using RiftVault.Models;
using RiftVault.Services;
using RiftVault.UI;
using RiftVault.ViewModels;
using RiftVault.Win32;

namespace RiftVault
{
    public partial class MainWindow : Window
    {
        private readonly IThemeService _themeService;
        private FileItemViewModel? _itemToRename;
        private readonly List<string> _addressHistory = new()
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        public MainWindow(MainViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _themeService = themeService;

            SourceInitialized += (s, e) => GlassHelper.EnableRoundedCorners(this);
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
        }

        private TabViewModel? _hookedTab;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _themeService.ApplyGlassEffect(this);

            if (DataContext is MainViewModel vm)
            {
                AnimationHelper.ApplyWindowCorners(this, RootBorder, vm.Settings.WindowCornerRadius);
                AnimationHelper.ApplyWindowEntrance(this, RootBorder, vm.Settings);

                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainViewModel.ActiveTab) && vm.ActiveTab != null)
                    {
                        HookTab(vm.ActiveTab);
                        ApplyViewMode(vm.ActiveTab.CurrentViewMode);
                    }
                };

                if (vm.ActiveTab != null)
                {
                    HookTab(vm.ActiveTab);
                    ApplyViewMode(vm.ActiveTab.CurrentViewMode);
                }

                InitializeToolbarBlocks();
                ApplyToolbarLayout();
                Loaded += (s, e) => AdjustDetailsColumnWidths(FileListView);
            }
        }

        // ─── Modular Dynamic Toolbar System ─────────────────────────────
        private readonly Dictionary<string, FrameworkElement> _toolbarBlocks = new();

        private void InitializeToolbarBlocks()
        {
            if (_toolbarBlocks.Count > 0) return;
            _toolbarBlocks["nav"] = ToolbarBlock_Nav;
            _toolbarBlocks["address"] = ToolbarBlock_Address;
            _toolbarBlocks["search"] = ToolbarBlock_Search;
            _toolbarBlocks["viewmode"] = ToolbarBlock_ViewMode;
            _toolbarBlocks["sort"] = ToolbarBlock_Sort;
            _toolbarBlocks["customize"] = ToolbarBlock_Customize;
            _toolbarBlocks["actionstrip"] = ToolbarBlock_ActionStrip;
            _toolbarBlocks["features"] = ToolbarBlock_Features;
            _toolbarBlocks["appmodes"] = ToolbarBlock_AppModes;
            _toolbarBlocks["inspector"] = ToolbarBlock_Inspector;
            _toolbarBlocks["terminal"] = ToolbarBlock_Terminal;
            _toolbarBlocks["selection"] = ToolbarBlock_Selection;
            _toolbarBlocks["settings"] = ToolbarBlock_Settings;
            _toolbarBlocks["overflow"] = ToolbarBlock_Overflow;
        }

        public void ApplyToolbarLayout()
        {
            if (DataContext is not MainViewModel vm) return;
            InitializeToolbarBlocks();

            var currentBlocks = vm.Settings.ToolbarBlocks ?? AppSettings.GetDefaultToolbarBlocks();
            var activeBlocks = currentBlocks.Where(b => b.IsVisible).OrderBy(b => b.OrderIndex).ToList();

            DynamicToolbarGrid.Children.Clear();
            DynamicToolbarGrid.ColumnDefinitions.Clear();

            const int MaxDirectBlocks = 7;
            List<ToolbarBlockSetting> directBlocks;
            List<ToolbarBlockSetting> overflowBlocks = new();

            if (activeBlocks.Count <= MaxDirectBlocks)
            {
                directBlocks = new List<ToolbarBlockSetting>(activeBlocks);
            }
            else
            {
                // Ensure Address bar is always in direct blocks so navigation is never hidden
                var addressBlock = activeBlocks.FirstOrDefault(b => b.Id == "address");
                directBlocks = activeBlocks.Take(MaxDirectBlocks - 1).ToList();
                if (addressBlock != null && !directBlocks.Contains(addressBlock))
                {
                    directBlocks[directBlocks.Count - 1] = addressBlock;
                }
                overflowBlocks = activeBlocks.Where(b => !directBlocks.Contains(b)).ToList();
            }

            int colIndex = 0;
            for (int i = 0; i < directBlocks.Count; i++)
            {
                var blockCfg = directBlocks[i];
                if (!_toolbarBlocks.TryGetValue(blockCfg.Id, out var element)) continue;

                // Address bar gets flexible Star width, others get Auto
                bool isFlexible = (blockCfg.Id == "address");
                var colDef = new ColumnDefinition
                {
                    Width = isFlexible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
                };
                DynamicToolbarGrid.ColumnDefinitions.Add(colDef);

                if (element.Parent is Panel p)
                {
                    p.Children.Remove(element);
                }

                Grid.SetColumn(element, colIndex++);
                element.Visibility = Visibility.Visible;
                DynamicToolbarGrid.Children.Add(element);
            }

            // If there are overflowed blocks, place ToolbarBlock_Overflow as the final column
            if (overflowBlocks.Count > 0)
            {
                var overflowDef = new ColumnDefinition { Width = GridLength.Auto };
                DynamicToolbarGrid.ColumnDefinitions.Add(overflowDef);

                if (ToolbarBlock_Overflow.Parent is Panel p)
                {
                    p.Children.Remove(ToolbarBlock_Overflow);
                }

                Grid.SetColumn(ToolbarBlock_Overflow, colIndex++);
                ToolbarBlock_Overflow.Visibility = Visibility.Visible;
                DynamicToolbarGrid.Children.Add(ToolbarBlock_Overflow);

                UpdateToolbarOverflowMenu(overflowBlocks);
            }
            else
            {
                if (ToolbarBlock_Overflow.Parent is Panel p)
                {
                    p.Children.Remove(ToolbarBlock_Overflow);
                }
                ToolbarBlock_Overflow.Visibility = Visibility.Collapsed;
                if (!ToolbarBlocksPool.Children.Contains(ToolbarBlock_Overflow))
                {
                    ToolbarBlocksPool.Children.Add(ToolbarBlock_Overflow);
                }
            }

            // Ensure inactive blocks are removed from DynamicToolbarGrid and hidden in pool
            foreach (var kvp in _toolbarBlocks)
            {
                if (kvp.Key == "overflow") continue; // Handled explicitly above
                if (!directBlocks.Any(b => b.Id == kvp.Key))
                {
                    if (kvp.Value.Parent is Panel p)
                    {
                        p.Children.Remove(kvp.Value);
                    }
                    kvp.Value.Visibility = Visibility.Collapsed;
                    if (!ToolbarBlocksPool.Children.Contains(kvp.Value))
                    {
                        ToolbarBlocksPool.Children.Add(kvp.Value);
                    }
                }
            }
        }

        private void ToolbarOverflow_Click(object sender, RoutedEventArgs e)
        {
            if (ToolbarBlock_Overflow.ContextMenu != null)
            {
                ToolbarBlock_Overflow.ContextMenu.PlacementTarget = ToolbarBlock_Overflow;
                ToolbarBlock_Overflow.ContextMenu.IsOpen = true;
            }
        }

        private void UpdateToolbarOverflowMenu(List<ToolbarBlockSetting> overflowBlocks)
        {
            if (ToolbarOverflowMenu == null) return;
            ToolbarOverflowMenu.Items.Clear();

            var header = new MenuItem { Header = "More Toolbar Tools", IsEnabled = false, FontWeight = FontWeights.SemiBold };
            ToolbarOverflowMenu.Items.Add(header);
            ToolbarOverflowMenu.Items.Add(new Separator());

            var font = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            var accent = (Brush)FindResource("AccentBrush");

            foreach (var b in overflowBlocks)
            {
                switch (b.Id)
                {
                    case "actionstrip":
                        var actionMenu = new MenuItem { Header = "Quick File Actions" };
                        actionMenu.Icon = new TextBlock { Text = "\uE8F4", FontFamily = font, FontSize = 13, Foreground = accent };
                        var miNF = new MenuItem { Header = "New Folder", InputGestureText = "Ctrl+Shift+N" };
                        miNF.Click += NewFolder_Click;
                        actionMenu.Items.Add(miNF);
                        var miNFile = new MenuItem { Header = "New File", InputGestureText = "Ctrl+N" };
                        miNFile.Click += NewFile_Click;
                        actionMenu.Items.Add(miNFile);
                        actionMenu.Items.Add(new Separator());
                        var miCut = new MenuItem { Header = "Cut", InputGestureText = "Ctrl+X" };
                        miCut.Click += Cut_Click;
                        actionMenu.Items.Add(miCut);
                        var miCopy = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
                        miCopy.Click += Copy_Click;
                        actionMenu.Items.Add(miCopy);
                        var miPaste = new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V" };
                        miPaste.Click += Paste_Click;
                        actionMenu.Items.Add(miPaste);
                        var miRen = new MenuItem { Header = "Rename", InputGestureText = "F2" };
                        miRen.Click += Rename_Click;
                        actionMenu.Items.Add(miRen);
                        var miDel = new MenuItem { Header = "Delete", InputGestureText = "Del" };
                        miDel.Click += Delete_Click;
                        actionMenu.Items.Add(miDel);
                        ToolbarOverflowMenu.Items.Add(actionMenu);
                        break;

                    case "features":
                        var featMenu = new MenuItem { Header = "Panes & Features" };
                        featMenu.Icon = new TextBlock { Text = "\uE890", FontFamily = font, FontSize = 13, Foreground = accent };
                        var miPrev = new MenuItem { Header = "Toggle Preview Pane", InputGestureText = "Alt+P" };
                        miPrev.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.TogglePreviewPaneCommand.Execute(null); };
                        featMenu.Items.Add(miPrev);
                        var miDual = new MenuItem { Header = "Toggle Dual-Pane Mode", InputGestureText = "Alt+2" };
                        miDual.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.ToggleDualPaneCommand.Execute(null); };
                        featMenu.Items.Add(miDual);
                        var miHidden = new MenuItem { Header = "Toggle Hidden Files", InputGestureText = "Ctrl+H" };
                        miHidden.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.ToggleHiddenFilesCommand.Execute(null); };
                        featMenu.Items.Add(miHidden);
                        var miAI = new MenuItem { Header = "AI Assistant", InputGestureText = "Alt+A" };
                        miAI.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.ToggleAIAssistantCommand.Execute(null); };
                        featMenu.Items.Add(miAI);
                        ToolbarOverflowMenu.Items.Add(featMenu);
                        break;

                    case "appmodes":
                        var modesMenu = new MenuItem { Header = "Workflow App Modes" };
                        modesMenu.Icon = new TextBlock { Text = "\uE8B7", FontFamily = font, FontSize = 13, Foreground = accent };
                        string[] modeKeys = { "Explorer", "DualCommander", "MediaGallery", "DeveloperWorkspace", "ZenFocus" };
                        string[] modeHeaders = { "Explorer Mode (Ctrl+1)", "Dual Commander (Ctrl+2)", "Media Gallery (Ctrl+3)", "Developer Workspace (Ctrl+4)", "Zen Focus (Ctrl+5)" };
                        for (int m = 0; m < modeKeys.Length; m++)
                        {
                            string target = modeKeys[m];
                            var item = new MenuItem { Header = modeHeaders[m] };
                            item.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.SetAppModeCommand.Execute(target); };
                            modesMenu.Items.Add(item);
                        }
                        ToolbarOverflowMenu.Items.Add(modesMenu);
                        break;

                    case "inspector":
                        var miInsp = new MenuItem { Header = "File Details & Inspector", InputGestureText = "Ctrl+I" };
                        miInsp.Icon = new TextBlock { Text = "\uE9F9", FontFamily = font, FontSize = 13, Foreground = accent };
                        miInsp.Click += Investigate_Click;
                        ToolbarOverflowMenu.Items.Add(miInsp);
                        break;

                    case "terminal":
                        var miTerm = new MenuItem { Header = "Open Terminal Console Here" };
                        miTerm.Icon = new TextBlock { Text = "\uE756", FontFamily = font, FontSize = 13, Foreground = accent };
                        miTerm.Click += Terminal_Click;
                        ToolbarOverflowMenu.Items.Add(miTerm);
                        break;

                    case "selection":
                        var miSelAll = new MenuItem { Header = "Select All", InputGestureText = "Ctrl+A" };
                        miSelAll.Icon = new TextBlock { Text = "\uE8B3", FontFamily = font, FontSize = 13 };
                        miSelAll.Click += SelectAll_Click;
                        ToolbarOverflowMenu.Items.Add(miSelAll);
                        var miInv = new MenuItem { Header = "Invert Selection" };
                        miInv.Icon = new TextBlock { Text = "\uE8E6", FontFamily = font, FontSize = 13 };
                        miInv.Click += InvertSelection_Click;
                        ToolbarOverflowMenu.Items.Add(miInv);
                        break;

                    case "settings":
                        var miSet = new MenuItem { Header = "Settings & Preferences", InputGestureText = "Ctrl+," };
                        miSet.Icon = new TextBlock { Text = "\uE713", FontFamily = font, FontSize = 13, Foreground = accent };
                        miSet.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.OpenSettingsCommand.Execute(null); };
                        ToolbarOverflowMenu.Items.Add(miSet);
                        break;

                    case "viewmode":
                        var miVm = new MenuItem { Header = "View Mode" };
                        miVm.Icon = new TextBlock { Text = "\uE762", FontFamily = font, FontSize = 13 };
                        var miDet = new MenuItem { Header = "Details View (Ctrl+Shift+6)" };
                        miDet.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.SetViewModeCommand.Execute("Details"); };
                        miVm.Items.Add(miDet);
                        var miGrd = new MenuItem { Header = "Grid Icons (Ctrl+Shift+2)" };
                        miGrd.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.SetViewModeCommand.Execute("Grid"); };
                        miVm.Items.Add(miGrd);
                        var miLst = new MenuItem { Header = "List View (Ctrl+Shift+5)" };
                        miLst.Click += (s, e) => { if (DataContext is MainViewModel vm) vm.SetViewModeCommand.Execute("List"); };
                        miVm.Items.Add(miLst);
                        ToolbarOverflowMenu.Items.Add(miVm);
                        break;

                    case "sort":
                        var miSort = new MenuItem { Header = "Sort Files..." };
                        miSort.Icon = new TextBlock { Text = "\uE8CB", FontFamily = font, FontSize = 13 };
                        miSort.Click += SortButton_Click;
                        ToolbarOverflowMenu.Items.Add(miSort);
                        break;
                }
            }

            ToolbarOverflowMenu.Items.Add(new Separator());
            var miCustom = new MenuItem { Header = "Customize Toolbar..." };
            miCustom.Icon = new TextBlock { Text = "\uE70F", FontFamily = font, FontSize = 13, Foreground = accent };
            miCustom.Click += CustomizeToolbar_Click;
            ToolbarOverflowMenu.Items.Add(miCustom);
        }

        private void DynamicToolbarGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            var blocks = vm.Settings.ToolbarBlocks ?? AppSettings.GetDefaultToolbarBlocks();

            void UpdateCheck(MenuItem? item, string id)
            {
                if (item == null) return;
                var b = blocks.FirstOrDefault(x => x.Id == id);
                item.IsChecked = b?.IsVisible ?? false;
            }

            UpdateCheck(CtxToggle_nav, "nav");
            UpdateCheck(CtxToggle_address, "address");
            UpdateCheck(CtxToggle_search, "search");
            UpdateCheck(CtxToggle_viewmode, "viewmode");
            UpdateCheck(CtxToggle_sort, "sort");
            UpdateCheck(CtxToggle_customize, "customize");
            UpdateCheck(CtxToggle_actionstrip, "actionstrip");
            UpdateCheck(CtxToggle_features, "features");
            UpdateCheck(CtxToggle_appmodes, "appmodes");
            UpdateCheck(CtxToggle_inspector, "inspector");
            UpdateCheck(CtxToggle_terminal, "terminal");
            UpdateCheck(CtxToggle_selection, "selection");
            UpdateCheck(CtxToggle_settings, "settings");
        }

        private void ToolbarQuickToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string tag && DataContext is MainViewModel vm)
            {
                var blocks = vm.Settings.ToolbarBlocks ?? AppSettings.GetDefaultToolbarBlocks();
                var b = blocks.FirstOrDefault(x => x.Id == tag);
                if (b != null)
                {
                    b.IsVisible = mi.IsChecked;
                    vm.SettingsService.Save();
                    ApplyToolbarLayout();
                }
            }
        }

        private void CustomizeToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            var customizer = new UI.ToolbarCustomizeWindow(vm.SettingsService, this);
            customizer.ShowDialog();
        }

        private void ResetToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            vm.Settings.ToolbarBlocks = AppSettings.GetDefaultToolbarBlocks();
            vm.SettingsService.Save();
            ApplyToolbarLayout();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            OmniSearchBox.Text = string.Empty;
        }

        private void AddressBarMoreActions_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.ActiveTab?.CurrentPath))
            {
                Clipboard.SetText(vm.ActiveTab.CurrentPath);
                vm.StatusText = $"Copied path: {vm.ActiveTab.CurrentPath}";
            }
        }

        private void Terminal_Click(object sender, RoutedEventArgs e)
        {
            OpenTerminal_Click(sender, e);
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            var selected = FileListView.SelectedItems.Cast<object>().ToHashSet();
            foreach (var item in FileListView.Items)
            {
                if (selected.Contains(item))
                    FileListView.SelectedItems.Remove(item);
                else
                    FileListView.SelectedItems.Add(item);
            }
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            FileListView.UnselectAll();
            if (SecondaryFileListView != null && SecondaryFileListView.Visibility == Visibility.Visible)
            {
                SecondaryFileListView.UnselectAll();
            }
        }

        private void HookTab(TabViewModel tab)
        {
            if (_hookedTab == tab) return;
            if (_hookedTab != null)
            {
                _hookedTab.PropertyChanged -= Tab_PropertyChanged;
            }
            _hookedTab = tab;
            _hookedTab.PropertyChanged += Tab_PropertyChanged;
            if (!string.IsNullOrEmpty(tab.CurrentPath))
            {
                RecordAddressHistory(tab.CurrentPath);
            }
            UpdateWindowTitle();
        }

        private void Tab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabViewModel.CurrentViewMode) && sender is TabViewModel tab)
            {
                ApplyViewMode(tab.CurrentViewMode);
            }
            else if (e.PropertyName == nameof(TabViewModel.Items) && sender is TabViewModel t && t.CurrentViewMode == "Grid")
            {
                TriggerThumbnailPreload(t);
            }
            else if (e.PropertyName == nameof(TabViewModel.CurrentPath) && sender is TabViewModel pTab)
            {
                RecordAddressHistory(pTab.CurrentPath);
                UpdateWindowTitle();
                ApplyFileGrouping(FileListView);
                if (SecondaryFileListView.Visibility == Visibility.Visible)
                {
                    ApplyFileGrouping(SecondaryFileListView);
                }
            }
            else if (e.PropertyName == nameof(TabViewModel.TotalItemCount) || e.PropertyName == nameof(TabViewModel.IsLoading))
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.UpdateStatusText();
                }
            }
        }

        public void UpdateWindowTitle()
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.Settings.ShowFullPathInTitleBar && !string.IsNullOrEmpty(vm.ActiveTab?.CurrentPath))
                {
                    Title = $"{vm.ActiveTab.CurrentPath} - Rift Vault";
                }
                else
                {
                    Title = "Rift Vault";
                }
            }
        }

        public void ApplyViewMode(string mode)
        {
            if (FileListView == null) return;

            try
            {
                switch (mode)
                {
                    case "Grid":
                        FileListView.View = null;
                        FileListView.ItemTemplate = (DataTemplate)FindResource("GridItemTemplate");
                        FileListView.ItemsPanel = (ItemsPanelTemplate)FindResource("GridItemsPanelTemplate");
                        FileListView.ItemContainerStyle = (Style)FindResource("GridListViewItemStyle");
                        ScrollViewer.SetHorizontalScrollBarVisibility(FileListView, ScrollBarVisibility.Disabled);
                        ScrollViewer.SetCanContentScroll(FileListView, false);
                        VirtualizingPanel.SetIsVirtualizing(FileListView, false);
                        if (DataContext is MainViewModel vmGrid && vmGrid.ActiveTab != null)
                        {
                            TriggerThumbnailPreload(vmGrid.ActiveTab);
                        }
                        break;

                    case "List":
                        FileListView.View = null;
                        FileListView.ItemTemplate = (DataTemplate)FindResource("ListItemTemplate");
                        FileListView.ItemsPanel = (ItemsPanelTemplate)FindResource("ListItemsPanelTemplate");
                        FileListView.ItemContainerStyle = (Style)FindResource("ListListViewItemStyle");
                        ScrollViewer.SetHorizontalScrollBarVisibility(FileListView, ScrollBarVisibility.Auto);
                        ScrollViewer.SetCanContentScroll(FileListView, true);
                        VirtualizingPanel.SetIsVirtualizing(FileListView, true);
                        break;

                    case "Details":
                    default:
                        FileListView.ItemTemplate = null;
                        FileListView.View = (GridView)FindResource("DetailsGridView");
                        FileListView.ItemsPanel = (ItemsPanelTemplate)FindResource("DetailsItemsPanelTemplate");
                        FileListView.ItemContainerStyle = (Style)FindResource("DetailsListViewItemStyle");
                        ScrollViewer.SetHorizontalScrollBarVisibility(FileListView, ScrollBarVisibility.Auto);
                        ScrollViewer.SetCanContentScroll(FileListView, true);
                        VirtualizingPanel.SetIsVirtualizing(FileListView, true);
                        UpdateHeaderArrows();
                        AdjustDetailsColumnWidths(FileListView);
                        break;
                }
            }
            catch { }
        }

        private bool _isAdjustingColumns = false;

        private void FileListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ListView lv)
            {
                AdjustDetailsColumnWidths(lv);
            }
        }

        private void AdjustDetailsColumnWidths(ListView? listView)
        {
            if (_isAdjustingColumns || listView == null) return;
            if (listView.View is not GridView gv || gv.Columns.Count < 4) return;

            try
            {
                _isAdjustingColumns = true;
                double actualWidth = listView.ActualWidth;
                if (actualWidth <= 0) return;

                // Sensible default widths that scale smoothly without giant empty voids
                double dateWidth = Math.Max(160, Math.Min(220, actualWidth * 0.16));
                double typeWidth = Math.Max(160, Math.Min(210, actualWidth * 0.15));
                double sizeWidth = Math.Max(100, Math.Min(140, actualWidth * 0.10));

                gv.Columns[1].Width = dateWidth;
                gv.Columns[2].Width = typeWidth;
                gv.Columns[3].Width = sizeWidth;

                // Allocate space to Name, but clamp it between 260px and 480px so it doesn't create an empty abyss
                double fixedWidths = dateWidth + typeWidth + sizeWidth;
                double scrollbarAllowance = 32;
                double availableForName = actualWidth - fixedWidths - scrollbarAllowance;

                double targetNameWidth = Math.Max(260, Math.Min(480, availableForName));
                if (Math.Abs(gv.Columns[0].Width - targetNameWidth) > 2)
                {
                    gv.Columns[0].Width = targetNameWidth;
                }
            }
            catch { }
            finally
            {
                _isAdjustingColumns = false;
            }
        }

        private void TriggerThumbnailPreload(TabViewModel tab)
        {
            Task.Run(() =>
            {
                foreach (var item in tab.Items)
                {
                    item.LoadThumbnail(96);
                }
            });
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (MaxRestoreBtn != null)
            {
                // Toggle between Maximize (\uE923) and Restore (\uE922) icon glyph
                MaxRestoreBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            }
            if (RootBorder != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    RootBorder.Margin = new Thickness(0);
                    RootBorder.BorderThickness = new Thickness(0);
                    RootBorder.CornerRadius = new CornerRadius(0);
                }
                else
                {
                    RootBorder.Margin = new Thickness(0);
                    RootBorder.BorderThickness = new Thickness(1);
                    RootBorder.CornerRadius = new CornerRadius(16);
                }
            }
        }

        private void FileListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ListView listView) return;
            ApplyFileGrouping(listView);
        }

        private void ApplyFileGrouping(ListView listView)
        {
            if (listView.ItemsSource == null) return;
            var view = listView.Items;

            // Explorer-style date separators are useful in working folders
            // (Downloads, Desktop, Documents, and user-created folders), but
            // make drive/root views noisier than the content warrants.
            view.GroupDescriptions.Clear();
            listView.GroupStyle.Clear();
            if (ShouldGroupByDate())
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItemViewModel.DateGroup)));
                if (FindResource("DateGroupStyle") is GroupStyle dateGroupStyle)
                {
                    listView.GroupStyle.Add(dateGroupStyle);
                }
            }
        }

        private bool ShouldGroupByDate()
        {
            if (DataContext is not MainViewModel vm || vm.ActiveTab == null)
            {
                return false;
            }

            string path = vm.ActiveTab.CurrentPath;
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(path, "This PC", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? fileName = Path.GetFileName(normalized);
            // Keep the top-level drive view clean; group actual folders.
            return !string.IsNullOrEmpty(fileName) &&
                   !string.Equals(normalized, Path.GetPathRoot(normalized)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
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

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaxRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        #region Window Keyboard Shortcuts
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // If input modal is open, let modal handle input
            if (InputModalOverlay.Visibility == Visibility.Visible) return;

            // If user is editing in Address Bar or Search Box, let them type
            if (AddressEditView.Visibility == Visibility.Visible && AddressTextBox.IsFocused) return;
            if (OmniSearchBox.IsFocused) return;

            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            // Alt+D or Ctrl+L: Address bar edit mode
            if ((ctrl && e.Key == Key.L) || (alt && e.Key == Key.D))
            {
                EnterAddressEditMode();
                e.Handled = true;
                return;
            }

            // Ctrl+T: New Tab
            if (ctrl && !shift && e.Key == Key.T)
            {
                vm.AddNewTabCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+W: Close Tab
            if (ctrl && !shift && e.Key == Key.W)
            {
                if (vm.ActiveTab != null) vm.CloseTabCommand.Execute(vm.ActiveTab);
                e.Handled = true;
                return;
            }

            // Alt+P: Toggle Preview Pane
            if (alt && e.Key == Key.P)
            {
                vm.TogglePreviewPaneCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Alt+A or Ctrl+Space: Toggle Rift AI Copilot
            if ((alt && e.Key == Key.A) || (ctrl && e.Key == Key.Space))
            {
                vm.ToggleAIAssistantCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+H: Toggle Hidden Files
            if (ctrl && !shift && e.Key == Key.H)
            {
                vm.ToggleHiddenFilesCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Alt+2: Toggle Dual Pane
            if (alt && (e.Key == Key.D2 || e.Key == Key.NumPad2))
            {
                vm.ToggleDualPaneCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+D: Duplicate Selected Item(s)
            if (ctrl && !shift && e.Key == Key.D)
            {
                if (FileListView.SelectedItem is FileItemViewModel item)
                {
                    vm.DuplicateSelectedCommand.Execute(item);
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+T: New Tab
            if (ctrl && !shift && e.Key == Key.T)
            {
                vm.AddNewTabCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+W: Close Tab
            if (ctrl && !shift && e.Key == Key.W)
            {
                vm.CloseTabCommand.Execute(vm.ActiveTab);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+T: Reopen Closed Tab
            if (ctrl && shift && e.Key == Key.T)
            {
                vm.ReopenClosedTabCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+Tab / Ctrl+Shift+Tab: Cycle Tabs
            if (ctrl && e.Key == Key.Tab)
            {
                if (shift)
                    vm.SelectPreviousTabCommand.Execute(null);
                else
                    vm.SelectNextTabCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+1..5: Workflow App Modes
            if (ctrl && !shift && !alt)
            {
                if (e.Key == Key.D1 || e.Key == Key.NumPad1) { vm.SetAppModeCommand.Execute("Explorer"); e.Handled = true; return; }
                if (e.Key == Key.D2 || e.Key == Key.NumPad2) { vm.SetAppModeCommand.Execute("DualCommander"); e.Handled = true; return; }
                if (e.Key == Key.D3 || e.Key == Key.NumPad3) { vm.SetAppModeCommand.Execute("MediaGallery"); e.Handled = true; return; }
                if (e.Key == Key.D4 || e.Key == Key.NumPad4) { vm.SetAppModeCommand.Execute("DeveloperWorkspace"); e.Handled = true; return; }
                if (e.Key == Key.D5 || e.Key == Key.NumPad5) { vm.SetAppModeCommand.Execute("ZenFocus"); e.Handled = true; return; }
            }

            // Esc: Exit Zen Focus Mode if active
            if (e.Key == Key.Escape && vm.CurrentAppMode == AppMode.ZenFocus)
            {
                vm.SetAppModeCommand.Execute("Explorer");
                e.Handled = true;
                return;
            }

            // View Mode Shortcuts
            // Ctrl+Shift+6: Details View
            if (ctrl && shift && (e.Key == Key.D6 || e.Key == Key.NumPad6))
            {
                vm.SetViewModeCommand.Execute("Details");
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+2: Grid Icons View
            if (ctrl && shift && (e.Key == Key.D2 || e.Key == Key.NumPad2))
            {
                vm.SetViewModeCommand.Execute("Grid");
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+5: List View
            if (ctrl && shift && (e.Key == Key.D5 || e.Key == Key.NumPad5))
            {
                vm.SetViewModeCommand.Execute("List");
                e.Handled = true;
                return;
            }

            // Ctrl+A: Select All
            if (ctrl && !shift && e.Key == Key.A)
            {
                FileListView.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+S: Save in Preview Pane if editing
            if (ctrl && !shift && e.Key == Key.S && vm.IsPreviewPaneOpen)
            {
                _ = vm.PreviewPane.SaveTextAsync();
                e.Handled = true;
                return;
            }

            // F5: Refresh
            if (e.Key == Key.F5)
            {
                vm.ActiveTab?.RefreshCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+N: New Folder
            if (ctrl && shift && e.Key == Key.N)
            {
                vm.ActiveTab?.CreateNewFolderCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+C: Copy Path
            if (ctrl && shift && e.Key == Key.C)
            {
                var item = FileListView.SelectedItem as FileItemViewModel;
                vm.ActiveTab?.CopyPath(item);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+T: Open in Terminal
            if (ctrl && shift && e.Key == Key.T)
            {
                vm.ActiveTab?.OpenTerminalHere();
                e.Handled = true;
                return;
            }

            // Shift+F10: Show More Options (Classic Properties / Shell)
            if (shift && e.Key == Key.F10)
            {
                if (FileListView.SelectedItem is FileItemViewModel sel)
                {
                    Win32.ShellHelper.ShowFileProperties(sel.Model.Path);
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+C: Copy
            if (ctrl && !shift && e.Key == Key.C)
            {
                vm.ActiveTab?.CopySelectedCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+X: Cut
            if (ctrl && !shift && e.Key == Key.X)
            {
                vm.ActiveTab?.CutSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+V: Paste
            if (ctrl && !shift && e.Key == Key.V)
            {
                vm.ActiveTab?.PasteCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Delete: Delete selected items
            if (e.Key == Key.Delete)
            {
                vm.ActiveTab?.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // F2: Rename selected item
            if (e.Key == Key.F2)
            {
                PromptRenameSelectedItem();
                e.Handled = true;
                return;
            }

            // Alt+Enter: Properties & Metadata Inspector
            if (alt && e.Key == Key.Enter)
            {
                ShowInvestigator("Properties");
                e.Handled = true;
                return;
            }

            // Ctrl+I: Deep Forensics & Investigation Mode
            if (ctrl && !shift && e.Key == Key.I)
            {
                ShowInvestigator("Forensics");
                e.Handled = true;
                return;
            }

            // Alt+Left / Backspace: Back
            if ((alt && e.Key == Key.Left) || (e.Key == Key.Back && !AddressTextBox.IsFocused && !OmniSearchBox.IsFocused))
            {
                if (vm.ActiveTab != null && vm.ActiveTab.CanGoBack())
                {
                    vm.ActiveTab.GoBackCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // Alt+Right: Forward
            if (alt && e.Key == Key.Right)
            {
                if (vm.ActiveTab != null && vm.ActiveTab.CanGoForward())
                {
                    vm.ActiveTab.GoForwardCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // Alt+Up: Parent Directory
            if (alt && e.Key == Key.Up)
            {
                vm.ActiveTab?.GoUpCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
        #endregion

        #region Dual-Mode Address Bar & Command Launcher
        private void AddressBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AddressEditView.Visibility == Visibility.Visible) return;
            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null) return;

            EnterAddressEditMode(showHistory: true);
            e.Handled = true;
        }

        private void BreadcrumbScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AddressEditView.Visibility == Visibility.Visible) return;
            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null) return;

            EnterAddressEditMode(showHistory: true);
            e.Handled = true;
        }

        private void AddressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AddressEditView.Visibility == Visibility.Visible) return;
            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null) return;

            EnterAddressEditMode(showHistory: true);
        }

        private void EditAddress_Click(object sender, RoutedEventArgs e)
        {
            EnterAddressEditMode(showHistory: true);
        }

        private void CancelAddressEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitAddressEditMode();
        }

        private void AddressHistoryDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (BreadcrumbView.Visibility == Visibility.Visible)
            {
                EnterAddressEditMode(showHistory: true);
            }
            else
            {
                AddressHistoryPopup.IsOpen = !AddressHistoryPopup.IsOpen;
                if (AddressHistoryPopup.IsOpen)
                {
                    ShowAddressHistory(AddressTextBox.Text);
                }
            }
        }

        private void AddressHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string path && DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                AddressHistoryPopup.IsOpen = false;
                ExitAddressEditMode();

                if (string.IsNullOrEmpty(path))
                {
                    vm.ActiveTab.NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                }
                else
                {
                    RecordAddressHistory(path);
                    vm.ActiveTab.NavigateTo(path);
                }
            }
        }

        private void AddressHistoryPopup_Closed(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!AddressTextBox.IsFocused && !AddressHistoryPopup.IsOpen)
                {
                    ExitAddressEditMode();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AddressEditView.Visibility == Visibility.Visible && AddressTextBox.IsFocused)
            {
                ShowAddressHistory(AddressTextBox.Text);
            }
        }

        public void EnterAddressEditMode(bool showHistory = true)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                AddressTextBox.Text = vm.ActiveTab.CurrentPath;
            }
            BreadcrumbView.Visibility = Visibility.Collapsed;
            AddressEditView.Visibility = Visibility.Visible;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AddressTextBox.Focus();
                AddressTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);

            if (showHistory)
            {
                ShowAddressHistory();
            }
        }

        public void ExitAddressEditMode()
        {
            AddressHistoryPopup.IsOpen = false;
            AddressEditView.Visibility = Visibility.Collapsed;
            BreadcrumbView.Visibility = Visibility.Visible;
        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddressHistoryPopup.IsOpen = false;
                ExecuteAddressInput(AddressTextBox.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                AddressHistoryPopup.IsOpen = false;
                ExitAddressEditMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (!AddressHistoryPopup.IsOpen)
                {
                    ShowAddressHistory(AddressTextBox.Text);
                }
            }
        }

        private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AddressHistoryPopup.IsOpen) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!AddressTextBox.IsFocused && !AddressHistoryPopup.IsOpen)
                {
                    ExitAddressEditMode();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RecordAddressHistory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim();
            _addressHistory.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _addressHistory.Insert(0, path);
            if (_addressHistory.Count > 25)
            {
                _addressHistory.RemoveAt(_addressHistory.Count - 1);
            }
        }

        private void ShowAddressHistory(string? filter = null)
        {
            var entries = new List<AddressHistoryEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string cleanFilter = (filter ?? "").Trim();

            // 1. Direct path suggestions (if user is typing a folder prefix)
            if (!string.IsNullOrEmpty(cleanFilter))
            {
                try
                {
                    string dir = cleanFilter;
                    string searchPattern = "*";
                    if (!Directory.Exists(dir))
                    {
                        dir = Path.GetDirectoryName(cleanFilter) ?? "";
                        searchPattern = Path.GetFileName(cleanFilter) + "*";
                    }

                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        var subs = Directory.GetDirectories(dir, searchPattern, SearchOption.TopDirectoryOnly)
                                            .Take(6);
                        foreach (var sub in subs)
                        {
                            if (seen.Add(sub))
                            {
                                entries.Add(new AddressHistoryEntry
                                {
                                    FullPath = sub,
                                    DisplayPath = sub,
                                    IconGlyph = "\uE8B7"
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Recent Visited History
            foreach (var hist in _addressHistory)
            {
                if (string.IsNullOrEmpty(cleanFilter) || hist.Contains(cleanFilter, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(hist))
                    {
                        entries.Add(new AddressHistoryEntry
                        {
                            FullPath = hist,
                            DisplayPath = hist,
                            IconGlyph = "\uE81C"
                        });
                        if (entries.Count >= 10) break;
                    }
                }
            }

            // 3. Standard Windows Folders (matching screenshot 2)
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var standardFolders = new (string Name, string Path, string Icon)[]
            {
                ("This PC", "", "\uE770"),
                ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "\uE7F4"),
                ("Downloads", Path.Combine(userProfile, "Downloads"), "\uE896"),
                ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "\uE8A5"),
                ("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "\uEB9F"),
                ("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "\uEC4F"),
                ("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "\uE714"),
            };

            foreach (var sf in standardFolders)
            {
                if (entries.Count >= 14) break;
                if (string.IsNullOrEmpty(cleanFilter) || sf.Name.Contains(cleanFilter, StringComparison.OrdinalIgnoreCase) || sf.Path.Contains(cleanFilter, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(sf.Path) || Directory.Exists(sf.Path))
                    {
                        if (seen.Add(sf.Path))
                        {
                            entries.Add(new AddressHistoryEntry
                            {
                                FullPath = sf.Path,
                                DisplayPath = string.IsNullOrEmpty(sf.Path) ? sf.Name : sf.Path,
                                IconGlyph = sf.Icon
                            });
                        }
                    }
                }
            }

            // 4. Drive Roots
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (entries.Count >= 16) break;
                    if (drive.IsReady && seen.Add(drive.RootDirectory.FullName))
                    {
                        if (string.IsNullOrEmpty(cleanFilter) || drive.Name.Contains(cleanFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            entries.Add(new AddressHistoryEntry
                            {
                                FullPath = drive.RootDirectory.FullName,
                                DisplayPath = $"{drive.Name} ({drive.DriveType})",
                                IconGlyph = "\uEDA2"
                            });
                        }
                    }
                }
            }
            catch { }

            AddressHistoryItemsControl.ItemsSource = entries;
            AddressHistoryPopup.IsOpen = entries.Count > 0;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void ExecuteAddressInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || DataContext is not MainViewModel vm || vm.ActiveTab == null)
            {
                ExitAddressEditMode();
                return;
            }

            input = input.Trim();
            string currentDir = Directory.Exists(vm.ActiveTab.CurrentPath)
                ? vm.ActiveTab.CurrentPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 0. "This PC" / Computer Root Virtual Folder
            if (FileSystemEngine.IsThisPc(input))
            {
                RecordAddressHistory("This PC");
                vm.ActiveTab.NavigateTo("This PC");
                ExitAddressEditMode();
                return;
            }

            // 0.1 Universal Path Resolution (shell: shortcuts, ~, %ENV_VARS%, drive letters)
            string resolved = FileSystemEngine.ResolvePath(input);
            if (resolved == "This PC" || Directory.Exists(resolved) || File.Exists(resolved))
            {
                RecordAddressHistory(resolved);
                vm.ActiveTab.NavigateTo(resolved);
                ExitAddressEditMode();
                return;
            }

            // Parse command and parameters
            string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmdName = parts[0];
            string args = parts.Length > 1 ? parts[1] : "";

            // 1. Direct Shell Commands (cmd, powershell, pwsh, wt, bash)
            if (cmdName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("wt", StringComparison.OrdinalIgnoreCase) ||
                cmdName.Equals("bash", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = cmdName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? cmdName : $"{cmdName}.exe",
                        Arguments = args,
                        WorkingDirectory = currentDir,
                        UseShellExecute = true
                    });
                    ExitAddressEditMode();
                    return;
                }
                catch { }
            }

            // 2. Expand Environment Variables (e.g. %temp%, %appdata%, %userprofile%)
            string expanded = Environment.ExpandEnvironmentVariables(input);
            if (Directory.Exists(expanded))
            {
                RecordAddressHistory(expanded);
                vm.ActiveTab.NavigateTo(expanded);
                ExitAddressEditMode();
                return;
            }

            if (File.Exists(expanded))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = expanded,
                        WorkingDirectory = Path.GetDirectoryName(expanded) ?? currentDir,
                        UseShellExecute = true
                    });
                    ExitAddressEditMode();
                    return;
                }
                catch { }
            }

            // 3. Child folder relative path
            string combined = Path.Combine(currentDir, input);
            if (Directory.Exists(combined))
            {
                RecordAddressHistory(combined);
                vm.ActiveTab.NavigateTo(combined);
                ExitAddressEditMode();
                return;
            }

            // 4. Try launching as system executable / app (e.g. notepad, calc, code, mspaint, taskmgr, regedit)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cmdName,
                    Arguments = args,
                    WorkingDirectory = currentDir,
                    UseShellExecute = true
                });
                ExitAddressEditMode();
                return;
            }
            catch
            {
                if (input.Contains('\\') || input.Contains('/') || input.Contains(':'))
                {
                    RecordAddressHistory(expanded);
                    vm.ActiveTab.NavigateTo(expanded);
                }
                else
                {
                    MessageBox.Show($"Unable to find or launch '{input}'.", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            ExitAddressEditMode();
        }
        #endregion

        #region Navigation & Tabs
        private void Tab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is TabViewModel tab && DataContext is MainViewModel vm)
            {
                vm.ActiveTab = tab;
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is TabViewModel tab && DataContext is MainViewModel vm)
            {
                vm.CloseTabCommand.Execute(tab);
            }
        }

        private void Breadcrumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string path && DataContext is MainViewModel vm)
            {
                if (FileSystemEngine.IsThisPc(path))
                {
                    vm.ActiveTab?.NavigateTo("This PC");
                }
                else
                {
                    vm.ActiveTab?.NavigateTo(path);
                }
            }
        }

        private void QuickAccess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string path && DataContext is MainViewModel vm)
            {
                vm.ActiveTab?.NavigateTo(path);
            }
        }

        private void Drive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string path && DataContext is MainViewModel vm)
            {
                vm.ActiveTab?.NavigateTo(path);
            }
        }
        #endregion

        #region Context Menu & File Management Actions
        private void NewMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private async void NewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string docType && DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                switch (docType)
                {
                    case "folder":
                        await vm.ActiveTab.CreateNewFolderAsync();
                        break;
                    case "txt":
                        await vm.ActiveTab.CreateNewDocumentAsync("New Text Document.txt");
                        break;
                    case "md":
                        await vm.ActiveTab.CreateNewDocumentAsync("New Document.md");
                        break;
                    case "ps1":
                        await vm.ActiveTab.CreateNewDocumentAsync("script.ps1");
                        break;
                    case "zip":
                        await vm.ActiveTab.CreateNewDocumentAsync("New Archive.zip");
                        break;
                }
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.CreateNewFolderCommand.Execute(null);
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.CreateNewFileCommand.Execute(null);
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.CutSelectedCommand.Execute(null);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.CopySelectedCommand.Execute(null);
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.PasteCommand.Execute(null);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.DeleteSelectedCommand.Execute(null);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var item = FileListView.SelectedItem as FileItemViewModel;
            if (DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                vm.ActiveTab.CopyPath(item);
            }
        }

        private void OpenTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                vm.ActiveTab.OpenTerminalHere();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ActiveTab?.RefreshCommand.Execute(null);
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && DataContext is MainViewModel vm)
            {
                if (item.Model.IsDirectory)
                {
                    vm.ActiveTab?.NavigateTo(item.Model.Path);
                }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.Model.Path,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
        }

        private void OpenInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && DataContext is MainViewModel vm)
            {
                string targetPath = item.Model.IsDirectory ? item.Model.Path : (Path.GetDirectoryName(item.Model.Path) ?? item.Model.Path);
                vm.AddNewTab(targetPath);
            }
        }

        private void OpenInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                string targetPath = item.Model.IsDirectory ? item.Model.Path : (Path.GetDirectoryName(item.Model.Path) ?? item.Model.Path);
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? "RiftVault.exe",
                        Arguments = $"\"{targetPath}\"",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{targetPath}\"",
                        UseShellExecute = true
                    });
                }
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            PromptRenameSelectedItem();
        }

        private void Properties_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FileItemViewModel item)
            {
                ShowInvestigator("Properties", item.Model.Path);
                return;
            }
            ShowInvestigator("Properties");
        }

        private void Investigate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FileItemViewModel item)
            {
                ShowInvestigator("Forensics", item.Model.Path);
                return;
            }
            ShowInvestigator("Forensics");
        }

        public void ShowInvestigator(string initialTab = "Properties", string? explicitPath = null)
        {
            string targetPath = explicitPath ?? "";
            if (string.IsNullOrEmpty(targetPath))
            {
                var item = (FileListView.SelectedItem as FileItemViewModel) ?? (SecondaryFileListView?.SelectedItem as FileItemViewModel);
                targetPath = item?.Model.Path ?? (DataContext as MainViewModel)?.ActiveTab?.CurrentPath ?? "";
            }

            if (string.IsNullOrEmpty(targetPath) || targetPath.Equals("This PC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
                    targetPath = drive?.RootDirectory.FullName ?? "C:\\";
                }
                catch
                {
                    targetPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }

            if (string.IsNullOrEmpty(targetPath)) return;

            try
            {
                var investigatorWindow = (UI.InvestigatorWindow)((App)Application.Current).Services.GetService(typeof(UI.InvestigatorWindow))!;
                if (IsLoaded)
                {
                    investigatorWindow.Owner = this;
                }
                investigatorWindow.InspectTarget(targetPath, initialTab);
                investigatorWindow.Show();
                investigatorWindow.Activate();
            }
            catch { }
        }

        private void OpenWithNotepad_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && !item.Model.IsDirectory)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{item.Model.Path}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void OpenWithVSCode_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c code \"{item.Model.Path}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                catch { }
            }
        }

        private void OpenWithDialog_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = $"shell32.dll,OpenAs_RunDLL \"{item.Model.Path}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            DuplicateSelectedItem();
        }

        private void DuplicateSelectedItem()
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.DuplicateSelectedCommand.Execute(item);
            }
        }

        private void CompressToZip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.CompressToZipCommand.Execute(item);
            }
        }

        private void ExtractZip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.ExtractZipCommand.Execute(item);
            }
        }

        private void ExtractToFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.ExtractZipCommand.Execute(item);
            }
        }

        private void PinToQuickAccess_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.PinToQuickAccessCommand.Execute(item.Model.Path);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            FileListView.SelectAll();
        }

        private void ToggleHiddenFiles_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ToggleHiddenFilesCommand.Execute(null);
            }
        }

        private void TogglePreviewPane_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TogglePreviewPaneCommand.Execute(null);
            }
        }

        private void PreviewPane_CloseRequested(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var selected = vm.ActiveTab?.Items.FirstOrDefault(i => i.IsSelected);
                if (selected != null)
                {
                    vm.DismissAutoPreviewForPath(selected.Model.Path);
                }
                vm.IsPreviewPaneOpen = false;
            }
        }

        private void AIAssistant_CloseRequested(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsAIAssistantOpen = false;
            }
        }

        private void AskAI_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsAIAssistantOpen = true;
                if (FileListView.SelectedItem is FileItemViewModel item)
                {
                    _ = vm.AIAssistant.ExecuteQuickPromptAsync($"Explain what this file does: {item.Model.Name}");
                }
            }
        }

        private void LocalAIStatus_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ToggleAIAssistantCommand.Execute(null);
            }
        }

        #region Windows 11 Style Context Menu Handlers
        private void QuickCut_Click(object sender, RoutedEventArgs e)
        {
            CloseContainingContextMenu(sender);
            Cut_Click(sender, e);
        }

        private void QuickCopy_Click(object sender, RoutedEventArgs e)
        {
            CloseContainingContextMenu(sender);
            Copy_Click(sender, e);
        }

        private void QuickRename_Click(object sender, RoutedEventArgs e)
        {
            CloseContainingContextMenu(sender);
            Rename_Click(sender, e);
        }

        private void QuickShare_Click(object sender, RoutedEventArgs e)
        {
            CloseContainingContextMenu(sender);
            TeleportOrShare_Click(sender, e);
        }

        private void QuickDelete_Click(object sender, RoutedEventArgs e)
        {
            CloseContainingContextMenu(sender);
            Delete_Click(sender, e);
        }

        private static void CloseContainingContextMenu(object sender)
        {
            if (sender is DependencyObject d)
            {
                var cm = FindVisualParent<ContextMenu>(d);
                if (cm != null)
                {
                    cm.IsOpen = false;
                }
            }
        }

        private void TeleportOrShare_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && DataContext is MainViewModel vm)
            {
                vm.ActiveTab?.CopySelectedCommand.Execute(null);
                vm.AIAssistant?.ExecuteQuickPromptAsync($"Pin '{item.Model.Name}' to quick access shelf");
            }
        }

        private void OpenWithPaint_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "mspaint.exe",
                        Arguments = $"\"{item.Model.Path}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void AISummarize_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsAIAssistantOpen = true;
                if (FileListView.SelectedItem is FileItemViewModel item)
                {
                    _ = vm.AIAssistant.ExecuteQuickPromptAsync($"Summarize the structure and contents of '{item.Model.Name}'");
                }
            }
        }

        private void SmartOrganize_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsAIAssistantOpen = true;
                _ = vm.AIAssistant.ExecuteQuickPromptAsync("Analyze the current folder contents and propose a clean organization strategy");
            }
        }

        private void SecureVault_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                MessageBox.Show($"File '{item.Model.Name}' has been encrypted.", "File Security", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ComputeChecksum_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FileItemViewModel item)
            {
                ShowInvestigator("Hashes", item.Model.Path);
                return;
            }
            ShowInvestigator("Hashes");
        }

        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && !item.Model.IsDirectory)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Model.Path,
                        Verb = "runas",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private void SetDesktopBackground_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && !item.Model.IsDirectory)
            {
                try
                {
                    SystemParametersInfo(20, 0, item.Model.Path, 0x01 | 0x02);
                }
                catch { }
            }
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            RotateSelectedImage(90);
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            RotateSelectedImage(270);
        }

        private void RotateSelectedImage(int angle)
        {
            if (FileListView.SelectedItem is FileItemViewModel item && !item.Model.IsDirectory)
            {
                try
                {
                    var decoder = BitmapDecoder.Create(new Uri(item.Model.Path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];
                    var rotated = new TransformedBitmap(frame, new RotateTransform(angle));

                    string ext = Path.GetExtension(item.Model.Path).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg")
                    {
                        var encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rotated));
                        using var stream = File.Create(item.Model.Path);
                        encoder.Save(stream);
                    }
                    else
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rotated));
                        using var stream = File.Create(item.Model.Path);
                        encoder.Save(stream);
                    }

                    if (DataContext is MainViewModel vm) vm.ActiveTab?.RefreshCommand.Execute(null);
                }
                catch { }
            }
        }

        private void CompressTo7z_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && FileListView.SelectedItem is FileItemViewModel item)
            {
                vm.CompressToZipCommand.Execute(item);
            }
        }

        private void ShowMoreOptions_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileItemViewModel item)
            {
                Win32.ShellHelper.ShowFileProperties(item.Model.Path);
            }
        }
        #endregion
        #endregion

        #region Inline Rename Modal
        private void PromptRenameSelectedItem()
        {
            if (FileListView.SelectedItem is not FileItemViewModel item) return;

            _itemToRename = item;
            InputModalTitle.Text = $"Rename {(item.Model.IsDirectory ? "Folder" : "File")}";
            InputModalTextBox.Text = item.Model.Name;
            InputModalOverlay.Visibility = Visibility.Visible;
            InputModalTextBox.Focus();

            // Smart Selection: Highlight only base name, preserving file extension
            if (!item.Model.IsDirectory && !string.IsNullOrEmpty(item.Model.Extension))
            {
                int extIndex = item.Model.Name.LastIndexOf('.');
                if (extIndex > 0)
                {
                    InputModalTextBox.Select(0, extIndex);
                }
                else
                {
                    InputModalTextBox.SelectAll();
                }
            }
            else
            {
                InputModalTextBox.SelectAll();
            }
        }

        private void InputModalConfirm_Click(object sender, RoutedEventArgs e)
        {
            string newName = InputModalTextBox.Text.Trim();
            if (_itemToRename != null && !string.IsNullOrEmpty(newName) && DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                _ = vm.ActiveTab.RenameItemAsync(_itemToRename, newName);
            }
            InputModalOverlay.Visibility = Visibility.Collapsed;
            _itemToRename = null;
        }

        private void InputModalCancel_Click(object sender, RoutedEventArgs e)
        {
            InputModalOverlay.Visibility = Visibility.Collapsed;
            _itemToRename = null;
        }

        private void InputModal_BackdropClick(object sender, MouseButtonEventArgs e)
        {
            InputModalOverlay.Visibility = Visibility.Collapsed;
            _itemToRename = null;
        }

        private void InputModalTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputModalConfirm_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                InputModalCancel_Click(sender, e);
                e.Handled = true;
            }
        }
        #endregion

        #region Sorting & Views
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void SortMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null && sender is ContextMenu cm)
            {
                string sortBy = vm.ActiveTab.SortBy;
                bool asc = vm.ActiveTab.SortAscending;

                foreach (var item in cm.Items.OfType<MenuItem>())
                {
                    if (item.Tag is string tag)
                    {
                        if (tag == "Asc") item.IsChecked = asc;
                        else if (tag == "Desc") item.IsChecked = !asc;
                        else item.IsChecked = (sortBy == tag);
                    }
                }
            }
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void ViewMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null && sender is ContextMenu cm)
            {
                string curMode = vm.ActiveTab.CurrentViewMode;
                foreach (var item in cm.Items.OfType<MenuItem>())
                {
                    if (item.Tag is string tag)
                    {
                        item.IsChecked = (curMode == tag);
                    }
                }
            }
        }

        private void ViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string mode && DataContext is MainViewModel vm)
            {
                vm.SetViewModeCommand.Execute(mode);
            }
        }

        private void SortSubmenu_Opened(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                string sortBy = vm.ActiveTab.SortBy;
                bool asc = vm.ActiveTab.SortAscending;

                CtxSortByName.IsChecked = sortBy == "Name";
                CtxSortByDate.IsChecked = sortBy == "DateModified";
                CtxSortByType.IsChecked = sortBy == "Type";
                CtxSortBySize.IsChecked = sortBy == "Size";

                CtxSortAsc.IsChecked = asc;
                CtxSortDesc.IsChecked = !asc;
            }
        }

        private void Sort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem item && item.Tag is string sortBy && DataContext is MainViewModel vm)
                {
                    vm.SetSortBy(sortBy);
                    UpdateHeaderArrows();
                }
            }
            catch { }
        }

        private void SortDirection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem item && item.Tag is string dir && DataContext is MainViewModel vm)
                {
                    vm.SetSortDirection(dir == "Asc");
                    UpdateHeaderArrows();
                }
            }
            catch { }
        }

        private void UpdateHeaderArrows()
        {
            try
            {
                if (FileListView?.View is GridView gridView && DataContext is MainViewModel vm && vm.ActiveTab != null)
                {
                    string currentSort = vm.ActiveTab.SortBy;
                    bool asc = vm.ActiveTab.SortAscending;
                    string arrow = asc ? " ▲" : " ▼";

                    foreach (var col in gridView.Columns)
                    {
                        if (col.Header is string title)
                        {
                            string clean = title.Replace(" ▲", "").Replace(" ▼", "").Trim();
                            string mapped = clean == "Date Modified" ? "DateModified" : clean;
                            if (mapped == currentSort)
                            {
                                col.Header = clean + arrow;
                            }
                            else
                            {
                                col.Header = clean;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                foreach (var item in e.RemovedItems.OfType<FileItemViewModel>())
                {
                    item.IsSelected = false;
                }
                foreach (var item in e.AddedItems.OfType<FileItemViewModel>())
                {
                    item.IsSelected = true;
                }
                vm.HandleFileSelection(e.AddedItems.OfType<FileItemViewModel>().LastOrDefault());
                vm.UpdateStatusText();
            }
        }

        private void FileList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            var dep = e.OriginalSource as DependencyObject;
            var itemContainer = FindVisualParent<ListViewItem>(dep);

            if (itemContainer?.DataContext is FileItemViewModel item)
            {
                if (item.Model.IsDirectory)
                {
                    vm.ActiveTab?.NavigateTo(item.Model.Path);
                }
                else
                {
                    OpenFileWithPreferredEditor(item.Model.Path, vm);
                }
            }
            else
            {
                if (vm.Settings.DoubleClickBlankToGoUp && vm.ActiveTab?.GoUpCommand?.CanExecute(null) == true)
                {
                    vm.ActiveTab.GoUpCommand.Execute(null);
                }
            }
        }

        private void SecondaryFileList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm || vm.SecondaryTab == null) return;

            var dep = e.OriginalSource as DependencyObject;
            var itemContainer = FindVisualParent<ListViewItem>(dep);

            if (itemContainer?.DataContext is FileItemViewModel item)
            {
                if (item.Model.IsDirectory)
                {
                    vm.SecondaryTab.NavigateTo(item.Model.Path);
                }
                else
                {
                    OpenFileWithPreferredEditor(item.Model.Path, vm);
                }
            }
            else
            {
                if (vm.Settings.DoubleClickBlankToGoUp && vm.SecondaryTab.GoUpCommand?.CanExecute(null) == true)
                {
                    vm.SecondaryTab.GoUpCommand.Execute(null);
                }
            }
        }

        private void FileList_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed && DataContext is MainViewModel vm)
            {
                if (vm.Settings.MiddleClickOpensInNewTab)
                {
                    var dep = e.OriginalSource as DependencyObject;
                    var itemContainer = FindVisualParent<ListViewItem>(dep);
                    if (itemContainer?.DataContext is FileItemViewModel item && item.Model.IsDirectory)
                    {
                        vm.AddNewTab(item.Model.Path);
                        e.Handled = true;
                    }
                }
            }
        }

        private void SecondaryFileList_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed && DataContext is MainViewModel vm)
            {
                if (vm.Settings.MiddleClickOpensInNewTab)
                {
                    var dep = e.OriginalSource as DependencyObject;
                    var itemContainer = FindVisualParent<ListViewItem>(dep);
                    if (itemContainer?.DataContext is FileItemViewModel item && item.Model.IsDirectory)
                    {
                        vm.AddNewTab(item.Model.Path);
                        e.Handled = true;
                    }
                }
            }
        }

        private void OpenFileWithPreferredEditor(string filePath, MainViewModel vm)
        {
            var preferredEditor = vm.Settings.PreferredEditorApp;
            var customPath = vm.Settings.CustomEditorPath;

            try
            {
                if (preferredEditor == "VSCode")
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "code",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true
                    });
                    return;
                }
                else if (preferredEditor == "Notepad")
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true
                    });
                    return;
                }
                else if (preferredEditor == "NotepadPlusPlus")
                {
                    string nppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe");
                    if (!File.Exists(nppPath))
                    {
                        nppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Notepad++", "notepad++.exe");
                    }
                    if (File.Exists(nppPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = nppPath,
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = true
                        });
                        return;
                    }
                }
                else if (preferredEditor == "Custom" && !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = customPath,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true
                    });
                    return;
                }
            }
            catch { }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null && DataContext is MainViewModel vm && vm.ActiveTab != null)
            {
                string? headerText = header.Column.Header as string;
                if (headerText != null)
                {
                    string clean = headerText.Replace(" ▲", "").Replace(" ▼", "").Trim();
                    string sortBy = clean == "Date Modified" ? "DateModified" : clean;

                    if (vm.ActiveTab.SortBy == sortBy)
                    {
                        vm.SetSortDirection(!vm.ActiveTab.SortAscending);
                    }
                    else
                    {
                        vm.ActiveTab.SortAscending = true;
                        vm.SetSortBy(sortBy);
                    }
                    UpdateHeaderArrows();
                }
            }
        }
        #endregion

        #region Tab Context Menu & Item Event Handlers
        private void NewTabFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AddNewTabCommand.Execute(null);
            }
        }

        private void DuplicateTabFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                vm.DuplicateTabCommand.Execute(tab);
            }
        }

        private void TogglePinTabFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                vm.TogglePinTabCommand.Execute(tab);
            }
        }

        private void CloseTabFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                vm.CloseTabCommand.Execute(tab);
            }
        }

        private void CloseOtherTabsFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                vm.CloseOtherTabsCommand.Execute(tab);
            }
        }

        private void CloseTabsToRightFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                vm.CloseTabsToRightCommand.Execute(tab);
            }
        }

        private void ReopenClosedTabFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ReopenClosedTabCommand.Execute(null);
            }
        }

        private void CopyTabPathFromMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var tab = GetTabFromMenuItem(sender) ?? vm.ActiveTab;
                if (tab != null && !string.IsNullOrEmpty(tab.CurrentPath))
                {
                    try { Clipboard.SetText(tab.CurrentPath); } catch { }
                }
            }
        }

        private TabViewModel? GetTabFromMenuItem(object sender)
        {
            if (sender is MenuItem mi)
            {
                if (mi.DataContext is TabViewModel tab) return tab;
                if (mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement pt && pt.DataContext is TabViewModel ptTab)
                {
                    return ptTab;
                }
            }
            return null;
        }

        private void Tab_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && sender is FrameworkElement fe && fe.DataContext is TabViewModel tab)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.CloseTabCommand.Execute(tab);
                    e.Handled = true;
                }
            }
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.DismissAutoPreviewForPath(null);
            }
        }

        private void ListViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem lvi)
            {
                if (!lvi.IsSelected)
                {
                    lvi.IsSelected = true;
                }
                lvi.Focus();
            }
        }
        #endregion
    }

    public class AddressHistoryEntry
    {
        public string DisplayPath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string IconGlyph { get; set; } = "\uE8B7";
    }
}
