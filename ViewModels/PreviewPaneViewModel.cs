using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.Models;

namespace RiftVault.ViewModels
{
    public enum PreviewContentType
    {
        None,
        Image,
        Video,
        Audio,
        CodeText,
        Archive,
        Generic
    }

    public partial class PreviewPaneViewModel : ObservableObject, IDisposable
    {
        private CancellationTokenSource? _loadCts;

        [ObservableProperty]
        private bool _isOpen = true;

        [ObservableProperty]
        private FileItemViewModel? _selectedItem;

        [ObservableProperty]
        private bool _hasSelection;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private PreviewContentType _previewType = PreviewContentType.None;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileType = string.Empty;

        [ObservableProperty]
        private string _fileSizeText = string.Empty;

        [ObservableProperty]
        private string _dateModifiedText = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _iconGlyph = "\uE8A5";

        // ─── Photo / Image Preview ──────────────────────────────────────
        [ObservableProperty]
        private BitmapSource? _imageSource;

        [ObservableProperty]
        private int _imagePixelWidth;

        [ObservableProperty]
        private int _imagePixelHeight;

        [ObservableProperty]
        private string _fileExtensionBadge = "FILE";

        [ObservableProperty]
        private string _dimensionsText = string.Empty;

        [ObservableProperty]
        private string _aspectRatioText = string.Empty;

        [ObservableProperty]
        private string _megapixelsText = string.Empty;

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        [ObservableProperty]
        private double _imageRotation;

        [ObservableProperty]
        private bool _isImageCopiedFeedback;

        [ObservableProperty]
        private bool _isPathCopiedFeedback;

        // ─── Video & Audio Preview ──────────────────────────────────────
        [ObservableProperty]
        private Uri? _mediaSource;

        [ObservableProperty]
        private bool _isMediaPlaying;

        [ObservableProperty]
        private bool _isLooping = true;

        [ObservableProperty]
        private double _playbackSpeed = 1.0;

        [ObservableProperty]
        private string _speedText = "1.0x";

        [ObservableProperty]
        private TimeSpan _mediaPosition = TimeSpan.Zero;

        [ObservableProperty]
        private TimeSpan _mediaDuration = TimeSpan.Zero;

        [ObservableProperty]
        private string _mediaDurationText = "00:00 / 00:00";

        [ObservableProperty]
        private double _mediaVolume = 0.75;

        [ObservableProperty]
        private bool _isMediaMuted;

        // ─── Code & Text Preview (Mini-Notepad Editor) ───────────────────
        [ObservableProperty]
        private string _textContent = string.Empty;

        [ObservableProperty]
        private bool _isEditingText;

        [ObservableProperty]
        private bool _isTextDirty;

        [ObservableProperty]
        private bool _isTextSavedFeedback;

        [ObservableProperty]
        private int _lineCount;

        [ObservableProperty]
        private int _charCount;

        [ObservableProperty]
        private string _textEncoding = "UTF-8";

        [ObservableProperty]
        private bool _wrapText = true;

        [ObservableProperty]
        private bool _isLoading;

        // ─── Archive / Directory Info ───────────────────────────────────
        [ObservableProperty]
        private int _archiveFileCount;

        [ObservableProperty]
        private string _archiveDetails = string.Empty;

