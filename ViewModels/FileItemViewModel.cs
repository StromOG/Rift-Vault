using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RiftVault.Core;
using RiftVault.Models;
using RiftVault.UI.Converters;

namespace RiftVault.ViewModels
{
    public partial class FileItemViewModel : ObservableObject
    {
        private readonly IThumbnailCache _thumbnailCache;
        private CancellationTokenSource? _thumbnailCts;
        private TypeVisualInfo? _cachedVisualInfo;

        [ObservableProperty]
        private FileItem _model;

        [ObservableProperty]
        private BitmapSource? _thumbnail;

        [ObservableProperty]
        private bool _isThumbnailLoaded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isRenaming;

        [ObservableProperty]
        private string _renameText = string.Empty;

        public FileItemViewModel(FileItem model, IThumbnailCache thumbnailCache)
        {
            _model = model;
            _thumbnailCache = thumbnailCache;
        }

        public TypeVisualInfo VisualInfo => _cachedVisualInfo ??= FileIconGlyphConverter.GetVisualInfo(Model.IsDirectory ? Model.Name : Model.Extension, Model.IsDirectory);

        /// <summary>
        /// Display name without file extension for clean typography hierarchy.
        /// </summary>
        public string BaseName
        {
            get
            {
                if (Model.IsDirectory) return Model.Name;
                if (string.IsNullOrEmpty(Model.Extension)) return Model.Name;
                if (Model.Name.StartsWith('.') && Model.Name.IndexOf('.', 1) < 0) return Model.Name; // e.g. .gitignore, .env
                try
                {
                    string withoutExt = Path.GetFileNameWithoutExtension(Model.Name);
                    return string.IsNullOrEmpty(withoutExt) ? Model.Name : withoutExt;
                }
                catch
                {
                    return Model.Name;
                }
            }
        }

        /// <summary>
        /// Explicit file extension (e.g., ".cs", ".json", ".png") or empty for directories.
        /// </summary>
        public string ExtensionDisplay => Model.IsDirectory ? "" : (Model.Extension ?? "");

        /// <summary>
        /// Uppercase clean 2-4 letter tag pill (e.g., CS, TS, PY, PNG, ZIP, GIT, NPM).
        /// </summary>
        public string BadgeText => VisualInfo.Badge;

        /// <summary>
        /// Whether this item has a badge pill to display.
        /// </summary>
        public bool HasBadge => !string.IsNullOrEmpty(BadgeText);

        /// <summary>
        /// Segoe Fluent Icons glyph for this file/folder type.
        /// </summary>
        public string IconGlyph => VisualInfo.Glyph;

        /// <summary>
        /// Professional human-readable type description (e.g., "C# Source File", "Git Repository", "PNG Image").
        /// </summary>
        public string TypeDescription => VisualInfo.Description;

        /// <summary>
        /// Semantic category for broad theming (Folder, Code, Image, Video, Audio, Document, Archive, Executable, Vault).
        /// </summary>
        public string Category => VisualInfo.Category;

        /// <summary>
        /// Distinct category hex color string.
        /// </summary>
        public string CategoryColor => VisualInfo.ColorHex;

