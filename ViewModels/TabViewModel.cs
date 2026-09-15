using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RiftVault.Core;
using RiftVault.Models;
using RiftVault.AI;
using RiftVault.Services;

namespace RiftVault.ViewModels
{
    public class BreadcrumbSegment
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
    }

    public partial class TabViewModel : ObservableObject, IDisposable
    {
        private readonly IFileSystemEngine _fsEngine;
        private readonly IThumbnailCache _thumbnailCache;
        private readonly AIEngine _aiEngine;
        private readonly ILogger<TabViewModel> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IShellFileService _shellFileService;
        private CancellationTokenSource? _navigationCts;

        // Navigation history
        private readonly List<string> _history = new();
        private int _historyIndex = -1;

        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private string _title = "Loading...";

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isPinned;

        public bool CanClose => !IsPinned;

        partial void OnIsPinnedChanged(bool value)
        {
            OnPropertyChanged(nameof(CanClose));
        }

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _items = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _mediaItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFolderEmpty))]
        private bool _isLoading;

        [ObservableProperty]
        private string _currentViewMode = "Details";

        [ObservableProperty]
        private string _sortBy = "Name";

        [ObservableProperty]
        private bool _sortAscending = true;

        [ObservableProperty]
        private string _filterQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BreadcrumbSegment> _breadcrumbs = new();

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private int _folderCount;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private long _selectedSize;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFolderEmpty))]
        private int _totalItemCount;

        public bool IsFolderEmpty => !IsLoading && TotalItemCount == 0 && CurrentPath != "This PC";

        public TabViewModel(IFileSystemEngine fsEngine, IThumbnailCache thumbnailCache, AIEngine aiEngine, ILogger<TabViewModel> logger, ISettingsService settingsService, IShellFileService shellFileService)
        {
            _fsEngine = fsEngine;
            _thumbnailCache = thumbnailCache;
            _aiEngine = aiEngine;
            _logger = logger;
            _settingsService = settingsService;
            _shellFileService = shellFileService;

            NavigateCommand = new AsyncRelayCommand<string>(NavigateToAsync);
            GoBackCommand = new RelayCommand(GoBack, CanGoBack);
            GoForwardCommand = new RelayCommand(GoForward, CanGoForward);
            GoUpCommand = new RelayCommand(GoUp);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);

            CreateNewFolderCommand = new AsyncRelayCommand(CreateNewFolderAsync);
            CreateNewFileCommand = new AsyncRelayCommand(CreateNewFileAsync);
            CopySelectedCommand = new RelayCommand(CopySelected);
            CutSelectedCommand = new RelayCommand(CutSelected);
            PasteCommand = new AsyncRelayCommand(PasteAsync);
            DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
            OpenTerminalHereCommand = new RelayCommand(OpenTerminalHere);
            CopyPathCommand = new RelayCommand<FileItemViewModel>(CopyPath);
        }

        public ICommand NavigateCommand { get; }
        public RelayCommand GoBackCommand { get; }
        public RelayCommand GoForwardCommand { get; }
        public ICommand GoUpCommand { get; }
        public ICommand RefreshCommand { get; }

        public ICommand CreateNewFolderCommand { get; }
        public ICommand CreateNewFileCommand { get; }
        public ICommand CopySelectedCommand { get; }
        public ICommand CutSelectedCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand OpenTerminalHereCommand { get; }
        public ICommand CopyPathCommand { get; }

        public bool CanGoBack() => _historyIndex > 0;
        public bool CanGoForward() => _historyIndex < _history.Count - 1;

        public void NavigateTo(string path)
        {
            NavigateCommand.Execute(path);
        }

        private async Task NavigateToAsync(string? path)
        {
            if (path == null) return;
            string resolvedPath = FileSystemEngine.ResolvePath(path);
            bool isThisPc = FileSystemEngine.IsThisPc(resolvedPath);

            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;

            try
            {
                IsLoading = true;
                if (isThisPc)
                {
                    CurrentPath = "This PC";
                    Title = "This PC";
                }
                else
                {
                    CurrentPath = resolvedPath;
                    Title = Path.GetFileName(resolvedPath);
                    if (string.IsNullOrEmpty(Title)) Title = resolvedPath; // Drive root
                }

                // Update history
                string historyEntry = isThisPc ? "This PC" : resolvedPath;
                if (_historyIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                }
                _history.Add(historyEntry);
                _historyIndex = _history.Count - 1;
                GoBackCommand.NotifyCanExecuteChanged();
                GoForwardCommand.NotifyCanExecuteChanged();

                // Update breadcrumbs
                UpdateBreadcrumbs(CurrentPath);

                // Load directory contents
                var files = await _fsEngine.GetDirectoryContentsAsync(CurrentPath, ct);

                if (ct.IsCancellationRequested) return;

                // Sort
                var sorted = ApplySort(files);

                Items.Clear();
                MediaItems.Clear();
                foreach (var file in sorted)
                {
                    var fileVm = new FileItemViewModel(file, _thumbnailCache);
                    Items.Add(fileVm);
                    if (!file.IsDirectory && IsMediaFile(file.Extension))
                    {
                        MediaItems.Add(fileVm);
                    }
                }

                // Update counts
                FileCount = files.Count(f => !f.IsDirectory);
                FolderCount = files.Count(f => f.IsDirectory);
                TotalItemCount = files.Count;
                SelectedCount = 0;
                SelectedSize = 0;

                if (!isThisPc && Directory.Exists(resolvedPath))
                {
                    _aiEngine.RecordFileAccess(resolvedPath);
                    _ = _aiEngine.AnalyzeFolderAsync(resolvedPath); // Fire and forget
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to navigate to {resolvedPath}");
                Title = "Access Denied";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void SortItemsInPlace()
        {
            if (Items.Count <= 1) return;

            var currentItems = Items.ToList();
            var dirs = currentItems.Where(i => i.Model.IsDirectory);
            var filesOnly = currentItems.Where(i => !i.Model.IsDirectory);

            IEnumerable<FileItemViewModel> sortedDirs;
            IEnumerable<FileItemViewModel> sortedFiles;

            switch (SortBy)
            {
                case "Name":
                    sortedDirs = SortAscending 
                        ? dirs.OrderBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase) 
                        : dirs.OrderByDescending(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending 
                        ? filesOnly.OrderBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase) 
                        : filesOnly.OrderByDescending(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                case "DateModified":
                    sortedDirs = SortAscending 
                        ? dirs.OrderBy(f => f.Model.DateModified) 
                        : dirs.OrderByDescending(f => f.Model.DateModified);
                    sortedFiles = SortAscending 
                        ? filesOnly.OrderBy(f => f.Model.DateModified) 
                        : filesOnly.OrderByDescending(f => f.Model.DateModified);
                    break;
                case "Size":
                    sortedDirs = SortAscending 
                        ? dirs.OrderBy(f => f.Model.Size).ThenBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase) 
                        : dirs.OrderByDescending(f => f.Model.Size).ThenByDescending(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending 
                        ? filesOnly.OrderBy(f => f.Model.Size) 
                        : filesOnly.OrderByDescending(f => f.Model.Size);
                    break;
                case "Type":
                    sortedDirs = SortAscending 
                        ? dirs.OrderBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase) 
                        : dirs.OrderByDescending(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending 
                        ? filesOnly.OrderBy(f => f.Model.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase) 
                        : filesOnly.OrderByDescending(f => f.Model.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                default:
                    sortedDirs = dirs.OrderBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = filesOnly.OrderBy(f => f.Model.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
            }

            var targetOrder = sortedDirs.Concat(sortedFiles).ToList();
            for (int targetIndex = 0; targetIndex < targetOrder.Count; targetIndex++)
            {
                var item = targetOrder[targetIndex];
                int currentIndex = Items.IndexOf(item);
                if (currentIndex != targetIndex && currentIndex >= 0)
                {
                    Items.Move(currentIndex, targetIndex);
                }
            }
        }

        private IEnumerable<FileItem> ApplySort(IEnumerable<FileItem> files)
        {
            // Always sort directories first
            var dirs = files.Where(f => f.IsDirectory);
            var filesOnly = files.Where(f => !f.IsDirectory);

            IEnumerable<FileItem> sortedDirs;
            IEnumerable<FileItem> sortedFiles;

            switch (SortBy)
            {
                case "Name":
                    sortedDirs = SortAscending ? dirs.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase) : dirs.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending ? filesOnly.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase) : filesOnly.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                case "DateModified":
                    sortedDirs = SortAscending ? dirs.OrderBy(f => f.DateModified) : dirs.OrderByDescending(f => f.DateModified);
                    sortedFiles = SortAscending ? filesOnly.OrderBy(f => f.DateModified) : filesOnly.OrderByDescending(f => f.DateModified);
                    break;
                case "Size":
                    sortedDirs = SortAscending ? dirs.OrderBy(f => f.Size).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase) : dirs.OrderByDescending(f => f.Size).ThenByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending ? filesOnly.OrderBy(f => f.Size) : filesOnly.OrderByDescending(f => f.Size);
                    break;
                case "Type":
                    sortedDirs = SortAscending ? dirs.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase) : dirs.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = SortAscending ? filesOnly.OrderBy(f => f.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase) : filesOnly.OrderByDescending(f => f.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                default:
                    sortedDirs = dirs.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    sortedFiles = filesOnly.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
            }

            return sortedDirs.Concat(sortedFiles);
        }

        private void UpdateBreadcrumbs(string path)
        {
            Breadcrumbs.Clear();

            if (FileSystemEngine.IsThisPc(path))
            {
                Breadcrumbs.Add(new BreadcrumbSegment { Name = "This PC", FullPath = "This PC" });
                return;
            }

            // Add "This PC" root
            Breadcrumbs.Add(new BreadcrumbSegment { Name = "This PC", FullPath = "This PC" });

            try
            {
                var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                string accumulated = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0)
                    {
                        accumulated = parts[0];
                        if (!accumulated.EndsWith(Path.DirectorySeparatorChar))
                            accumulated += Path.DirectorySeparatorChar;
                        Breadcrumbs.Add(new BreadcrumbSegment { Name = parts[0], FullPath = accumulated });
                    }
                    else
                    {
                        accumulated = Path.Combine(accumulated, parts[i]);
                        Breadcrumbs.Add(new BreadcrumbSegment { Name = parts[i], FullPath = accumulated });
                    }
                }
            }
            catch { }
        }

        private void GoBack()
        {
            if (!CanGoBack()) return;
            _historyIndex--;
            var path = _history[_historyIndex];
            // Navigate without adding to history
            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();
            _ = LoadDirectoryAsync(path, _navigationCts.Token);
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }

        private void GoForward()
        {
            if (!CanGoForward()) return;
            _historyIndex++;
            var path = _history[_historyIndex];
            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();
            _ = LoadDirectoryAsync(path, _navigationCts.Token);
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }

        private void GoUp()
        {
            if (string.IsNullOrEmpty(CurrentPath) || FileSystemEngine.IsThisPc(CurrentPath)) return;

            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                NavigateTo(parent.FullName);
            }
            else
            {
                // Drive root (e.g. C:\) -> Ascend to "This PC" like Windows Explorer
                NavigateTo("This PC");
            }
        }

        public async Task RefreshAsync()
        {
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                await NavigateToAsync(CurrentPath);
            }
        }

        /// <summary>
        /// Load a directory without modifying the history stack (used for back/forward).
        /// </summary>
        private async Task LoadDirectoryAsync(string path, CancellationToken ct)
        {
            bool isThisPc = FileSystemEngine.IsThisPc(path);
            try
            {
                IsLoading = true;
                if (isThisPc)
                {
                    CurrentPath = "This PC";
                    Title = "This PC";
                }
                else
                {
                    CurrentPath = path;
                    Title = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(Title)) Title = path;
                }

                UpdateBreadcrumbs(CurrentPath);

                var files = await _fsEngine.GetDirectoryContentsAsync(CurrentPath, ct);
                if (ct.IsCancellationRequested) return;

                var sorted = ApplySort(files);
                Items.Clear();
                MediaItems.Clear();
                foreach (var file in sorted)
                {
                    var fileVm = new FileItemViewModel(file, _thumbnailCache);
                    Items.Add(fileVm);
                    if (!file.IsDirectory && IsMediaFile(file.Extension))
                    {
                        MediaItems.Add(fileVm);
                    }
                }

                FileCount = files.Count(f => !f.IsDirectory);
                FolderCount = files.Count(f => f.IsDirectory);
                TotalItemCount = files.Count;
                SelectedCount = 0;
                SelectedSize = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load {path}");
                Title = "Access Denied";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void UpdateSelectionInfo()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            SelectedCount = selected.Count;
            SelectedSize = selected.Where(i => !i.Model.IsDirectory).Sum(i => i.Model.Size);
        }

        // ═══════════════════════════════════════════════════════════
        // Smart File & Folder Management System
        // ═══════════════════════════════════════════════════════════

        private static readonly List<string> _clipboardPaths = new();
        private static bool _isCutOperation = false;

        public async Task CreateNewFolderAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;
            try
            {
                string baseName = "New folder";
                string targetPath = Path.Combine(CurrentPath, baseName);
                int counter = 2;
                while (Directory.Exists(targetPath) || File.Exists(targetPath))
                {
                    targetPath = Path.Combine(CurrentPath, $"{baseName} ({counter++})");
                }
                bool success = await _shellFileService.CreateFolderAsync(targetPath);
                if (success) await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create new folder");
            }
        }

        public async Task CreateNewFileAsync()
        {
            await CreateNewDocumentAsync("New Text Document.txt");
        }

        public async Task CreateNewDocumentAsync(string baseName)
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;
            try
            {
                string ext = Path.GetExtension(baseName);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
                string targetPath = Path.Combine(CurrentPath, baseName);
                int counter = 2;
                while (File.Exists(targetPath) || Directory.Exists(targetPath))
                {
                    targetPath = Path.Combine(CurrentPath, $"{nameWithoutExt} ({counter++}){ext}");
                }
                bool success = await _shellFileService.CreateFileAsync(targetPath, string.Empty);
                if (success) await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create new document {baseName}");
            }
        }

        public void CopySelected()
        {
            var selected = Items.Where(i => i.IsSelected).Select(i => i.Model.Path).ToList();
            if (selected.Count == 0) return;

            _clipboardPaths.Clear();
            _clipboardPaths.AddRange(selected);
            _isCutOperation = false;

            try
            {
                var sc = new System.Collections.Specialized.StringCollection();
                sc.AddRange(selected.ToArray());
                Clipboard.SetFileDropList(sc);
            }
            catch { }
        }

        public void CutSelected()
        {
            var selected = Items.Where(i => i.IsSelected).Select(i => i.Model.Path).ToList();
            if (selected.Count == 0) return;

            _clipboardPaths.Clear();
            _clipboardPaths.AddRange(selected);
            _isCutOperation = true;

            try
            {
                var sc = new System.Collections.Specialized.StringCollection();
                sc.AddRange(selected.ToArray());
                Clipboard.SetFileDropList(sc);
            }
            catch { }
        }

        public async Task PasteAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;

            List<string> sources = new(_clipboardPaths);
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var drop = Clipboard.GetFileDropList();
                    if (drop != null && drop.Count > 0)
                    {
                        sources.Clear();
                        foreach (var p in drop)
                        {
                            if (!string.IsNullOrEmpty(p)) sources.Add(p);
                        }
                    }
                }
            }
            catch { }

            if (sources.Count == 0) return;

            try
            {
                foreach (var src in sources)
                {
                    string fileName = Path.GetFileName(src);
                    string dest = Path.Combine(CurrentPath, fileName);

                    if (_isCutOperation)
                    {
                        if (src.Equals(dest, StringComparison.OrdinalIgnoreCase)) continue;
                        await _shellFileService.MoveItemAsync(src, dest);
                    }
                    else
                    {
                        if (src.Equals(dest, StringComparison.OrdinalIgnoreCase))
                        {
                            string ext = Path.GetExtension(fileName);
                            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            dest = Path.Combine(CurrentPath, $"{nameWithoutExt} - Copy{ext}");
                            int c = 2;
                            while (File.Exists(dest) || Directory.Exists(dest))
                            {
                                dest = Path.Combine(CurrentPath, $"{nameWithoutExt} - Copy ({c++}){ext}");
                            }
                        }
                        await _shellFileService.CopyItemAsync(src, dest);
                    }
                }

                if (_isCutOperation)
                {
                    _clipboardPaths.Clear();
                    _isCutOperation = false;
                }

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to paste items");
            }
        }

        public async Task DeleteSelectedAsync()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            bool permanent = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            string actionText = permanent ? "permanently delete" : "send to the Recycle Bin";

            var result = MessageBox.Show(
                $"Are you sure you want to {actionText} {selected.Count} selected item(s)?",
                permanent ? "Confirm Permanent Delete" : "Delete to Recycle Bin",
                MessageBoxButton.YesNo,
                permanent ? MessageBoxImage.Warning : MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var paths = selected.Select(i => i.Model.Path).ToList();
                await _shellFileService.DeleteItemsAsync(paths, permanent);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete selected items");
            }
        }

        public async Task RenameItemAsync(FileItemViewModel item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName)) return;
            if (item.Model.Name.Equals(newName, StringComparison.Ordinal)) return;

            try
            {
                bool success = await _shellFileService.RenameItemAsync(item.Model.Path, newName);
                if (success) await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to rename {item.Model.Path} to {newName}");
            }
        }

        public void OpenTerminalHere()
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;

            string terminal = _settingsService?.Current?.PreferredTerminal ?? "WindowsTerminal";
            string customPath = _settingsService?.Current?.CustomTerminalPath ?? "";

            try
            {
                ProcessStartInfo? psi = null;

                if (terminal == "Custom" && !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = customPath,
                        WorkingDirectory = CurrentPath,
                        UseShellExecute = true
                    };
                }
                else if (terminal == "WindowsTerminal")
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "wt.exe",
                        Arguments = $"-d \"{CurrentPath}\"",
                        WorkingDirectory = CurrentPath,
                        UseShellExecute = true
                    };
                }
                else if (terminal == "PowerShell")
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        WorkingDirectory = CurrentPath,
                        UseShellExecute = true
                    };
                }
                else if (terminal == "GitBash")
                {
                    string gitBashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "git-bash.exe");
                    if (!File.Exists(gitBashPath))
                    {
                        gitBashPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "git-bash.exe");
                    }
                    if (File.Exists(gitBashPath))
                    {
                        psi = new ProcessStartInfo
                        {
                            FileName = gitBashPath,
                            WorkingDirectory = CurrentPath,
                            UseShellExecute = true
                        };
                    }
                }

                if (psi == null)
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        WorkingDirectory = CurrentPath,
                        UseShellExecute = true
                    };
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to launch preferred terminal {Terminal}, falling back to cmd.exe", terminal);
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        WorkingDirectory = CurrentPath,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        public void CopyPath(FileItemViewModel? item = null)
        {
            string path = item?.Model.Path ?? CurrentPath;
            if (!string.IsNullOrEmpty(path))
            {
                try { Clipboard.SetText(path); } catch { }
            }
        }

        private static readonly HashSet<string> _mediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff", ".tif",
            ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v",
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
        };

        public static bool IsMediaFile(string? extension)
        {
            return !string.IsNullOrEmpty(extension) && _mediaExtensions.Contains(extension);
        }

        public void Dispose()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
        }
    }
}