        public PreviewPaneViewModel()
        {
            SaveTextCommand = new AsyncRelayCommand(SaveTextAsync);
            ToggleEditModeCommand = new RelayCommand(() => IsEditingText = !IsEditingText);
            ToggleWrapCommand = new RelayCommand(() => WrapText = !WrapText);
            OpenWithNotepadCommand = new RelayCommand(OpenWithNotepad);
            OpenWithVSCodeCommand = new RelayCommand(OpenWithVSCode);
            OpenWithDefaultCommand = new RelayCommand(OpenWithDefault);
            OpenWithDialogCommand = new RelayCommand(OpenWithDialog);
            CopyPathCommand = new RelayCommand(CopyPath);
            CopyImageCommand = new RelayCommand(CopyImage);
            ExtractZipCommand = new AsyncRelayCommand(ExtractZipAsync);

            ZoomInCommand = new RelayCommand(() => ZoomLevel = Math.Min(5.0, Math.Round(ZoomLevel + 0.25, 2)));
            ZoomOutCommand = new RelayCommand(() => ZoomLevel = Math.Max(0.25, Math.Round(ZoomLevel - 0.25, 2)));
            ResetZoomCommand = new RelayCommand(() => { ZoomLevel = 1.0; ImageRotation = 0; });
            RotateRightCommand = new RelayCommand(() => ImageRotation = (ImageRotation + 90) % 360);
            RotateLeftCommand = new RelayCommand(() => ImageRotation = (ImageRotation - 90 + 360) % 360);
            ToggleLoopCommand = new RelayCommand(() => IsLooping = !IsLooping);
            CycleSpeedCommand = new RelayCommand(CycleSpeed);
        }

        public ICommand SaveTextCommand { get; }
        public ICommand ToggleEditModeCommand { get; }
        public ICommand ToggleWrapCommand { get; }
        public ICommand OpenWithNotepadCommand { get; }
        public ICommand OpenWithVSCodeCommand { get; }
        public ICommand OpenWithDefaultCommand { get; }
        public ICommand OpenWithDialogCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand CopyImageCommand { get; }
        public ICommand ExtractZipCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand ToggleLoopCommand { get; }
        public ICommand CycleSpeedCommand { get; }

        public async Task LoadPreviewAsync(FileItemViewModel? item)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            SelectedItem = item;

            if (item == null)
            {
                ClearPreview();
                HasSelection = false;
                return;
            }

            HasSelection = true;
            FileName = item.Model.Name;
            FilePath = item.Model.Path;
            FileType = item.TypeDescription;
            FileSizeText = item.DisplaySize;
            DateModifiedText = item.DisplayDate;
            Category = item.Category;
            IconGlyph = item.IconGlyph;
            IsDirectory = item.Model.IsDirectory;

            string rawExt = Path.GetExtension(FilePath);
            FileExtensionBadge = !string.IsNullOrEmpty(rawExt) ? rawExt.TrimStart('.').ToUpperInvariant() : "FILE";
            ZoomLevel = 1.0;
            ImageRotation = 0;

            ClearMediaAndText();

            if (IsDirectory)
            {
                PreviewType = PreviewContentType.None;
                return;
            }

            string ext = rawExt.ToLowerInvariant();
            IsLoading = true;

