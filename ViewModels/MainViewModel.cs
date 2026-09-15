using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.Models;
using RiftVault.Services;

namespace RiftVault.ViewModels
{
    public class QuickAccessItem : ObservableObject
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string IconGlyph { get; set; } = "\uE8B7";
        public string Category { get; set; } = "Generic";
        public bool IsPinned { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class DriveItem : ObservableObject
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Label { get; set; } = "";
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSize => TotalSize - FreeSpace;
        public double UsagePercent => TotalSize > 0 ? (double)UsedSize / TotalSize * 100 : 0;
        public string UsageText => $"{FormatSize(FreeSpace)} free of {FormatSize(TotalSize)}";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {suffixes[order]}";
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly IServiceProvider _serviceProvider;

        private readonly Stack<string> _closedTabHistory = new();

        [ObservableProperty]
        private ObservableCollection<TabViewModel> _tabs = new();

        [ObservableProperty]
        private TabViewModel? _activeTab;

        partial void OnActiveTabChanged(TabViewModel? oldValue, TabViewModel? newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= ActiveTab_PropertyChanged;
            }
            if (newValue != null)
            {
                newValue.PropertyChanged += ActiveTab_PropertyChanged;
            }
            UpdateStatusText();
        }

        private void ActiveTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TabViewModel.TotalItemCount) 
                or nameof(TabViewModel.SelectedCount) 
                or nameof(TabViewModel.SelectedSize) 
                or nameof(TabViewModel.IsLoading)
                or nameof(TabViewModel.CurrentPath))
            {
                UpdateStatusText();
            }
        }

        [ObservableProperty]
        private AppMode _currentAppMode = AppMode.Explorer;

        [ObservableProperty]
        private TabViewModel? _secondaryTab;

        [ObservableProperty]
        private double _galleryThumbnailSize = 180;

        [ObservableProperty]
        private FileItemViewModel? _lightboxItem;

        [ObservableProperty]
        private bool _isLightboxOpen;

        [ObservableProperty]
        private bool _isGitRepository;

        [ObservableProperty]
        private string _gitBranchName = string.Empty;

        [ObservableProperty]
        private string _projectBadge = string.Empty;

        [ObservableProperty]
        private string _terminalOutput = "⚡ Rift Vault Developer Workspace Console\nReady for PowerShell and Git commands. Type below or click quick action chips.\n";

        [ObservableProperty]
        private string _terminalCommandInput = string.Empty;

        [ObservableProperty]
        private bool _isTerminalRunning;

        [ObservableProperty]
        private bool _isDualPane;

        [ObservableProperty]
        private bool _isSidebarVisible = true;

        [ObservableProperty]
        private bool _isFocusMode;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private bool _isPreviewPaneOpen;

        [ObservableProperty]
        private string _customBackgroundPath = string.Empty;

        [ObservableProperty]
        private double _backgroundOpacity = 0.85;

        [ObservableProperty]
        private double _backgroundBlur = 0.0;

        [ObservableProperty]
        private double _backgroundDimmer = 0.30;

        [ObservableProperty]
        private double _fileAreaGlassOpacity = 0.25;

        [ObservableProperty]
        private string _backgroundStretch = "UniformToFill";

        public bool HasCustomBackground => !string.IsNullOrWhiteSpace(CustomBackgroundPath);

        partial void OnCustomBackgroundPathChanged(string value)
        {
            OnPropertyChanged(nameof(HasCustomBackground));
        }

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<QuickAccessItem> _quickAccessItems = new();

        [ObservableProperty]
        private ObservableCollection<DriveItem> _drives = new();

        [ObservableProperty]
        private bool _isAIAssistantOpen;

        [ObservableProperty]
        private bool _isAdministrator;

        [ObservableProperty]
        private string _statusText = "Ready";

        public PreviewPaneViewModel PreviewPane { get; } = new();
        public AIAssistantViewModel AIAssistant { get; }

        public AppSettings Settings => _settingsService.Current;
        public ISettingsService SettingsService => _settingsService;
        public bool ShowHiddenFiles => Settings.ShowHiddenFiles;

        public MainViewModel(ISettingsService settingsService, IThemeService themeService, IServiceProvider serviceProvider)
        {
            _settingsService = settingsService;
            _themeService = themeService;
            _serviceProvider = serviceProvider;

            // Load saved preferences
            _isPreviewPaneOpen = _settingsService.Current.IsPreviewPaneOpen;
            _customBackgroundPath = _settingsService.Current.CustomBackgroundPath;
            _backgroundOpacity = _settingsService.Current.BackgroundOpacity;
            _backgroundBlur = _settingsService.Current.BackgroundBlur;
            _backgroundDimmer = _settingsService.Current.BackgroundDimmer;
            _fileAreaGlassOpacity = _settingsService.Current.FileAreaGlassOpacity;
            _backgroundStretch = _settingsService.Current.BackgroundStretch;

            AIAssistant = new AIAssistantViewModel(null, _settingsService);
            AIAssistant.ActionRequested += OnAIActionRequested;
            AIAssistant.AISettingsRequested += () => OpenSettingsSection("AI");

            AddNewTabCommand = new RelayCommand(AddNewTab);
            CloseTabCommand = new RelayCommand<TabViewModel>(CloseTab);
            DuplicateTabCommand = new RelayCommand<TabViewModel>(DuplicateTab);
            TogglePinTabCommand = new RelayCommand<TabViewModel>(TogglePinTab);
            CloseOtherTabsCommand = new RelayCommand<TabViewModel>(CloseOtherTabs);
            CloseTabsToRightCommand = new RelayCommand<TabViewModel>(CloseTabsToRight);
            ReopenClosedTabCommand = new RelayCommand(ReopenClosedTab);
            SelectNextTabCommand = new RelayCommand(SelectNextTab);
            SelectPreviousTabCommand = new RelayCommand(SelectPreviousTab);

            SetAppModeCommand = new RelayCommand<string>(SetAppMode);
            RunTerminalCommand = new AsyncRelayCommand<string>(RunTerminalCommandAsync);
            ClearTerminalOutputCommand = new RelayCommand(() => TerminalOutput = string.Empty);
            CopySelectedToOppositePaneCommand = new AsyncRelayCommand(CopySelectedToOppositePaneAsync);
            MoveSelectedToOppositePaneCommand = new AsyncRelayCommand(MoveSelectedToOppositePaneAsync);
            SwapPanesCommand = new RelayCommand(SwapPanes);

            OpenLightboxCommand = new RelayCommand<FileItemViewModel>(OpenLightbox);
            CloseLightboxCommand = new RelayCommand(CloseLightbox);
            NextLightboxItemCommand = new RelayCommand(NextLightboxItem);
            PreviousLightboxItemCommand = new RelayCommand(PreviousLightboxItem);

            ToggleDualPaneCommand = new RelayCommand(ToggleDualPane);
            ToggleSidebarCommand = new RelayCommand(() => IsSidebarVisible = !IsSidebarVisible);
            ToggleFocusModeCommand = new RelayCommand(ToggleFocusMode);
            TogglePreviewPaneCommand = new RelayCommand(TogglePreviewPane);
            ToggleAIAssistantCommand = new RelayCommand(ToggleAIAssistant);
            ToggleHiddenFilesCommand = new RelayCommand(ToggleHiddenFiles);
            CompressToZipCommand = new AsyncRelayCommand<FileItemViewModel>(CompressToZipAsync);
            ExtractZipCommand = new AsyncRelayCommand<FileItemViewModel>(ExtractZipAsync);
            DuplicateSelectedCommand = new AsyncRelayCommand<FileItemViewModel>(DuplicateSelectedAsync);
            PinToQuickAccessCommand = new RelayCommand<string>(PinToQuickAccess);
            UnpinFromQuickAccessCommand = new RelayCommand<string>(UnpinFromQuickAccess);

            OpenSettingsCommand = new RelayCommand(OpenSettings);
            CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
            NavigateToPathCommand = new RelayCommand<string>(NavigateToPath);
            SetViewModeCommand = new RelayCommand<string>(SetViewMode);
            SetSortByCommand = new RelayCommand<string>(SetSortBy);
            SetSortDirectionCommand = new RelayCommand<bool>(SetSortDirection);
            RefreshDrivesCommand = new RelayCommand(LoadDrives);
            RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
            CheckAdminPrivileges();

            LoadQuickAccess();
            LoadDrives();
            InitializeStartup();
        }

        public ICommand AddNewTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand DuplicateTabCommand { get; }
        public ICommand TogglePinTabCommand { get; }
        public ICommand CloseOtherTabsCommand { get; }
        public ICommand CloseTabsToRightCommand { get; }
        public ICommand ReopenClosedTabCommand { get; }
        public ICommand SelectNextTabCommand { get; }
        public ICommand SelectPreviousTabCommand { get; }

        public ICommand SetAppModeCommand { get; }
        public ICommand RunTerminalCommand { get; }
        public ICommand ClearTerminalOutputCommand { get; }
        public ICommand CopySelectedToOppositePaneCommand { get; }
        public ICommand MoveSelectedToOppositePaneCommand { get; }
        public ICommand SwapPanesCommand { get; }

        public ICommand OpenLightboxCommand { get; }
        public ICommand CloseLightboxCommand { get; }
        public ICommand NextLightboxItemCommand { get; }
        public ICommand PreviousLightboxItemCommand { get; }

        public ICommand ToggleDualPaneCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleFocusModeCommand { get; }
        public ICommand TogglePreviewPaneCommand { get; }
        public ICommand ToggleAIAssistantCommand { get; }
        public ICommand ToggleHiddenFilesCommand { get; }
        public ICommand CompressToZipCommand { get; }
        public ICommand ExtractZipCommand { get; }
        public ICommand DuplicateSelectedCommand { get; }
        public ICommand PinToQuickAccessCommand { get; }
        public ICommand UnpinFromQuickAccessCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand CloseSettingsCommand { get; }
        public ICommand NavigateToPathCommand { get; }
        public ICommand SetViewModeCommand { get; }
        public ICommand SetSortByCommand { get; }
        public ICommand SetSortDirectionCommand { get; }
        public ICommand RefreshDrivesCommand { get; }
        public ICommand RestartAsAdminCommand { get; }

        private void CheckAdminPrivileges()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                IsAdministrator = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                IsAdministrator = false;
            }
        }

        public void RestartAsAdmin()
        {
            try
            {
                var procPath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(procPath))
                {
                    procPath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = procPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not elevate to administrator: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeStartup()
        {
            string startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var startup = _settingsService.Current.StartupBehavior;
            if (startup == "ThisPC")
            {
                startPath = "This PC";
            }
            else if (startup == "CustomFolder" && !string.IsNullOrWhiteSpace(_settingsService.Current.CustomStartupPath) && Directory.Exists(_settingsService.Current.CustomStartupPath))
            {
                startPath = _settingsService.Current.CustomStartupPath;
            }
            else if (startup == "Home")
            {
                startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            AddNewTab(startPath);
        }

        private void LoadQuickAccess()
        {
            QuickAccessItems.Clear();

            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Home",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                IconGlyph = "\uE80F",
                Category = "Home",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Desktop",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                IconGlyph = "\uE7F4",
                Category = "Desktop",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Documents",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                IconGlyph = "\uE8A5",
                Category = "Document",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Downloads",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                IconGlyph = "\uE896",
                Category = "Code",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Pictures",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                IconGlyph = "\uEB9F",
                Category = "Image",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Music",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                IconGlyph = "\uE8D6",
                Category = "Audio",
                IsPinned = true
            });
            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = "Videos",
                Path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                IconGlyph = "\uE8B2",
                Category = "Video",
                IsPinned = true
            });
        }

        private void LoadDrives()
        {
            Drives.Clear();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    Drives.Add(new DriveItem
                    {
                        Name = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        Path = drive.Name,
                        Label = $"{(string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel)} ({drive.Name.TrimEnd('\\')})",
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.TotalFreeSpace
                    });
                }
            }
            catch { }
        }

        partial void OnActiveTabChanged(TabViewModel? value)
        {
            foreach (var tab in Tabs)
            {
                tab.IsActive = (tab == value);
            }
            if (value != null)
            {
                CheckGitAndProjectStatus(value.CurrentPath);
            }
            UpdateStatusText();
        }

        partial void OnIsPreviewPaneOpenChanged(bool value)
        {
            _settingsService.Current.IsPreviewPaneOpen = value;
            _settingsService.Save();
            if (value && ActiveTab != null)
            {
                var selected = ActiveTab.Items.FirstOrDefault(i => i.IsSelected);
                _ = PreviewPane.LoadPreviewAsync(selected);
            }
        }

        public void TogglePreviewPane()
        {
            IsPreviewPaneOpen = !IsPreviewPaneOpen;
        }

        public void HandleFileSelection(FileItemViewModel? selected)
        {
            if (selected == null)
            {
                if (IsPreviewPaneOpen)
                {
                    _ = PreviewPane.LoadPreviewAsync(null);
                }
                return;
            }

            bool isPicture = selected.IsImage;
            if (isPicture && !IsPreviewPaneOpen && _autoPreviewDismissedPath != selected.Model.Path)
            {
                IsPreviewPaneOpen = true;
            }

            if (IsPreviewPaneOpen)
            {
                _ = PreviewPane.LoadPreviewAsync(selected);
            }
        }

        public void ToggleAIAssistant()
        {
            IsAIAssistantOpen = !IsAIAssistantOpen;
            if (IsAIAssistantOpen && ActiveTab != null)
            {
                AIAssistant.UpdateContext(ActiveTab.CurrentPath, ActiveTab.Items, ActiveTab.Items.FirstOrDefault(i => i.IsSelected));
            }
        }

        public void ToggleHiddenFiles()
        {
            _settingsService.Current.ShowHiddenFiles = !_settingsService.Current.ShowHiddenFiles;
            _settingsService.Save();
            OnPropertyChanged(nameof(ShowHiddenFiles));
            OnPropertyChanged(nameof(Settings));
            ActiveTab?.RefreshCommand.Execute(null);
            if (IsDualPane && SecondaryTab != null)
            {
                SecondaryTab.RefreshCommand.Execute(null);
            }
        }

        public void UpdateStatusText()
        {
            if (ActiveTab == null)
            {
                StatusText = "Ready";
                return;
            }

            UpdateSidebarSelection();
            ActiveTab.UpdateSelectionInfo();

            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"{ActiveTab.TotalItemCount} items");

            if (ActiveTab.SelectedCount > 0)
            {
                parts.Add($"{ActiveTab.SelectedCount} selected");
                if (ActiveTab.SelectedSize > 0)
                {
                    parts.Add(FormatSize(ActiveTab.SelectedSize));
                }
            }

            if (CurrentAppMode == AppMode.DualCommander && SecondaryTab != null)
            {
                parts.Add($"Dual Commander Active · Other: {SecondaryTab.Title}");
            }
            else if (CurrentAppMode == AppMode.DeveloperWorkspace && IsGitRepository)
            {
                parts.Add($"Git: {GitBranchName}" + (!string.IsNullOrEmpty(ProjectBadge) ? $" ({ProjectBadge})" : ""));
            }
            else if (CurrentAppMode == AppMode.MediaGallery)
            {
                parts.Add($"Gallery: {ActiveTab.MediaItems.Count} visual files");
            }
            else if (CurrentAppMode == AppMode.ZenFocus)
            {
                parts.Add("Zen Focus Mode (Press Esc or Ctrl+1 to exit)");
            }

            StatusText = string.Join("  ·  ", parts);

            var selected = ActiveTab.Items.FirstOrDefault(i => i.IsSelected);
            if (selected != null && !selected.Model.IsDirectory)
            {
                string ext = Path.GetExtension(selected.Model.Path).ToLowerInvariant();
                bool isPicture = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp"
                                      or ".ico" or ".tiff" or ".tif" or ".jfif" or ".avif" or ".heic"
                                      || string.Equals(selected.Category, "Image", StringComparison.OrdinalIgnoreCase);

                if (isPicture && !IsPreviewPaneOpen && _autoPreviewDismissedPath != selected.Model.Path)
                {
                    IsPreviewPaneOpen = true;
                }
            }

            if (IsPreviewPaneOpen)
            {
                _ = PreviewPane.LoadPreviewAsync(selected);
            }
            if (IsAIAssistantOpen)
            {
                AIAssistant.UpdateContext(ActiveTab.CurrentPath, ActiveTab.Items, selected);
            }
        }

        public void UpdateSidebarSelection()
        {
            if (ActiveTab == null) return;
            string cur = ActiveTab.CurrentPath?.TrimEnd('\\') ?? "";

            foreach (var item in QuickAccessItems)
            {
                string target = item.Path?.TrimEnd('\\') ?? "";
                item.IsSelected = !string.IsNullOrEmpty(cur) && string.Equals(target, cur, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var drive in Drives)
            {
                string target = drive.Path?.TrimEnd('\\') ?? "";
                drive.IsSelected = !string.IsNullOrEmpty(cur) &&
                                  (string.Equals(target, cur, StringComparison.OrdinalIgnoreCase) ||
                                   (cur.Length >= 2 && target.Length >= 2 && cur.StartsWith(target, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private string? _autoPreviewDismissedPath;

        public void DismissAutoPreviewForPath(string? path)
        {
            _autoPreviewDismissedPath = path;
        }

        private void OnAIActionRequested(AI.AIActionResult action)
        {
            switch (action.ActionType)
            {
                case "Search":
                    SearchQuery = action.Parameter;
                    break;
                case "Navigate":
                    NavigateToPath(action.Parameter);
                    break;
                case "Terminal":
                    ActiveTab?.OpenTerminalHere();
                    break;
                case "OpenWithNotepad":
                    if (!string.IsNullOrEmpty(action.Parameter))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "notepad.exe",
                                Arguments = $"\"{action.Parameter}\"",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                    break;
                case "Properties":
                    if (!string.IsNullOrEmpty(action.Parameter))
                    {
                        Win32.ShellHelper.ShowFileProperties(action.Parameter);
                    }
                    break;
                case "Duplicates":
                case "RunDuplicateScan":
                    if (ActiveTab != null)
                    {
                        try
                        {
                            var brain = (AI.DuplicateBrain)_serviceProvider.GetService(typeof(AI.DuplicateBrain))!;
                            if (brain != null)
                            {
                                _ = Task.Run(async () =>
                                {
                                    var dupes = await brain.FindExactDuplicatesAsync(ActiveTab.CurrentPath, System.Threading.CancellationToken.None);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (dupes.Count == 0)
                                        {
                                            MessageBox.Show("Zero duplicate content collisions found! All files in this directory are unique.", "Rift Duplicate Brain", MessageBoxButton.OK, MessageBoxImage.Information);
                                        }
                                        else
                                        {
                                            int totalCopies = dupes.Sum(d => d.Copies.Count);
                                            long wasted = dupes.Sum(d => d.Size * d.Copies.Count);
                                            MessageBox.Show($"Found {dupes.Count} duplicate groups ({totalCopies} redundant files) consuming {FormatSize(wasted)}!", "Rift Duplicate Brain", MessageBoxButton.OK, MessageBoxImage.Information);
                                        }
                                    });
                                });
                            }
                        }
                        catch { }
                    }
                    break;
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {suffixes[order]}";
        }

        public void AddNewTab() => AddNewTab(ActiveTab?.CurrentPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        public void AddNewTab(string path)
        {
            if (Tabs.Count >= 20)
            {
                var oldest = Tabs.FirstOrDefault(t => !t.IsPinned) ?? Tabs.First();
                CloseTab(oldest);
            }

            var newTab = (TabViewModel)_serviceProvider.GetService(typeof(TabViewModel))!;
            newTab.NavigateTo(path);
            Tabs.Add(newTab);
            ActiveTab = newTab;
        }

        private void CloseTab(TabViewModel? tab)
        {
            if (tab == null) return;

            if (!string.IsNullOrEmpty(tab.CurrentPath))
            {
                _closedTabHistory.Push(tab.CurrentPath);
            }

            int index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            tab.Dispose();

            if (Tabs.Count == 0)
            {
                AddNewTab();
            }
            else if (ActiveTab == tab)
            {
                ActiveTab = Tabs[Math.Max(0, Math.Min(index - 1, Tabs.Count - 1))];
            }
        }

        private void DuplicateTab(TabViewModel? tab)
        {
            var target = tab ?? ActiveTab;
            if (target == null) return;
            AddNewTab(target.CurrentPath);
        }

        private void TogglePinTab(TabViewModel? tab)
        {
            var target = tab ?? ActiveTab;
            if (target == null) return;
            target.IsPinned = !target.IsPinned;

            var pinned = Tabs.Where(t => t.IsPinned).ToList();
            var unpinned = Tabs.Where(t => !t.IsPinned).ToList();
            Tabs.Clear();
            foreach (var t in pinned) Tabs.Add(t);
            foreach (var t in unpinned) Tabs.Add(t);
            ActiveTab = target;
        }

        private void CloseOtherTabs(TabViewModel? tab)
        {
            var target = tab ?? ActiveTab;
            if (target == null) return;
            var toClose = Tabs.Where(t => t != target && !t.IsPinned).ToList();
            foreach (var t in toClose)
            {
                CloseTab(t);
            }
        }

        private void CloseTabsToRight(TabViewModel? tab)
        {
            var target = tab ?? ActiveTab;
            if (target == null) return;
            int index = Tabs.IndexOf(target);
            if (index < 0) return;
            var toClose = Tabs.Skip(index + 1).Where(t => !t.IsPinned).ToList();
            foreach (var t in toClose)
            {
                CloseTab(t);
            }
        }

        private void ReopenClosedTab()
        {
            if (_closedTabHistory.Count > 0)
            {
                var path = _closedTabHistory.Pop();
                if (Directory.Exists(path))
                {
                    AddNewTab(path);
                }
            }
        }

        private void SelectNextTab()
        {
            if (Tabs.Count <= 1 || ActiveTab == null) return;
            int idx = Tabs.IndexOf(ActiveTab);
            int next = (idx + 1) % Tabs.Count;
            ActiveTab = Tabs[next];
        }

        private void SelectPreviousTab()
        {
            if (Tabs.Count <= 1 || ActiveTab == null) return;
            int idx = Tabs.IndexOf(ActiveTab);
            int prev = (idx - 1 + Tabs.Count) % Tabs.Count;
            ActiveTab = Tabs[prev];
        }

        public void SetAppMode(string? modeStr)
        {
            if (string.IsNullOrEmpty(modeStr)) return;
            if (Enum.TryParse<AppMode>(modeStr, true, out var mode))
            {
                SetAppMode(mode);
            }
        }

        public void SetAppMode(AppMode mode)
        {
            CurrentAppMode = mode;
            switch (mode)
            {
                case AppMode.Explorer:
                    IsDualPane = false;
                    IsFocusMode = false;
                    IsSidebarVisible = true;
                    break;
                case AppMode.DualCommander:
                    IsDualPane = true;
                    EnsureSecondaryTab();
                    break;
                case AppMode.MediaGallery:
                    IsDualPane = false;
                    if (ActiveTab != null)
                    {
                        ActiveTab.CurrentViewMode = "Grid";
                    }
                    break;
                case AppMode.DeveloperWorkspace:
                    IsDualPane = false;
                    if (ActiveTab != null)
                    {
                        CheckGitAndProjectStatus(ActiveTab.CurrentPath);
                    }
                    break;
                case AppMode.ZenFocus:
                    IsDualPane = false;
                    IsFocusMode = true;
                    IsSidebarVisible = false;
                    IsPreviewPaneOpen = false;
                    IsAIAssistantOpen = false;
                    break;
            }
            UpdateStatusText();
        }

        public void EnsureSecondaryTab()
        {
            if (SecondaryTab == null)
            {
                SecondaryTab = (TabViewModel)_serviceProvider.GetService(typeof(TabViewModel))!;
                SecondaryTab.NavigateTo(ActiveTab?.CurrentPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
        }

        private async Task CopySelectedToOppositePaneAsync()
        {
            if (ActiveTab == null || SecondaryTab == null) return;
            var selected = ActiveTab.Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            string dest = SecondaryTab.CurrentPath;
            await Task.Run(() =>
            {
                foreach (var item in selected)
                {
                    try
                    {
                        if (item.Model.IsDirectory)
                        {
                            string targetDir = Path.Combine(dest, item.Model.Name);
                            CopyDirectoryRecursive(item.Model.Path, targetDir);
                        }
                        else
                        {
                            string targetFile = Path.Combine(dest, item.Model.Name);
                            File.Copy(item.Model.Path, targetFile, true);
                        }
                    }
                    catch { }
                }
            });

            SecondaryTab.RefreshCommand.Execute(null);
            StatusText = $"Copied {selected.Count} item(s) to {SecondaryTab.Title}";
        }

        private async Task MoveSelectedToOppositePaneAsync()
        {
            if (ActiveTab == null || SecondaryTab == null) return;
            var selected = ActiveTab.Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            string dest = SecondaryTab.CurrentPath;
            await Task.Run(() =>
            {
                foreach (var item in selected)
                {
                    try
                    {
                        string targetPath = Path.Combine(dest, item.Model.Name);
                        if (item.Model.IsDirectory)
                        {
                            Directory.Move(item.Model.Path, targetPath);
                        }
                        else
                        {
                            File.Move(item.Model.Path, targetPath, true);
                        }
                    }
                    catch { }
                }
            });

            ActiveTab.RefreshCommand.Execute(null);
            SecondaryTab.RefreshCommand.Execute(null);
            StatusText = $"Moved {selected.Count} item(s) to {SecondaryTab.Title}";
        }

        private void SwapPanes()
        {
            if (ActiveTab == null || SecondaryTab == null) return;
            var temp = ActiveTab;
            ActiveTab = SecondaryTab;
            SecondaryTab = temp;
        }

        private void OpenLightbox(FileItemViewModel? item)
        {
            if (item == null) return;
            LightboxItem = item;
            IsLightboxOpen = true;
        }

        private void CloseLightbox()
        {
            IsLightboxOpen = false;
            LightboxItem = null;
        }

        private void NextLightboxItem()
        {
            if (ActiveTab == null || LightboxItem == null) return;
            var media = ActiveTab.MediaItems;
            if (media.Count == 0) return;
            int idx = media.IndexOf(LightboxItem);
            if (idx >= 0 && idx < media.Count - 1)
            {
                LightboxItem = media[idx + 1];
            }
            else if (media.Count > 0)
            {
                LightboxItem = media[0];
            }
        }

        private void PreviousLightboxItem()
        {
            if (ActiveTab == null || LightboxItem == null) return;
            var media = ActiveTab.MediaItems;
            if (media.Count == 0) return;
            int idx = media.IndexOf(LightboxItem);
            if (idx > 0)
            {
                LightboxItem = media[idx - 1];
            }
            else if (media.Count > 0)
            {
                LightboxItem = media[^1];
            }
        }

        private void CheckGitAndProjectStatus(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
            {
                IsGitRepository = false;
                GitBranchName = string.Empty;
                ProjectBadge = string.Empty;
                return;
            }

            try
            {
                string current = dirPath;
                string? gitHeadPath = null;
                for (int i = 0; i < 4; i++)
                {
                    string gitDir = Path.Combine(current, ".git");
                    if (Directory.Exists(gitDir))
                    {
                        gitHeadPath = Path.Combine(gitDir, "HEAD");
                        break;
                    }
                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }

                if (gitHeadPath != null && File.Exists(gitHeadPath))
                {
                    IsGitRepository = true;
                    string headContent = File.ReadAllText(gitHeadPath).Trim();
                    if (headContent.StartsWith("ref: refs/heads/"))
                    {
                        GitBranchName = headContent.Substring("ref: refs/heads/".Length);
                    }
                    else if (headContent.Length >= 7)
                    {
                        GitBranchName = headContent.Substring(0, 7);
                    }
                    else
                    {
                        GitBranchName = "main";
                    }
                }
                else
                {
                    IsGitRepository = false;
                    GitBranchName = string.Empty;
                }

                if (Directory.GetFiles(dirPath, "*.csproj").Length > 0 || Directory.GetFiles(dirPath, "*.sln").Length > 0)
                {
                    ProjectBadge = ".NET Project";
                }
                else if (File.Exists(Path.Combine(dirPath, "package.json")))
                {
                    ProjectBadge = "Node.js Project";
                }
                else if (File.Exists(Path.Combine(dirPath, "Cargo.toml")))
                {
                    ProjectBadge = "Rust Project";
                }
                else if (File.Exists(Path.Combine(dirPath, "requirements.txt")) || File.Exists(Path.Combine(dirPath, "pyproject.toml")))
                {
                    ProjectBadge = "Python Project";
                }
                else if (File.Exists(Path.Combine(dirPath, "go.mod")))
                {
                    ProjectBadge = "Go Project";
                }
                else
                {
                    ProjectBadge = string.Empty;
                }
            }
            catch
            {
                IsGitRepository = false;
                GitBranchName = string.Empty;
                ProjectBadge = string.Empty;
            }
        }

        private async Task RunTerminalCommandAsync(string? rawCmd)
        {
            if (string.IsNullOrWhiteSpace(rawCmd) || ActiveTab == null) return;
            string command = rawCmd.Trim();
            TerminalCommandInput = string.Empty;

            IsTerminalRunning = true;
            TerminalOutput += $"\n> {command}\n";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{command}\"",
                    WorkingDirectory = ActiveTab.CurrentPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                await Task.WhenAll(stdoutTask, stderrTask);
                await proc.WaitForExitAsync();

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (!string.IsNullOrEmpty(stdout))
                {
                    TerminalOutput += stdout;
                }
                if (!string.IsNullOrEmpty(stderr))
                {
                    TerminalOutput += "[ERR] " + stderr;
                }
            }
            catch (Exception ex)
            {
                TerminalOutput += $"[Execution Error]: {ex.Message}\n";
            }
            finally
            {
                IsTerminalRunning = false;
            }
        }

        private void ToggleFocusMode()
        {
            IsFocusMode = !IsFocusMode;
            IsSidebarVisible = !IsFocusMode;
        }

        private void NavigateToPath(string? path)
        {
            if (string.IsNullOrEmpty(path) || ActiveTab == null) return;
            ActiveTab.NavigateTo(path);
        }

        private void SetViewMode(string? mode)
        {
            if (mode == null || ActiveTab == null) return;
            ActiveTab.CurrentViewMode = mode;
        }

        public void ToggleDualPane()
        {
            IsDualPane = !IsDualPane;
            if (IsDualPane)
            {
                EnsureSecondaryTab();
            }
        }

        public void SetSortBy(string? sortField)
        {
            if (sortField == null || ActiveTab == null) return;
            ActiveTab.SortBy = sortField;
            ActiveTab.SortItemsInPlace();
            OnPropertyChanged(nameof(ActiveTab));
        }

        public void SetSortDirection(bool ascending)
        {
            if (ActiveTab == null) return;
            ActiveTab.SortAscending = ascending;
            ActiveTab.SortItemsInPlace();
            OnPropertyChanged(nameof(ActiveTab));
        }

        public void ApplyTheme(string themeName)
        {
            _themeService.ApplyTheme(themeName);
            OnPropertyChanged(nameof(Settings));
        }

        public void SaveSettings()
        {
            _settingsService.Save();
        }

        public async Task CompressToZipAsync(FileItemViewModel? item)
        {
            var target = item ?? ActiveTab?.Items.FirstOrDefault(i => i.IsSelected);
            if (target == null || ActiveTab == null) return;

            string sourcePath = target.Model.Path;
            string zipPath = Path.Combine(ActiveTab.CurrentPath, $"{target.Model.Name}.zip");
            int counter = 2;
            while (File.Exists(zipPath))
            {
                zipPath = Path.Combine(ActiveTab.CurrentPath, $"{target.Model.Name} ({counter++}).zip");
            }

            try
            {
                await Task.Run(() =>
                {
                    if (target.Model.IsDirectory)
                    {
                        ZipFile.CreateFromDirectory(sourcePath, zipPath);
                    }
                    else
                    {
                        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                        archive.CreateEntryFromFile(sourcePath, target.Model.Name);
                    }
                });
                await ActiveTab.RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create zip: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task ExtractZipAsync(FileItemViewModel? item)
        {
            var target = item ?? ActiveTab?.Items.FirstOrDefault(i => i.IsSelected);
            if (target == null || ActiveTab == null || !target.Model.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return;

            string sourcePath = target.Model.Path;
            string destDir = Path.Combine(ActiveTab.CurrentPath, Path.GetFileNameWithoutExtension(sourcePath));
            int counter = 2;
            while (Directory.Exists(destDir))
            {
                destDir = Path.Combine(ActiveTab.CurrentPath, $"{Path.GetFileNameWithoutExtension(sourcePath)} ({counter++})");
            }

            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(sourcePath, destDir, true));
                await ActiveTab.RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to extract zip: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task DuplicateSelectedAsync(FileItemViewModel? item)
        {
            var targets = ActiveTab?.Items.Where(i => i.IsSelected).ToList();
            if (item != null && (targets == null || !targets.Contains(item)))
            {
                targets = new List<FileItemViewModel> { item };
            }
            if (targets == null || targets.Count == 0 || ActiveTab == null) return;

            try
            {
                foreach (var target in targets)
                {
                    string path = target.Model.Path;
                    string parent = Path.GetDirectoryName(path) ?? ActiveTab.CurrentPath;
                    string nameNoExt = Path.GetFileNameWithoutExtension(path);
                    string ext = Path.GetExtension(path);

                    if (target.Model.IsDirectory)
                    {
                        string newDir = Path.Combine(parent, $"{nameNoExt} - Copy");
                        int c = 2;
                        while (Directory.Exists(newDir))
                        {
                            newDir = Path.Combine(parent, $"{nameNoExt} - Copy ({c++})");
                        }
                        await Task.Run(() => CopyDirectoryRecursive(path, newDir));
                    }
                    else
                    {
                        string newFile = Path.Combine(parent, $"{nameNoExt} - Copy{ext}");
                        int c = 2;
                        while (File.Exists(newFile))
                        {
                            newFile = Path.Combine(parent, $"{nameNoExt} - Copy ({c++}){ext}");
                        }
                        File.Copy(path, newFile);
                    }
                }
                await ActiveTab.RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Duplicate failed: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
            }
        }

        public void PinToQuickAccess(string? path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (QuickAccessItems.Any(q => q.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;

            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            QuickAccessItems.Add(new QuickAccessItem
            {
                Name = name,
                Path = path,
                IconGlyph = "\uE8B7",
                Category = "Folder",
                IsPinned = true
            });
        }

        public void UnpinFromQuickAccess(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var item = QuickAccessItems.FirstOrDefault(q => q.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                QuickAccessItems.Remove(item);
            }
        }

        public void UpdateCustomBackground(string path, double opacity, double blur, string stretch, double dimmer = 0.30, double glassOpacity = 0.25)
        {
            CustomBackgroundPath = path;
            BackgroundOpacity = opacity;
            BackgroundBlur = blur;
            BackgroundStretch = stretch;
            BackgroundDimmer = dimmer;
            FileAreaGlassOpacity = glassOpacity;

            _settingsService.Current.CustomBackgroundPath = path;
            _settingsService.Current.BackgroundOpacity = opacity;
            _settingsService.Current.BackgroundBlur = blur;
            _settingsService.Current.BackgroundStretch = stretch;
            _settingsService.Current.BackgroundDimmer = dimmer;
            _settingsService.Current.FileAreaGlassOpacity = glassOpacity;
            _settingsService.Save();

            OnPropertyChanged(nameof(HasCustomBackground));
        }

        private void OpenSettings()
        {
            OpenSettingsSection(null);
        }

        public void OpenSettingsSection(string? category = null)
        {
            try
            {
                var settingsWindow = (UI.SettingsWindow)_serviceProvider.GetService(typeof(UI.SettingsWindow))!;
                if (System.Windows.Application.Current?.MainWindow != null)
                {
                    settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
                }
                if (!string.IsNullOrEmpty(category))
                {
                    settingsWindow.SelectCategory(category);
                }
                settingsWindow.ShowDialog();
                OnPropertyChanged(nameof(Settings));

                // Refresh background properties in case they were modified in settings
                CustomBackgroundPath = _settingsService.Current.CustomBackgroundPath;
                BackgroundOpacity = _settingsService.Current.BackgroundOpacity;
                BackgroundBlur = _settingsService.Current.BackgroundBlur;
                BackgroundStretch = _settingsService.Current.BackgroundStretch;
                BackgroundDimmer = _settingsService.Current.BackgroundDimmer;
                FileAreaGlassOpacity = _settingsService.Current.FileAreaGlassOpacity;
                OnPropertyChanged(nameof(HasCustomBackground));
            }
            catch { }
        }
    }
}