        private SolidColorBrush? _itemBrush;
        public SolidColorBrush ItemColorBrush
        {
            get
            {
                if (_itemBrush == null)
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(CategoryColor);
                        _itemBrush = new SolidColorBrush(color);
                        _itemBrush.Freeze();
                    }
                    catch
                    {
                        _itemBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                        _itemBrush.Freeze();
                    }
                }
                return _itemBrush;
            }
        }

        private SolidColorBrush? _badgeBgBrush;
        public SolidColorBrush BadgeBackgroundBrush
        {
            get
            {
                if (_badgeBgBrush == null)
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(CategoryColor);
                        // 15% opacity tint
                        _badgeBgBrush = new SolidColorBrush(Color.FromArgb(38, c.R, c.G, c.B));
                        _badgeBgBrush.Freeze();
                    }
                    catch
                    {
                        _badgeBgBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                        _badgeBgBrush.Freeze();
                    }
                }
                return _badgeBgBrush;
            }
        }

        private SolidColorBrush? _badgeBorderBrush;
        public SolidColorBrush BadgeBorderBrush
        {
            get
            {
                if (_badgeBorderBrush == null)
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(CategoryColor);
                        // 40% opacity border
                        _badgeBorderBrush = new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B));
                        _badgeBorderBrush.Freeze();
                    }
                    catch
                    {
                        _badgeBorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                        _badgeBorderBrush.Freeze();
                    }
                }
                return _badgeBorderBrush;
            }
        }

        /// <summary>
        /// Secondary detail subtext for grid/tile cards (e.g., "35.8 KB · CS" or "Git Repository").
        /// </summary>
        public string SecondaryDetail
        {
            get
            {
                if (Model.IsDirectory)
                {
                    return TypeDescription != "File folder" ? TypeDescription : "Folder";
                }
                return string.IsNullOrEmpty(DisplaySize) ? BadgeText : $"{DisplaySize} · {BadgeText}";
            }
        }

        /// <summary>
        /// True if this item starts with '.' or has the Windows Hidden attribute.
        /// </summary>
        public bool IsHiddenItem
        {
            get
            {
                if (Model.Name.StartsWith('.')) return true;
                try
                {
                    if (File.Exists(Model.Path))
                        return (File.GetAttributes(Model.Path) & FileAttributes.Hidden) == FileAttributes.Hidden;
                    if (Directory.Exists(Model.Path))
                        return (new DirectoryInfo(Model.Path).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Relative date string for display (e.g., "2 hours ago", "Yesterday").
        /// </summary>
        public string DisplayDate
        {
            get
            {
                var diff = DateTime.Now - Model.DateModified;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 2) return "Yesterday";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
                if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
                return Model.DateModified.ToString("MMM d, yyyy");
            }
        }

        public string DateGroup
        {
            get
            {
                var date = Model.DateModified.Date;
                var today = DateTime.Today;
                if (date == today) return "Today";
                if (date == today.AddDays(-1)) return "Yesterday";
                return date.ToString("dd MMMM yyyy");
            }
        }

        /// <summary>
        /// Formatted file size for display.
        /// </summary>
        public string DisplaySize
        {
            get
            {
                if (Model.IsDirectory) return "";
                long bytes = Model.Size;
                if (bytes <= 0) return "0 B";
                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double size = bytes;
                while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
                return $"{size:0.#} {suffixes[order]}";
            }
        }

        #region Content-Aware Category Predicates (Context Menus & Actions)
        /// <summary>
        /// True if this item is a folder / directory.
        /// </summary>
        public bool IsDirectory => Model.IsDirectory;

        /// <summary>
        /// True if this item is a regular file.
        /// </summary>
        public bool IsFile => !Model.IsDirectory;

        /// <summary>
        /// True if this item is an image file (e.g. .png, .jpg, .jpeg, .webp, .bmp, .gif, .tiff, .ico).
        /// </summary>
        public bool IsImage
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Image", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".tif" or ".ico" or ".svg" or ".heic";
            }
        }

        /// <summary>
        /// True if this item is a video file.
        /// </summary>
        public bool IsVideo
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Video", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".flv" or ".m4v" or ".3gp";
            }
        }

        /// <summary>
        /// True if this item is an audio file.
        /// </summary>
        public bool IsAudio
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Audio", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" or ".opus";
            }
        }

        /// <summary>
        /// True if media (image, video, or audio).
        /// </summary>
        public bool IsMedia => IsImage || IsVideo || IsAudio;

        /// <summary>
        /// True if compressed archive.
        /// </summary>
        public bool IsArchive
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Archive", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2" or ".xz" or ".cab" or ".iso";
            }
        }

        /// <summary>
        /// True if code or editable text document.
        /// </summary>
        public bool IsCodeOrText
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Code", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(Category, "Document", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".txt" or ".md" or ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".xml" or ".c" or ".cpp" or ".h" or ".hpp" or ".java" or ".sql" or ".yaml" or ".yml" or ".sh" or ".bat" or ".ps1" or ".cmd" or ".log" or ".ini" or ".cfg";
            }
        }

        /// <summary>
        /// True if Windows executable or script installer.
        /// </summary>
        public bool IsExecutable
        {
            get
            {
                if (Model.IsDirectory) return false;
                if (string.Equals(Category, "Executable", StringComparison.OrdinalIgnoreCase)) return true;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1";
            }
        }

        /// <summary>
        /// True if file format is supported to set directly as Windows desktop background.
        /// </summary>
        public bool CanSetAsWallpaper
        {
            get
            {
                if (!IsImage) return false;
                string ext = (Model.Extension ?? "").ToLowerInvariant();
                return ext is ".jpg" or ".jpeg" or ".png" or ".bmp";
            }
        }
        #endregion

        public void LoadThumbnail(int size)
        {
            if (IsThumbnailLoaded || Model.IsDirectory) return;

            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            var ct = _thumbnailCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    var bmp = await _thumbnailCache.GetThumbnailAsync(Model.Path, size, ct);
                    if (bmp != null && !ct.IsCancellationRequested)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            Thumbnail = bmp;
                            IsThumbnailLoaded = true;
                        });
                    }
                }
                catch { /* Ignore thumbnail load failures */ }
            }, ct);
        }

        public void CancelThumbnailLoad()
        {
            _thumbnailCts?.Cancel();
        }

        public void BeginRename()
        {
            RenameText = Model.Name;
            IsRenaming = true;
        }

        public void CommitRename()
        {
            IsRenaming = false;
        }

        public void CancelRename()
        {
            IsRenaming = false;
            RenameText = Model.Name;
        }
    }
}