            try
            {
                // 1. Photo / Image preview
                if (IsImageExtension(ext))
                {
                    PreviewType = PreviewContentType.Image;
                    await LoadImagePreviewAsync(FilePath, ct);
                    return;
                }

                // 2. Video preview
                if (IsVideoExtension(ext))
                {
                    PreviewType = PreviewContentType.Video;
                    MediaSource = new Uri(FilePath);
                    return;
                }

                // 3. Audio preview
                if (IsAudioExtension(ext))
                {
                    PreviewType = PreviewContentType.Audio;
                    MediaSource = new Uri(FilePath);
                    return;
                }

                // 4. Code & Text preview (Mini-Notepad compatible)
                if (IsTextOrCodeExtension(ext))
                {
                    PreviewType = PreviewContentType.CodeText;
                    await LoadTextPreviewAsync(FilePath, ct);
                    return;
                }

                // 5. ZIP Archive preview
                if (ext == ".zip")
                {
                    PreviewType = PreviewContentType.Archive;
                    await LoadArchivePreviewAsync(FilePath, ct);
                    return;
                }

                // 6. Generic preview
                PreviewType = PreviewContentType.Generic;
            }
            catch
            {
                PreviewType = PreviewContentType.Generic;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadImagePreviewAsync(string path, CancellationToken ct)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bi.StreamSource = stream;
                    bi.EndInit();
                    bi.Freeze();

                    int width = bi.PixelWidth;
                    int height = bi.PixelHeight;
                    double mp = (width * height) / 1_000_000.0;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        ImageSource = bi;
                        ImagePixelWidth = width;
                        ImagePixelHeight = height;
                        DimensionsText = $"{width} × {height} px";
                        MegapixelsText = $"{mp:F1} MP";
                        ZoomLevel = 1.0;
                        ImageRotation = 0;

                        int gcd = GreatestCommonDivisor(width, height);
                        AspectRatioText = gcd > 0 ? $"{width / gcd}:{height / gcd}" : "";
                    });
                }
                catch
                {
                    try
                    {
                        using var stream2 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        var decoder = BitmapDecoder.Create(stream2, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0)
                        {
                            var frame = decoder.Frames[0];
                            frame.Freeze();
                            int width = frame.PixelWidth;
                            int height = frame.PixelHeight;
                            double mp = (width * height) / 1_000_000.0;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (ct.IsCancellationRequested) return;
                                ImageSource = frame;
                                ImagePixelWidth = width;
                                ImagePixelHeight = height;
                                DimensionsText = $"{width} × {height} px";
                                MegapixelsText = $"{mp:F1} MP";
                                ZoomLevel = 1.0;
                                ImageRotation = 0;

                                int gcd = GreatestCommonDivisor(width, height);
                                AspectRatioText = gcd > 0 ? $"{width / gcd}:{height / gcd}" : "";
                            });
                            return;
                        }
                    }
                    catch { }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PreviewType = PreviewContentType.Generic;
                    });
                }
            }, ct);
        }

        private async Task LoadTextPreviewAsync(string path, CancellationToken ct)
        {
            await Task.Run(async () =>
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    // Cap preview at 1.5MB to prevent UI memory stalls on giant logs
                    long maxBytes = 1_500_000;
                    byte[] buffer;
                    bool isTruncated = false;

                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        int bytesToRead = (int)Math.Min(fs.Length, maxBytes);
                        buffer = new byte[bytesToRead];
                        await fs.ReadAsync(buffer, 0, bytesToRead, ct);
                        isTruncated = fs.Length > maxBytes;
                    }

                    // Auto-detect UTF-8 / ASCII
                    string text = Encoding.UTF8.GetString(buffer);
                    if (isTruncated)
                    {
                        text += "\n\n... [Preview truncated: File exceeds 1.5MB] ...";
                    }

                    int lines = text.Split('\n').Length;
                    int chars = text.Length;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        TextContent = text;
                        LineCount = lines;
                        CharCount = chars;
                        IsEditingText = false;
                        IsTextDirty = false;
                    });
                }
                catch
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PreviewType = PreviewContentType.Generic;
                    });
                }
            }, ct);
        }

        private async Task LoadArchivePreviewAsync(string path, CancellationToken ct)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var archive = ZipFile.OpenRead(path);
                    int count = archive.Entries.Count;
                    long uncompressedSize = 0;
                    foreach (var entry in archive.Entries)
                    {
                        uncompressedSize += entry.Length;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        ArchiveFileCount = count;
                        ArchiveDetails = $"{count} file(s) · {FormatBytes(uncompressedSize)} uncompressed";
                    });
                }
                catch { }
            }, ct);
        }

        public async Task SaveTextAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;

            try
            {
                await File.WriteAllTextAsync(FilePath, TextContent, Encoding.UTF8);
                IsTextDirty = false;
                IsTextSavedFeedback = true;

                // Brief visual feedback reset
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() => IsTextSavedFeedback = false);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenWithNotepad()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void OpenWithVSCode()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "code",
                    Arguments = $"\"{FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                // If 'code' is not in PATH, try common VS Code installation paths
                string userVsCode = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
                if (File.Exists(userVsCode))
                {
                    Process.Start(userVsCode, $"\"{FilePath}\"");
                }
                else
                {
                    OpenWithNotepad();
                }
            }
        }

        public void OpenWithDefault()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = FilePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void OpenWithDialog()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;
            try
            {
                Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {FilePath}");
            }
            catch { }
        }

        public void CopyPath()
        {
            if (!string.IsNullOrEmpty(FilePath))
            {
                try
                {
                    Clipboard.SetText(FilePath);
                    IsPathCopiedFeedback = true;
                    _ = Task.Delay(1800).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() => IsPathCopiedFeedback = false);
                    });
                }
                catch { }
            }
        }

        public void CopyImage()
        {
            try
            {
                if (ImageSource != null)
                {
                    Clipboard.SetImage(ImageSource);
                    IsImageCopiedFeedback = true;
                    _ = Task.Delay(1800).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() => IsImageCopiedFeedback = false);
                    });
                }
                else if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    var files = new System.Collections.Specialized.StringCollection { FilePath };
                    Clipboard.SetFileDropList(files);
                    IsImageCopiedFeedback = true;
                    _ = Task.Delay(1800).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() => IsImageCopiedFeedback = false);
                    });
                }
            }
            catch { }
        }

        public void CycleSpeed()
        {
            PlaybackSpeed = PlaybackSpeed switch
            {
                1.0 => 1.25,
                1.25 => 1.5,
                1.5 => 2.0,
                2.0 => 0.5,
                _ => 1.0
            };
            SpeedText = $"{PlaybackSpeed:0.##}x";
        }

        public async Task ExtractZipAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;

            string targetDir = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", Path.GetFileNameWithoutExtension(FilePath));
            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(FilePath, targetDir, true));
                MessageBox.Show($"Extracted successfully to:\n{targetDir}", "Rift Vault Archive", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to extract archive: {ex.Message}", "Rift Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearPreview()
        {
            FileName = string.Empty;
            FilePath = string.Empty;
            FileType = string.Empty;
            FileSizeText = string.Empty;
            DateModifiedText = string.Empty;
            Category = string.Empty;
            IconGlyph = "\uE8A5";
            FileExtensionBadge = "FILE";
            PreviewType = PreviewContentType.None;
            ClearMediaAndText();
        }

        private void ClearMediaAndText()
        {
            ImageSource = null;
            DimensionsText = string.Empty;
            AspectRatioText = string.Empty;
            MegapixelsText = string.Empty;
            ZoomLevel = 1.0;
            ImageRotation = 0;
            MediaSource = null;
            IsMediaPlaying = false;
            TextContent = string.Empty;
            IsEditingText = false;
            IsTextDirty = false;
            IsTextSavedFeedback = false;
            LineCount = 0;
            CharCount = 0;
            ArchiveDetails = string.Empty;
        }

        private static bool IsImageExtension(string ext)
        {
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" 
                       or ".ico" or ".tiff" or ".tif" or ".jfif" or ".avif" or ".heic";
        }

        private static bool IsVideoExtension(string ext)
        {
            return ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v" or ".flv" or ".3gp" or ".ts";
        }

        private static bool IsAudioExtension(string ext)
        {
            return ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".wma" or ".m4a" or ".ogg";
        }

        private static bool IsTextOrCodeExtension(string ext)
        {
            return ext is ".txt" or ".log" or ".md" or ".json" or ".xml" or ".html" or ".htm" or ".css" or ".scss"
                       or ".js" or ".ts" or ".jsx" or ".tsx" or ".cs" or ".cpp" or ".c" or ".h" or ".hpp"
                       or ".py" or ".java" or ".go" or ".rs" or ".sql" or ".sh" or ".bat" or ".cmd" or ".ps1"
                       or ".yaml" or ".yml" or ".ini" or ".cfg" or ".config" or ".toml" or ".props" or ".targets";
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {suffixes[order]}";
        }

        public void Dispose()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        }
    }
}
