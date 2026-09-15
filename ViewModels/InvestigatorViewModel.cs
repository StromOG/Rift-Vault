using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.Core;
using RiftVault.UI.Converters;

namespace RiftVault.ViewModels
{
    public class HexLine
    {
        public string Offset { get; set; } = string.Empty;
        public string HexBytes { get; set; } = string.Empty;
        public string Ascii { get; set; } = string.Empty;
    }

    public class ExtensionUsageItem
    {
        public string Extension { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public string DisplaySize { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#38BDF8";
    }

    public class LargestFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public string DisplaySize { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE7C3";
        public string ColorHex { get; set; } = "#38BDF8";
    }

    public class SuspiciousFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning"; // Critical, Warning, Info
        public string ColorHex { get; set; } = "#F59E0B";
        public string IconGlyph { get; set; } = "\uE7BA";
        public string DisplaySize { get; set; } = string.Empty;
    }

    public class ExtractedStringItem
    {
        public string Value { get; set; } = string.Empty;
        public string Category { get; set; } = "General"; // URL, Path, Registry, General
        public string EncodingType { get; set; } = "ASCII"; // ASCII, UTF-16
        public string CategoryColorHex { get; set; } = "#94A3B8";
    }

    public class ManifestFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string DisplaySize { get; set; } = string.Empty;
        public string Sha256 { get; set; } = "Computing...";
        public string StatusIcon { get; set; } = "\uE73E";
    }

    public partial class InvestigatorViewModel : ObservableObject
    {
        private readonly IChecksumService _checksumService;
        private readonly IThumbnailCache _thumbnailCache;
        private CancellationTokenSource? _analysisCts;
        private CancellationTokenSource? _manifestCts;

        [ObservableProperty] private string _targetPath = string.Empty;
        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _baseName = string.Empty;
        [ObservableProperty] private string _extension = string.Empty;
        [ObservableProperty] private string _badgeText = "FILE";
        [ObservableProperty] private string _typeDescription = "General File";
        [ObservableProperty] private string _iconGlyph = "\uE7C3";
        [ObservableProperty] private string _categoryColorHex = "#38BDF8";
        [ObservableProperty] private bool _isDirectory;
        [ObservableProperty] private BitmapSource? _thumbnail;
        [ObservableProperty] private bool _isThumbnailLoaded;

        // Active Navigation Mode: Properties, Forensics, Hex, Hashes, Storage
        [ObservableProperty] private string _activeTab = "Properties";

        // Properties Mode fields
        [ObservableProperty] private string _fileSizeFormatted = string.Empty;
        [ObservableProperty] private string _exactBytesFormatted = string.Empty;
        [ObservableProperty] private string _parentDirectory = string.Empty;
        [ObservableProperty] private string _rootDrive = string.Empty;
        [ObservableProperty] private string _dateCreatedFormatted = string.Empty;
        [ObservableProperty] private string _dateModifiedFormatted = string.Empty;
        [ObservableProperty] private string _dateAccessedFormatted = string.Empty;
        [ObservableProperty] private bool _isReadOnly;
        [ObservableProperty] private bool _isHidden;
        [ObservableProperty] private bool _isSystem;
        [ObservableProperty] private bool _isArchive;
        [ObservableProperty] private bool _isCompressed;
        [ObservableProperty] private bool _isEncrypted;
        [ObservableProperty] private string _ownerName = "Unknown";
        [ObservableProperty] private string _permissionsSummary = string.Empty;
        [ObservableProperty] private string _attributesStatusMessage = string.Empty;

        // Content Specifics (Lines, Images, etc.)
        [ObservableProperty] private string _contentMetric1Label = string.Empty;
        [ObservableProperty] private string _contentMetric1Value = string.Empty;
        [ObservableProperty] private string _contentMetric2Label = string.Empty;
        [ObservableProperty] private string _contentMetric2Value = string.Empty;
        [ObservableProperty] private string _contentMetric3Label = string.Empty;
        [ObservableProperty] private string _contentMetric3Value = string.Empty;
        [ObservableProperty] private bool _hasContentMetrics;

        // Forensics & Investigation Mode fields (Single File)
        [ObservableProperty] private string _magicHeaderHex = string.Empty;
        [ObservableProperty] private string _magicHeaderAscii = string.Empty;
        [ObservableProperty] private string _detectedFormatFromMagic = string.Empty;
        [ObservableProperty] private bool _isExtensionDisguised;
        [ObservableProperty] private string _disguiseSeverity = "Verified"; // Critical, Warning, Verified
        [ObservableProperty] private string _disguiseWarningMessage = string.Empty;
        [ObservableProperty] private double _shannonEntropy;
        [ObservableProperty] private string _entropyAssessment = string.Empty;
        [ObservableProperty] private string _entropyColorHex = "#34D399";
        [ObservableProperty] private string _entropyNullBytesPercent = "0.0%";
        [ObservableProperty] private string _entropyAsciiBytesPercent = "0.0%";
        [ObservableProperty] private string _entropyHighBytesPercent = "0.0%";

        // Printable Strings Extractor (ASCII + UTF-16 Unicode)
        [ObservableProperty] private ObservableCollection<ExtractedStringItem> _extractedStringItems = new();
        [ObservableProperty] private string _stringFilterQuery = string.Empty;
        [ObservableProperty] private string _selectedStringCategory = "All";
        [ObservableProperty] private string _totalStringsFoundText = "0 strings extracted";
        private List<ExtractedStringItem> _allExtractedStrings = new();

        // Binary Hex Dump fields
        [ObservableProperty] private ObservableCollection<HexLine> _hexLines = new();
        [ObservableProperty] private int _currentHexPage = 1;
        [ObservableProperty] private int _totalHexPages = 1;
        [ObservableProperty] private string _hexPageStatus = "Page 1 of 1";
        [ObservableProperty] private string _jumpToOffsetInput = string.Empty;
        [ObservableProperty] private string _hexSearchQuery = string.Empty;
        [ObservableProperty] private string _hexSearchStatus = string.Empty;
        private const int HexPageSize = 512; // 32 lines of 16 bytes

        // Checksums & Hashes fields
        [ObservableProperty] private string _md5Hash = "Computing...";
        [ObservableProperty] private string _sha1Hash = "Computing...";
        [ObservableProperty] private string _sha256Hash = "Computing...";
        [ObservableProperty] private string _sha512Hash = "Computing...";
        [ObservableProperty] private bool _isHashingComplete;
        [ObservableProperty] private string _compareHashInput = string.Empty;
        [ObservableProperty] private string _hashMatchStatus = string.Empty;
        [ObservableProperty] private string _hashMatchColorHex = "#94A3B8";

        // Deep Folder & Storage Analysis fields (for directories)
        [ObservableProperty] private int _totalSubFiles;
        [ObservableProperty] private int _totalSubFolders;
        [ObservableProperty] private long _totalDirectoryBytes;
        [ObservableProperty] private string _totalDirectorySizeFormatted = string.Empty;
        [ObservableProperty] private int _emptyDirectoriesCount;
        [ObservableProperty] private int _deepestNestingLevel;
        [ObservableProperty] private int _disguisedFilesCount;
        [ObservableProperty] private int _hiddenFilesCount;
        [ObservableProperty] private int _systemFilesCount;
        [ObservableProperty] private string _folderAuditStatus = "Checking folder safety...";
        [ObservableProperty] private string _folderAuditStatusColor = "#38BDF8";
        [ObservableProperty] private bool _hasSuspiciousFiles;
        [ObservableProperty] private bool _isStorageAnalysisBusy;
        [ObservableProperty] private ObservableCollection<SuspiciousFileItem> _suspiciousFiles = new();
        [ObservableProperty] private ObservableCollection<ExtensionUsageItem> _extensionDistribution = new();
        [ObservableProperty] private ObservableCollection<LargestFileItem> _largestFiles = new();

        // Directory Integrity Manifest fields
        [ObservableProperty] private ObservableCollection<ManifestFileItem> _manifestFiles = new();
        [ObservableProperty] private bool _isManifestBusy;
        [ObservableProperty] private string _manifestStatusText = string.Empty;

        // Commands
        public RelayCommand<string> SelectTabCommand { get; }
        public RelayCommand NextHexPageCommand { get; }
        public RelayCommand PrevHexPageCommand { get; }
        public RelayCommand FirstHexPageCommand { get; }
        public RelayCommand LastHexPageCommand { get; }
        public RelayCommand JumpToHexOffsetCommand { get; }
        public RelayCommand SearchHexCommand { get; }
        public RelayCommand CopyHexPageCommand { get; }
        public RelayCommand ApplyAttributesCommand { get; }
        public RelayCommand<string> CopyCommand { get; }
        public RelayCommand CopyForensicReportCommand { get; }
        public RelayCommand CopyAllStringsCommand { get; }
        public RelayCommand<string> SelectStringCategoryCommand { get; }
        public RelayCommand<string> InspectFileItemCommand { get; }
        public RelayCommand GenerateManifestCommand { get; }
        public RelayCommand CopyManifestCommand { get; }

        public InvestigatorViewModel(IChecksumService checksumService, IThumbnailCache thumbnailCache)
        {
            _checksumService = checksumService;
            _thumbnailCache = thumbnailCache;

            SelectTabCommand = new RelayCommand<string>(tab =>
            {
                if (!string.IsNullOrEmpty(tab)) ActiveTab = tab;
            });

            NextHexPageCommand = new RelayCommand(NextHexPage);
            PrevHexPageCommand = new RelayCommand(PrevHexPage);
            FirstHexPageCommand = new RelayCommand(() => LoadHexPage(1));
            LastHexPageCommand = new RelayCommand(() => LoadHexPage(TotalHexPages));
            JumpToHexOffsetCommand = new RelayCommand(JumpToHexOffset);
            SearchHexCommand = new RelayCommand(SearchHex);
            CopyHexPageCommand = new RelayCommand(CopyHexPage);

            ApplyAttributesCommand = new RelayCommand(() => ApplyAttributes(IsReadOnly, IsHidden));
            CopyCommand = new RelayCommand<string>(text =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    try { System.Windows.Clipboard.SetText(text); } catch { }
                }
            });

            CopyForensicReportCommand = new RelayCommand(CopyForensicReport);
            CopyAllStringsCommand = new RelayCommand(CopyAllStrings);
            SelectStringCategoryCommand = new RelayCommand<string>(cat =>
            {
                SelectedStringCategory = cat ?? "All";
                FilterExtractedStrings();
            });

            InspectFileItemCommand = new RelayCommand<string>(path =>
            {
                if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                {
                    _ = LoadTargetAsync(path, "Forensics");
                }
            });

            GenerateManifestCommand = new RelayCommand(() => _ = GenerateDirectoryManifestAsync());
            CopyManifestCommand = new RelayCommand(CopyDirectoryManifest);
        }

        public async Task LoadTargetAsync(string path, string initialTab = "Properties")
        {
            // Normalize path
            if (string.IsNullOrEmpty(path) || path.Equals("This PC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
                    path = drive?.RootDirectory.FullName ?? "C:\\";
                }
                catch
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }

            TargetPath = path;
            IsDirectory = Directory.Exists(path);

            // Intelligently select default tab
            if (IsDirectory)
            {
                ActiveTab = initialTab == "Hex" ? "Storage" : initialTab;
            }
            else
            {
                ActiveTab = initialTab == "Storage" ? "Properties" : initialTab;
            }

            _analysisCts?.Cancel();
            _analysisCts = new CancellationTokenSource();
            var ct = _analysisCts.Token;

            FileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(FileName)) FileName = path;

            var visual = FileIconGlyphConverter.GetVisualInfo(IsDirectory ? FileName : Path.GetExtension(path), IsDirectory);
            IconGlyph = visual.Glyph;
            CategoryColorHex = visual.ColorHex;
            TypeDescription = visual.Description;
            BadgeText = IsDirectory ? (Path.GetPathRoot(path) == path ? "DRIVE" : "DIR") : visual.Badge;
            BaseName = IsDirectory ? FileName : (Path.GetFileNameWithoutExtension(path) ?? FileName);
            Extension = IsDirectory ? "" : (Path.GetExtension(path) ?? "");

            LoadBasicProperties();

            // Load high-res thumbnail
            Thumbnail = null;
            IsThumbnailLoaded = false;
            if (!IsDirectory)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bmp = await _thumbnailCache.GetThumbnailAsync(path, 128, ct);
                        if (bmp != null)
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                Thumbnail = bmp;
                                IsThumbnailLoaded = true;
                            });
                        }
                    }
                    catch { }
                }, ct);
            }

            if (IsDirectory)
            {
                var folderTask = AnalyzeFolderDeepAsync(ct);
                var manifestTask = GenerateDirectoryManifestAsync();
                await Task.WhenAll(folderTask, manifestTask);
            }
            else
            {
                LoadHexPage(1);
                var forensicsTask = AnalyzeFileForensicsAsync(ct);
                var hashesTask = ComputeHashesAsync(ct);
                await Task.WhenAll(forensicsTask, hashesTask);
            }
        }

        private void LoadBasicProperties()
        {
            try
            {
                if (IsDirectory)
                {
                    var dir = new DirectoryInfo(TargetPath);
                    ParentDirectory = dir.Parent?.FullName ?? "";
                    RootDrive = Path.GetPathRoot(TargetPath) ?? "";
                    DateCreatedFormatted = dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    DateModifiedFormatted = dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    DateAccessedFormatted = dir.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");

                    IsReadOnly = dir.Attributes.HasFlag(FileAttributes.ReadOnly);
                    IsHidden = dir.Attributes.HasFlag(FileAttributes.Hidden);
                    IsSystem = dir.Attributes.HasFlag(FileAttributes.System);
                    IsArchive = dir.Attributes.HasFlag(FileAttributes.Archive);
                    IsCompressed = dir.Attributes.HasFlag(FileAttributes.Compressed);
                    IsEncrypted = dir.Attributes.HasFlag(FileAttributes.Encrypted);

                    FileSizeFormatted = "Directory";
                    ExactBytesFormatted = "-";

                    // Drive specifics if root
                    if (Path.GetPathRoot(TargetPath) == TargetPath)
                    {
                        try
                        {
                            var di = new DriveInfo(TargetPath);
                            if (di.IsReady)
                            {
                                ContentMetric1Label = "File System:";
                                ContentMetric1Value = di.DriveFormat;
                                ContentMetric2Label = "Free Space:";
                                ContentMetric2Value = FormatBytes(di.AvailableFreeSpace);
                                ContentMetric3Label = "Total Capacity:";
                                ContentMetric3Value = FormatBytes(di.TotalSize);
                                HasContentMetrics = true;
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    var file = new FileInfo(TargetPath);
                    ParentDirectory = file.DirectoryName ?? "";
                    RootDrive = Path.GetPathRoot(TargetPath) ?? "";
                    DateCreatedFormatted = file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    DateModifiedFormatted = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    DateAccessedFormatted = file.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");

                    IsReadOnly = file.Attributes.HasFlag(FileAttributes.ReadOnly);
                    IsHidden = file.Attributes.HasFlag(FileAttributes.Hidden);
                    IsSystem = file.Attributes.HasFlag(FileAttributes.System);
                    IsArchive = file.Attributes.HasFlag(FileAttributes.Archive);
                    IsCompressed = file.Attributes.HasFlag(FileAttributes.Compressed);
                    IsEncrypted = file.Attributes.HasFlag(FileAttributes.Encrypted);

                    long bytes = file.Length;
                    ExactBytesFormatted = $"{bytes:N0} bytes";
                    FileSizeFormatted = FormatBytes(bytes);

                    ReadContentMetrics(file);
                }

                // Security owner
                try
                {
                    if (File.Exists(TargetPath))
                    {
                        var fileInfo = new FileInfo(TargetPath);
                        var fs = fileInfo.GetAccessControl();
                        OwnerName = fs.GetOwner(typeof(NTAccount))?.ToString() ?? "NT AUTHORITY\\SYSTEM";
                    }
                    else if (Directory.Exists(TargetPath))
                    {
                        var dirInfo = new DirectoryInfo(TargetPath);
                        var ds = dirInfo.GetAccessControl();
                        OwnerName = ds.GetOwner(typeof(NTAccount))?.ToString() ?? "NT AUTHORITY\\SYSTEM";
                    }
                }
                catch
                {
                    OwnerName = Environment.UserName;
                }

                PermissionsSummary = "Full Access (Read, Write, Execute, Modify)";
                AttributesStatusMessage = "";
            }
            catch (Exception ex)
            {
                AttributesStatusMessage = $"Properties read note: {ex.Message}";
            }
        }

        private void ReadContentMetrics(FileInfo file)
        {
            string ext = file.Extension.ToLowerInvariant();
            HasContentMetrics = false;

            try
            {
                // Code & Text Files
                if (ext is ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".json" or ".xml" or ".xaml" or ".md" or ".txt" or ".sql" or ".html" or ".css" or ".csproj")
                {
                    if (file.Length < 10 * 1024 * 1024) // < 10MB safe reading
                    {
                        int lines = 0;
                        int words = 0;
                        using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(fs);
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lines++;
                            words += line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        }

                        ContentMetric1Label = "Total Lines:";
                        ContentMetric1Value = $"{lines:N0} lines";
                        ContentMetric2Label = "Word Count:";
                        ContentMetric2Value = $"{words:N0} words";
                        ContentMetric3Label = "Encoding:";
                        ContentMetric3Value = "UTF-8 (CRLF/LF)";
                        HasContentMetrics = true;
                    }
                }
                // Image Files
                else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif" or ".ico")
                {
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        ContentMetric1Label = "Resolution:";
                        ContentMetric1Value = $"{frame.PixelWidth} × {frame.PixelHeight} px";
                        ContentMetric2Label = "Megapixels:";
                        ContentMetric2Value = $"{(frame.PixelWidth * frame.PixelHeight / 1000000.0):0.1} MP";
                        ContentMetric3Label = "Color Depth:";
                        ContentMetric3Value = $"{frame.Format.BitsPerPixel} bpp";
                        HasContentMetrics = true;
                    }
                }
                // Executables & DLLs
                else if (ext is ".exe" or ".dll" or ".sys")
                {
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    byte[] header = new byte[1024];
                    int read = stream.Read(header, 0, header.Length);
                    if (read > 64 && header[0] == 0x4D && header[1] == 0x5A)
                    {
                        int peOffset = BitConverter.ToInt32(header, 0x3C);
                        if (peOffset > 0 && peOffset + 24 < read)
                        {
                            ushort machine = BitConverter.ToUInt16(header, peOffset + 4);
                            string arch = machine switch
                            {
                                0x8664 => "x64 (AMD64 / EM64T)",
                                0x014c => "x86 (32-bit i386)",
                                0xaa64 => "ARM64 (AArch64)",
                                0x01c4 => "ARMv7 Thumb",
                                _ => $"Architecture (0x{machine:X4})"
                            };

                            ContentMetric1Label = "Architecture:";
                            ContentMetric1Value = arch;
                            ContentMetric2Label = "Binary Subsystem:";
                            ushort subsystem = peOffset + 68 < read ? BitConverter.ToUInt16(header, peOffset + 68) : (ushort)0;
                            ContentMetric2Value = subsystem == 2 ? "Windows GUI" : subsystem == 3 ? "Windows CUI (Console)" : "PE Native";
                            ContentMetric3Label = "PE Header Offset:";
                            ContentMetric3Value = $"0x{peOffset:X4}";
                            HasContentMetrics = true;
                        }
                    }
                }
            }
            catch { }
        }

        public void ApplyAttributes(bool readOnly, bool hidden)
        {
            try
            {
                var attrs = File.GetAttributes(TargetPath);
                if (readOnly) attrs |= FileAttributes.ReadOnly; else attrs &= ~FileAttributes.ReadOnly;
                if (hidden) attrs |= FileAttributes.Hidden; else attrs &= ~FileAttributes.Hidden;
                File.SetAttributes(TargetPath, attrs);

                IsReadOnly = readOnly;
                IsHidden = hidden;
                AttributesStatusMessage = "Attributes applied successfully.";
            }
            catch (Exception ex)
            {
                AttributesStatusMessage = $"Failed to set attributes: {ex.Message}";
            }
        }

        #region Forensics & Magic Numbers
        private async Task AnalyzeFileForensicsAsync(CancellationToken ct)
        {
            if (!File.Exists(TargetPath)) return;

            await Task.Run(() =>
            {
                try
                {
                    using var fs = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    byte[] buffer = new byte[Math.Min(fs.Length, 64)];
                    int read = fs.Read(buffer, 0, buffer.Length);

                    // Magic Bytes Header
                    var hexSb = new StringBuilder();
                    var asciiSb = new StringBuilder();
                    for (int i = 0; i < Math.Min(read, 16); i++)
                    {
                        hexSb.Append($"{buffer[i]:X2} ");
                        char c = (char)buffer[i];
                        asciiSb.Append(char.IsControl(c) ? '.' : c);
                    }

                    MagicHeaderHex = hexSb.ToString().Trim();
                    MagicHeaderAscii = asciiSb.ToString();

                    // Detect format from magic bytes
                    var (detectedType, detectedExt) = DetectFormatFromBytes(buffer, TargetPath);
                    DetectedFormatFromMagic = detectedType;

                    // Comprehensive Threat & Disguise Checks
                    EvaluateFileDisguise(detectedExt, TargetPath);

                    // Calculate Shannon Entropy & distribution metrics
                    CalculateEntropy(fs);

                    // Extract printable strings (both ASCII and UTF-16)
                    ExtractStrings(fs);
                }
                catch { }
            }, ct);
        }

        private void EvaluateFileDisguise(string detectedExt, string path)
        {
            string fileName = Path.GetFileName(path);
            string actualExt = Path.GetExtension(path).ToLowerInvariant();

            // 1. Check for Right-to-Left Override (RLO) character
            if (fileName.Contains('\u202E'))
            {
                IsExtensionDisguised = true;
                DisguiseSeverity = "Critical";
                DisguiseWarningMessage = "⚠️ Warning: File name contains hidden characters that can reverse the visible file extension.";
                return;
            }

            // 2. Check for double extension deception (e.g. document.pdf.exe)
            var doubleExtMatch = Regex.Match(fileName, @"\.(pdf|docx?|xlsx?|pptx?|jpe?g|png|gif|mp4|mp3|txt|csv)\.(exe|dll|scr|bat|cmd|vbs|ps1|hta|wsf)$", RegexOptions.IgnoreCase);
            if (doubleExtMatch.Success)
            {
                IsExtensionDisguised = true;
                DisguiseSeverity = "Critical";
                DisguiseWarningMessage = $"⚠️ Warning: This file has a double extension ({doubleExtMatch.Groups[1].Value}.{doubleExtMatch.Groups[2].Value}) and may be hiding its true type.";
                return;
            }

            // 3. Check for Executable header disguised under non-executable extension
            if (!string.IsNullOrEmpty(detectedExt) && !string.IsNullOrEmpty(actualExt))
            {
                if ((detectedExt == ".exe" || detectedExt == ".elf") && actualExt != ".exe" && actualExt != ".dll" && actualExt != ".sys" && actualExt != ".scr" && actualExt != ".ocx")
                {
                    IsExtensionDisguised = true;
                    DisguiseSeverity = "Critical";
                    DisguiseWarningMessage = $"⚠️ Warning: This file is named as '{actualExt}', but its real format is an application ({detectedExt}). Opening it will run program code.";
                    return;
                }

                if (detectedExt != actualExt && !IsCompatibleExtension(detectedExt, actualExt))
                {
                    IsExtensionDisguised = true;
                    DisguiseSeverity = "Warning";
                    DisguiseWarningMessage = $"⚠️ Note: The file's internal format ({detectedExt}) does not match its file extension ({actualExt}).";
                    return;
                }
            }

            IsExtensionDisguised = false;
            DisguiseSeverity = "Verified";
            DisguiseWarningMessage = $"✅ Verified: File contents match the '{actualExt}' extension.";
        }

        private static (string Name, string Ext) DetectFormatFromBytes(byte[] b, string filePath)
        {
            if (b.Length == 0) return ("Empty File", "");

            // 1. Executables & Binaries
            if (b.Length >= 2 && b[0] == 0x4D && b[1] == 0x5A)
                return ("Windows PE Executable / DLL (MZ)", ".exe");
            if (b.Length >= 4 && b[0] == 0x7F && b[1] == 0x45 && b[2] == 0x4C && b[3] == 0x46)
                return ("Linux ELF Binary", ".elf");
            if (b.Length >= 4 && ((b[0] == 0xFE && b[1] == 0xED && b[2] == 0xFA && (b[3] == 0xCE || b[3] == 0xCF)) ||
                                  (b[0] == 0xCF && b[1] == 0xFA && b[2] == 0xED && b[3] == 0xFE)))
                return ("macOS Mach-O Binary", ".macho");
            if (b.Length >= 4 && b[0] == 0xCA && b[1] == 0xFE && b[2] == 0xBA && b[3] == 0xBE)
                return ("Java Bytecode Class", ".class");
            if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x61 && b[2] == 0x73 && b[3] == 0x6D)
                return ("WebAssembly Binary (WASM)", ".wasm");
            if (b.Length >= 8 && b[0] == 0x4C && b[1] == 0x00 && b[2] == 0x00 && b[3] == 0x00 && b[4] == 0x01 && b[5] == 0x14)
                return ("Windows Shell Link Shortcut", ".lnk");

            // 2. Images & Graphics
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
                return ("PNG Raster Image", ".png");
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("JPEG Photograph", ".jpg");
            if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38)
                return ("GIF Animation", ".gif");
            if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
                return ("WebP Vector/Raster Image", ".webp");
            if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D)
                return ("Windows Bitmap (BMP)", ".bmp");
            if (b.Length >= 4 && ((b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) || (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A)))
                return ("TIFF Image", ".tiff");
            if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && (b[2] == 0x01 || b[2] == 0x02) && b[3] == 0x00)
                return (b[2] == 0x01 ? "Windows Icon (ICO)" : "Windows Cursor (CUR)", ".ico");
            if (b.Length >= 4 && b[0] == 0x38 && b[1] == 0x42 && b[2] == 0x50 && b[3] == 0x53)
                return ("Photoshop Document (PSD)", ".psd");

            // 3. Audio & Video
            if (b.Length >= 8 && b[4] == 0x66 && b[5] == 0x74 && b[6] == 0x79 && b[7] == 0x70)
                return ("MPEG-4 Video / ISO Media Container", ".mp4");
            if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3)
                return ("Matroska / WebM Media Container", ".mkv");
            if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x41 && b[9] == 0x56 && b[10] == 0x49 && b[11] == 0x20)
                return ("Audio Video Interleave (AVI)", ".avi");
            if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x41 && b[10] == 0x56 && b[11] == 0x45)
                return ("Waveform Audio (WAV)", ".wav");
            if (b.Length >= 4 && b[0] == 0x66 && b[1] == 0x4C && b[2] == 0x61 && b[3] == 0x43)
                return ("Free Lossless Audio Codec (FLAC)", ".flac");
            if (b.Length >= 4 && b[0] == 0x4F && b[1] == 0x67 && b[2] == 0x67 && b[3] == 0x53)
                return ("Ogg Multimedia Container", ".ogg");
            if ((b.Length >= 3 && b[0] == 0x49 && b[1] == 0x44 && b[2] == 0x33) || (b.Length >= 2 && b[0] == 0xFF && (b[1] == 0xFB || b[1] == 0xF3 || b[1] == 0xF2)))
                return ("MP3 MPEG Audio", ".mp3");

            // 4. Documents & Archives
            if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46)
                return ("PDF Document", ".pdf");
            if (b.Length >= 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0 && b[4] == 0xA1 && b[5] == 0xB1 && b[6] == 0x1A && b[7] == 0xE1)
                return ("Microsoft Office Legacy (Compound CFBF/OLE)", ".doc");
            if (b.Length >= 4 && b[0] == 0x50 && b[1] == 0x4B && (b[2] == 0x03 || b[2] == 0x05 || b[2] == 0x07) && (b[3] == 0x04 || b[3] == 0x06 || b[3] == 0x08))
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is ".docx" or ".xlsx" or ".pptx") return ($"Microsoft Office OpenXML ({ext.TrimStart('.').ToUpperInvariant()})", ext);
                if (ext == ".jar") return ("Java Archive (JAR)", ".jar");
                if (ext == ".apk") return ("Android Package (APK)", ".apk");
                if (ext == ".nupkg") return ("NuGet Package (NUPKG)", ".nupkg");
                return ("ZIP Compressed Container", ".zip");
            }
            if (b.Length >= 6 && b[0] == 0x37 && b[1] == 0x7A && b[2] == 0xBC && b[3] == 0xAF && b[4] == 0x27 && b[5] == 0x1C)
                return ("7-Zip Compressed Archive", ".7z");
            if (b.Length >= 4 && b[0] == 0x52 && b[1] == 0x61 && b[2] == 0x72 && b[3] == 0x21)
                return ("WinRAR Archive", ".rar");
            if (b.Length >= 2 && b[0] == 0x1F && b[1] == 0x8B)
                return ("GZip Compressed Archive", ".gz");
            if (b.Length >= 3 && b[0] == 0x42 && b[1] == 0x5A && b[2] == 0x68)
                return ("BZip2 Compressed Archive", ".bz2");
            if (b.Length >= 6 && b[0] == 0xFD && b[1] == 0x37 && b[2] == 0x7A && b[3] == 0x58 && b[4] == 0x5A && b[5] == 0x00)
                return ("XZ Compressed Archive", ".xz");
            if (b.Length >= 5 && b[0] == 0x7B && b[1] == 0x5C && b[2] == 0x72 && b[3] == 0x74 && b[4] == 0x66)
                return ("Rich Text Format (RTF)", ".rtf");
            if (b.Length >= 16 && Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 16)).StartsWith("SQLite format 3"))
                return ("SQLite 3 Database", ".db");
            if (b.Length >= 5 && b[0] == 0x3C && b[1] == 0x3F && b[2] == 0x78 && b[3] == 0x6D && b[4] == 0x6C)
                return ("XML Extensible Markup", ".xml");

            // Text heuristic
            string asciiPrefix = Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 32)).TrimStart();
            if (asciiPrefix.StartsWith("{") || asciiPrefix.StartsWith("["))
                return ("JSON Structured Data", ".json");
            if (asciiPrefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase) || asciiPrefix.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
                return ("HTML Web Document", ".html");

            return ("General Binary / Data", "");
        }

        private static bool IsCompatibleExtension(string detectedExt, string actualExt)
        {
            if (detectedExt == ".zip" && actualExt is ".docx" or ".xlsx" or ".pptx" or ".jar" or ".apk" or ".nupkg" or ".cbz" or ".epub" or ".kmz") return true;
            if (detectedExt == ".jpg" && actualExt is ".jpeg" or ".jfif") return true;
            if (detectedExt == ".exe" && actualExt is ".dll" or ".sys" or ".scr" or ".ocx" or ".cpl" or ".efi") return true;
            if (detectedExt == ".mp4" && actualExt is ".m4v" or ".m4a" or ".mov") return true;
            if (detectedExt == ".mkv" && actualExt is ".webm") return true;
            if (detectedExt == ".doc" && actualExt is ".xls" or ".ppt" or ".msg" or ".ole") return true;
            if (detectedExt == ".xml" && actualExt is ".xaml" or ".csproj" or ".config" or ".svg" or ".props" or ".targets" or ".nuspec" or ".resx") return true;
            if (detectedExt == ".tar" && actualExt is ".tgz") return true;
            if (detectedExt == ".db" && actualExt is ".sqlite" or ".sqlite3") return true;
            return false;
        }

        private void CalculateEntropy(FileStream fs)
        {
            fs.Position = 0;
            long[] counts = new long[256];
            byte[] buf = new byte[65536];
            int read;
            long total = 0;

            long maxToScan = Math.Min(fs.Length, 4 * 1024 * 1024);
            while (total < maxToScan && (read = fs.Read(buf, 0, (int)Math.Min(buf.Length, maxToScan - total))) > 0)
            {
                for (int i = 0; i < read; i++) counts[buf[i]]++;
                total += read;
            }

            if (total == 0)
            {
                ShannonEntropy = 0;
                EntropyAssessment = "Empty file (No data)";
                EntropyColorHex = "#94A3B8";
                EntropyNullBytesPercent = "0.0%";
                EntropyAsciiBytesPercent = "0.0%";
                EntropyHighBytesPercent = "0.0%";
                return;
            }

            double entropy = 0;
            long nullBytes = counts[0];
            long asciiBytes = 0;
            long highBytes = 0;

            for (int i = 0; i < 256; i++)
            {
                if (i >= 32 && i <= 126) asciiBytes += counts[i];
                if (i > 127) highBytes += counts[i];

                if (counts[i] == 0) continue;
                double p = (double)counts[i] / total;
                entropy -= p * Math.Log2(p);
            }

            ShannonEntropy = Math.Round(entropy, 3);
            EntropyNullBytesPercent = $"{(double)nullBytes / total * 100.0:0.1}%";
            EntropyAsciiBytesPercent = $"{(double)asciiBytes / total * 100.0:0.1}%";
            EntropyHighBytesPercent = $"{(double)highBytes / total * 100.0:0.1}%";

            if (entropy < 3.5)
            {
                EntropyAssessment = "Low randomness (Text files, uncompressed data)";
                EntropyColorHex = "#38BDF8";
            }
            else if (entropy < 6.0)
            {
                EntropyAssessment = "Normal randomness (Documents, code, standard files)";
                EntropyColorHex = "#34D399";
            }
            else if (entropy < 7.2)
            {
                EntropyAssessment = "High randomness (Compressed files: zip, images, video)";
                EntropyColorHex = "#FBBF24";
            }
            else
            {
                EntropyAssessment = "Very high randomness (Encrypted or heavily compressed file)";
                EntropyColorHex = "#F87171";
            }
        }

        private void ExtractStrings(FileStream fs)
        {
            fs.Position = 0;
            byte[] buf = new byte[Math.Min(fs.Length, 2 * 1024 * 1024)];
            int read = fs.Read(buf, 0, buf.Length);

            var items = new List<ExtractedStringItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // 1. Extract 8-bit ASCII strings (min 4 chars)
            var asciiSb = new StringBuilder();
            for (int i = 0; i < read; i++)
            {
                char c = (char)buf[i];
                if (c >= 32 && c <= 126)
                {
                    asciiSb.Append(c);
                }
                else
                {
                    if (asciiSb.Length >= 4)
                    {
                        string str = asciiSb.ToString();
                        if (seen.Add(str))
                        {
                            items.Add(ClassifyString(str, "ASCII"));
                            if (items.Count >= 400) break;
                        }
                    }
                    asciiSb.Clear();
                }
            }

            // 2. Extract 16-bit UTF-16 LE strings (min 4 chars)
            if (items.Count < 400)
            {
                var unicodeSb = new StringBuilder();
                for (int i = 0; i < read - 1; i += 2)
                {
                    byte b1 = buf[i];
                    byte b2 = buf[i + 1];
                    if (b2 == 0 && b1 >= 32 && b1 <= 126)
                    {
                        unicodeSb.Append((char)b1);
                    }
                    else
                    {
                        if (unicodeSb.Length >= 4)
                        {
                            string str = unicodeSb.ToString();
                            if (seen.Add(str))
                            {
                                items.Add(ClassifyString(str, "UTF-16"));
                                if (items.Count >= 400) break;
                            }
                        }
                        unicodeSb.Clear();
                    }
                }
            }

            _allExtractedStrings = items;
            App.Current.Dispatcher.Invoke(() =>
            {
                TotalStringsFoundText = $"{items.Count} strings extracted";
                FilterExtractedStrings();
            });
        }

        private static ExtractedStringItem ClassifyString(string s, string encoding)
        {
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("www.", StringComparison.OrdinalIgnoreCase))
            {
                return new ExtractedStringItem { Value = s, Category = "URL", EncodingType = encoding, CategoryColorHex = "#38BDF8" };
            }

            if (Regex.IsMatch(s, @"^[A-Za-z]:\\|^\\Windows\\|^\\System32\\|^/usr/|^/etc/", RegexOptions.IgnoreCase))
            {
                return new ExtractedStringItem { Value = s, Category = "Path", EncodingType = encoding, CategoryColorHex = "#34D399" };
            }

            if (s.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase) || s.Contains("SOFTWARE\\", StringComparison.OrdinalIgnoreCase) || s.Contains("CurrentVersion", StringComparison.OrdinalIgnoreCase))
            {
                return new ExtractedStringItem { Value = s, Category = "Registry", EncodingType = encoding, CategoryColorHex = "#FBBF24" };
            }

            return new ExtractedStringItem { Value = s, Category = "General", EncodingType = encoding, CategoryColorHex = "#94A3B8" };
        }

        partial void OnStringFilterQueryChanged(string value) => FilterExtractedStrings();

        private void FilterExtractedStrings()
        {
            var filtered = _allExtractedStrings.AsEnumerable();

            if (SelectedStringCategory != "All")
            {
                filtered = filtered.Where(x => x.Category.Equals(SelectedStringCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(StringFilterQuery))
            {
                filtered = filtered.Where(x => x.Value.Contains(StringFilterQuery, StringComparison.OrdinalIgnoreCase));
            }

            ExtractedStringItems = new ObservableCollection<ExtractedStringItem>(filtered);
        }

        private void CopyAllStrings()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var item in ExtractedStringItems)
                {
                    sb.AppendLine($"[{item.Category}][{item.EncodingType}] {item.Value}");
                }
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch { }
        }
        #endregion

        #region Binary Hex Dump
        public void LoadHexPage(int page)
        {
            if (IsDirectory || !File.Exists(TargetPath)) return;

            try
            {
                using var fs = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long length = fs.Length;
                TotalHexPages = Math.Max(1, (int)Math.Ceiling((double)length / HexPageSize));
                CurrentHexPage = Math.Clamp(page, 1, TotalHexPages);
                HexPageStatus = $"Page {CurrentHexPage:N0} of {TotalHexPages:N0} ({length:N0} bytes total)";

                long offset = (CurrentHexPage - 1) * (long)HexPageSize;
                fs.Position = offset;

                byte[] buffer = new byte[HexPageSize];
                int read = fs.Read(buffer, 0, buffer.Length);

                var lines = new ObservableCollection<HexLine>();
                for (int i = 0; i < read; i += 16)
                {
                    var hexSb = new StringBuilder();
                    var asciiSb = new StringBuilder();

                    for (int j = 0; j < 16; j++)
                    {
                        if (i + j < read)
                        {
                            hexSb.Append($"{buffer[i + j]:X2} ");
                            char c = (char)buffer[i + j];
                            asciiSb.Append(c >= 32 && c <= 126 ? c : '.');
                        }
                        else
                        {
                            hexSb.Append("   ");
                        }
                        if (j == 7) hexSb.Append(" ");
                    }

                    lines.Add(new HexLine
                    {
                        Offset = (offset + i).ToString("X8"),
                        HexBytes = hexSb.ToString(),
                        Ascii = asciiSb.ToString()
                    });
                }

                HexLines = lines;
            }
            catch { }
        }

        public void NextHexPage() => LoadHexPage(CurrentHexPage + 1);
        public void PrevHexPage() => LoadHexPage(CurrentHexPage - 1);

        private void JumpToHexOffset()
        {
            if (string.IsNullOrWhiteSpace(JumpToOffsetInput) || !File.Exists(TargetPath)) return;

            try
            {
                string input = JumpToOffsetInput.Trim().ToLowerInvariant();
                long targetOffset = 0;

                if (input.StartsWith("0x"))
                {
                    targetOffset = Convert.ToInt64(input.Substring(2), 16);
                }
                else if (input.StartsWith("p") && int.TryParse(input.Substring(1), out int pNum))
                {
                    LoadHexPage(pNum);
                    return;
                }
                else if (long.TryParse(input, out long decOffset))
                {
                    targetOffset = decOffset;
                }

                int page = (int)(targetOffset / HexPageSize) + 1;
                LoadHexPage(page);
                HexSearchStatus = $"Jumped to offset 0x{targetOffset:X8} (Page {CurrentHexPage})";
            }
            catch
            {
                HexSearchStatus = "Invalid offset or page syntax.";
            }
        }

        private void SearchHex()
        {
            if (string.IsNullOrWhiteSpace(HexSearchQuery) || !File.Exists(TargetPath)) return;

            try
            {
                string query = HexSearchQuery.Trim();
                using var fs = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                byte[] pattern;
                // Check if query is hex bytes (e.g. "4D 5A" or "4D5A")
                string cleanedHex = query.Replace(" ", "").Replace("-", "");
                if (cleanedHex.Length % 2 == 0 && Regex.IsMatch(cleanedHex, @"\A\b[0-9a-fA-F]+\b\Z"))
                {
                    pattern = new byte[cleanedHex.Length / 2];
                    for (int i = 0; i < pattern.Length; i++)
                    {
                        pattern[i] = Convert.ToByte(cleanedHex.Substring(i * 2, 2), 16);
                    }
                }
                else
                {
                    pattern = Encoding.ASCII.GetBytes(query);
                }

                byte[] buf = new byte[65536];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    for (int i = 0; i <= bytesRead - pattern.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < pattern.Length; j++)
                        {
                            if (buf[i + j] != pattern[j]) { match = false; break; }
                        }

                        if (match)
                        {
                            long foundOffset = totalRead + i;
                            int page = (int)(foundOffset / HexPageSize) + 1;
                            LoadHexPage(page);
                            HexSearchStatus = $"Match found at offset 0x{foundOffset:X8} (Page {page})";
                            return;
                        }
                    }
                    totalRead += bytesRead;
                    if (totalRead > 50 * 1024 * 1024) break; // 50MB search boundary
                }

                HexSearchStatus = "Pattern not found in file.";
            }
            catch (Exception ex)
            {
                HexSearchStatus = $"Search error: {ex.Message}";
            }
        }

        private void CopyHexPage()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"--- Hex Dump: {FileName} (Page {CurrentHexPage} of {TotalHexPages}) ---");
                sb.AppendLine("OFFSET    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII DECODE");
                sb.AppendLine("--------------------------------------------------------------------------");
                foreach (var line in HexLines)
                {
                    sb.AppendLine($"{line.Offset}  {line.HexBytes,-48}  {line.Ascii}");
                }
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch { }
        }
        #endregion

        #region Checksums & Integrity
        private async Task ComputeHashesAsync(CancellationToken ct)
        {
            if (!File.Exists(TargetPath)) return;

            IsHashingComplete = false;
            Md5Hash = "Calculating...";
            Sha1Hash = "Calculating...";
            Sha256Hash = "Calculating...";
            Sha512Hash = "Calculating...";

            await Task.Run(async () =>
            {
                try
                {
                    var md5Task = _checksumService.CalculateMD5Async(TargetPath, ct);
                    var sha256Task = _checksumService.CalculateSHA256Async(TargetPath, ct);
                    var sha512Task = _checksumService.CalculateSHA512Async(TargetPath, ct);

                    var sha1Task = Task.Run(async () =>
                    {
                        using var sha1 = System.Security.Cryptography.SHA1.Create();
                        using var s = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
                        byte[] hash = await sha1.ComputeHashAsync(s, ct);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }, ct);

                    await Task.WhenAll(md5Task, sha1Task, sha256Task, sha512Task);

                    Md5Hash = await md5Task;
                    Sha1Hash = await sha1Task;
                    Sha256Hash = await sha256Task;
                    Sha512Hash = await sha512Task;
                    IsHashingComplete = true;

                    VerifyHashMatch();
                }
                catch
                {
                    Md5Hash = "Error reading file";
                    Sha1Hash = "Error reading file";
                    Sha256Hash = "Error reading file";
                    Sha512Hash = "Error reading file";
                }
            }, ct);
        }

        partial void OnCompareHashInputChanged(string value) => VerifyHashMatch();

        private void VerifyHashMatch()
        {
            string query = CompareHashInput.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                HashMatchStatus = "";
                HashMatchColorHex = "#94A3B8";
                return;
            }

            // Strip prefixes
            if (query.StartsWith("sha256:") || query.StartsWith("sha-256:")) query = query.Substring(query.IndexOf(':') + 1).Trim();
            if (query.StartsWith("md5:")) query = query.Substring(4).Trim();
            if (query.StartsWith("sha1:") || query.StartsWith("sha-1:")) query = query.Substring(query.IndexOf(':') + 1).Trim();
            if (query.StartsWith("sha512:") || query.StartsWith("sha-512:")) query = query.Substring(query.IndexOf(':') + 1).Trim();

            if (query.Equals(Md5Hash, StringComparison.OrdinalIgnoreCase))
            {
                HashMatchStatus = "✅ MATCH: Checksum perfectly matches computed MD5 hash!";
                HashMatchColorHex = "#34D399";
            }
            else if (query.Equals(Sha1Hash, StringComparison.OrdinalIgnoreCase))
            {
                HashMatchStatus = "✅ MATCH: Checksum perfectly matches computed SHA-1 hash!";
                HashMatchColorHex = "#34D399";
            }
            else if (query.Equals(Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                HashMatchStatus = "✅ MATCH: Checksum perfectly matches computed SHA-256 hash!";
                HashMatchColorHex = "#34D399";
            }
            else if (query.Equals(Sha512Hash, StringComparison.OrdinalIgnoreCase))
            {
                HashMatchStatus = "✅ MATCH: Checksum perfectly matches computed SHA-512 hash!";
                HashMatchColorHex = "#34D399";
            }
            else
            {
                HashMatchStatus = "❌ MISMATCH: Input checksum does not match any computed hash.";
                HashMatchColorHex = "#F87171";
            }
        }

        private void CopyForensicReport()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("==========================================================");
                sb.AppendLine("              RIFT VAULT FILE SUMMARY REPORT            ");
                sb.AppendLine("==========================================================");
                sb.AppendLine($"File Name:          {FileName}");
                sb.AppendLine($"Full Path:          {TargetPath}");
                sb.AppendLine($"Size:               {ExactBytesFormatted} ({FileSizeFormatted})");
                sb.AppendLine($"Type:               {TypeDescription} [{BadgeText}]");
                sb.AppendLine($"Owner:              {OwnerName}");
                sb.AppendLine($"Created:            {DateCreatedFormatted}");
                sb.AppendLine($"Modified:           {DateModifiedFormatted}");
                sb.AppendLine($"Accessed:           {DateAccessedFormatted}");
                sb.AppendLine();
                sb.AppendLine("--- FILE FORMAT & SAFETY CHECK ---");
                sb.AppendLine($"Detected Format:    {DetectedFormatFromMagic}");
                sb.AppendLine($"First Bytes (Hex):  {MagicHeaderHex}");
                sb.AppendLine($"First Bytes (Text): {MagicHeaderAscii}");
                sb.AppendLine($"Safety Status:      {DisguiseWarningMessage}");
                sb.AppendLine($"Data Randomness:    {ShannonEntropy:F3} / 8.000 ({EntropyAssessment})");
                sb.AppendLine($"Byte Distribution:  Null={EntropyNullBytesPercent}, ASCII={EntropyAsciiBytesPercent}, High={EntropyHighBytesPercent}");
                sb.AppendLine();
                sb.AppendLine("--- FILE CHECKSUMS ---");
                sb.AppendLine($"MD5:                {Md5Hash}");
                sb.AppendLine($"SHA-1:              {Sha1Hash}");
                sb.AppendLine($"SHA-256:            {Sha256Hash}");
                sb.AppendLine($"SHA-512:            {Sha512Hash}");
                sb.AppendLine("==========================================================");

                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch { }
        }
        #endregion

        #region Deep Folder & Storage Analysis
        private async Task AnalyzeFolderDeepAsync(CancellationToken ct)
        {
            if (!Directory.Exists(TargetPath)) return;

            IsStorageAnalysisBusy = true;
            FolderAuditStatus = "Checking files and calculating size...";
            FolderAuditStatusColor = "#38BDF8";

            await Task.Run(() =>
            {
                try
                {
                    var dir = new DirectoryInfo(TargetPath);
                    int fileCount = 0;
                    int dirCount = 0;
                    int emptyDirCount = 0;
                    long totalBytes = 0;
                    int maxDepth = 0;
                    int hiddenCount = 0;
                    int systemCount = 0;

                    var extStats = new Dictionary<string, (int Count, long Bytes)>(StringComparer.OrdinalIgnoreCase);
                    var filesList = new List<FileInfo>();
                    var suspiciousList = new List<SuspiciousFileItem>();

                    var dirsToVisit = new Stack<(DirectoryInfo Dir, int Depth)>();
                    dirsToVisit.Push((dir, 0));

                    while (dirsToVisit.Count > 0 && !ct.IsCancellationRequested)
                    {
                        var (current, depth) = dirsToVisit.Pop();
                        dirCount++;
                        if (depth > maxDepth) maxDepth = depth;

                        DirectoryInfo[] subDirs = Array.Empty<DirectoryInfo>();
                        FileInfo[] subFiles = Array.Empty<FileInfo>();

                        try
                        {
                            subDirs = current.GetDirectories();
                            subFiles = current.GetFiles();
                        }
                        catch { continue; }

                        if (subDirs.Length == 0 && subFiles.Length == 0)
                        {
                            emptyDirCount++;
                        }

                        foreach (var f in subFiles)
                        {
                            fileCount++;
                            totalBytes += f.Length;
                            filesList.Add(f);

                            if (f.Attributes.HasFlag(FileAttributes.Hidden)) hiddenCount++;
                            if (f.Attributes.HasFlag(FileAttributes.System)) systemCount++;

                            string ext = f.Extension.ToLowerInvariant();
                            if (string.IsNullOrEmpty(ext)) ext = "(no extension)";

                            if (extStats.TryGetValue(ext, out var val))
                                extStats[ext] = (val.Count + 1, val.Bytes + f.Length);
                            else
                                extStats[ext] = (1, f.Length);

                            // Security & Disguise Audit on files
                            AuditFolderFile(f, suspiciousList);
                        }

                        foreach (var d in subDirs)
                        {
                            dirsToVisit.Push((d, depth + 1));
                        }
                    }

                    TotalSubFiles = fileCount;
                    TotalSubFolders = Math.Max(0, dirCount - 1);
                    TotalDirectoryBytes = totalBytes;
                    TotalDirectorySizeFormatted = FormatBytes(totalBytes);
                    EmptyDirectoriesCount = emptyDirCount;
                    DeepestNestingLevel = maxDepth;
                    HiddenFilesCount = hiddenCount;
                    SystemFilesCount = systemCount;
                    DisguisedFilesCount = suspiciousList.Count;
                    HasSuspiciousFiles = suspiciousList.Count > 0;

                    if (suspiciousList.Count > 0)
                    {
                        FolderAuditStatus = $"⚠️ {suspiciousList.Count} files need review: Mismatched or misleading file extensions found.";
                        FolderAuditStatusColor = "#EF4444";
                    }
                    else
                    {
                        FolderAuditStatus = $"✅ All clean: {fileCount:N0} files checked. All file formats match their extensions.";
                        FolderAuditStatusColor = "#10B981";
                    }

                    var distList = new ObservableCollection<ExtensionUsageItem>();
                    foreach (var kvp in extStats.OrderByDescending(k => k.Value.Bytes).Take(10))
                    {
                        double pct = totalBytes > 0 ? (double)kvp.Value.Bytes / totalBytes * 100.0 : 0;
                        var visual = FileIconGlyphConverter.GetVisualInfo(kvp.Key, false);
                        distList.Add(new ExtensionUsageItem
                        {
                            Extension = kvp.Key,
                            FileCount = kvp.Value.Count,
                            TotalBytes = kvp.Value.Bytes,
                            DisplaySize = FormatBytes(kvp.Value.Bytes),
                            Percentage = Math.Round(pct, 1),
                            ColorHex = visual.ColorHex
                        });
                    }

                    var topFiles = new ObservableCollection<LargestFileItem>();
                    foreach (var f in filesList.OrderByDescending(f => f.Length).Take(15))
                    {
                        var visual = FileIconGlyphConverter.GetVisualInfo(f.Extension, false);
                        topFiles.Add(new LargestFileItem
                        {
                            Name = f.Name,
                            Path = f.FullName,
                            Bytes = f.Length,
                            DisplaySize = FormatBytes(f.Length),
                            IconGlyph = visual.Glyph,
                            ColorHex = visual.ColorHex
                        });
                    }

                    var suspColl = new ObservableCollection<SuspiciousFileItem>(suspiciousList);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ExtensionDistribution = distList;
                        LargestFiles = topFiles;
                        SuspiciousFiles = suspColl;
                    });
                }
                catch { }
                finally
                {
                    IsStorageAnalysisBusy = false;
                }
            }, ct);
        }

        private static void AuditFolderFile(FileInfo f, List<SuspiciousFileItem> suspiciousList)
        {
            try
            {
                string name = f.Name;
                string ext = f.Extension.ToLowerInvariant();

                // 1. Check for Right-to-Left Override
                if (name.Contains('\u202E'))
                {
                    suspiciousList.Add(new SuspiciousFileItem
                    {
                        Name = name,
                        Path = f.FullName,
                        Reason = "Hidden direction characters: Uses invisible text that reverses the visible file name and extension.",
                        Severity = "Warning",
                        ColorHex = "#EF4444",
                        DisplaySize = FormatBytes(f.Length)
                    });
                    return;
                }

                // 2. Check for double extension
                var dblMatch = Regex.Match(name, @"\.(pdf|docx?|xlsx?|pptx?|jpe?g|png|gif|mp4|mp3|txt|csv)\.(exe|dll|scr|bat|cmd|vbs|ps1|hta|wsf)$", RegexOptions.IgnoreCase);
                if (dblMatch.Success)
                {
                    suspiciousList.Add(new SuspiciousFileItem
                    {
                        Name = name,
                        Path = f.FullName,
                        Reason = $"Double extension: File is an application ({dblMatch.Groups[2].Value}) ending with a document name ({dblMatch.Groups[1].Value}).",
                        Severity = "Warning",
                        ColorHex = "#EF4444",
                        DisplaySize = FormatBytes(f.Length)
                    });
                    return;
                }

                // 3. Quick Magic Header check for non-executables under 10MB
                if (f.Length > 2 && f.Length < 10 * 1024 * 1024 &&
                    ext is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".mp4" or ".mp3" or ".txt" or ".docx" or ".xlsx")
                {
                    using var stream = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    byte[] hdr = new byte[2];
                    if (stream.Read(hdr, 0, 2) == 2 && hdr[0] == 0x4D && hdr[1] == 0x5A) // MZ header!
                    {
                        suspiciousList.Add(new SuspiciousFileItem
                        {
                            Name = name,
                            Path = f.FullName,
                            Reason = $"Mismatched format: File is a program or application inside, but has a '{ext}' extension.",
                            Severity = "Warning",
                            ColorHex = "#EF4444",
                            DisplaySize = FormatBytes(f.Length)
                        });
                    }
                }
            }
            catch { }
        }

        private async Task GenerateDirectoryManifestAsync()
        {
            if (!Directory.Exists(TargetPath)) return;

            _manifestCts?.Cancel();
            _manifestCts = new CancellationTokenSource();
            var ct = _manifestCts.Token;

            IsManifestBusy = true;
            ManifestStatusText = "Calculating SHA-256 checksums...";

            await Task.Run(async () =>
            {
                try
                {
                    var dir = new DirectoryInfo(TargetPath);
                    var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                                   .OrderByDescending(f => f.Length)
                                   .Take(50)
                                   .ToList();

                    var list = new ObservableCollection<ManifestFileItem>();
                    foreach (var f in files)
                    {
                        list.Add(new ManifestFileItem
                        {
                            Name = f.Name,
                            RelativePath = f.Name,
                            DisplaySize = FormatBytes(f.Length),
                            Sha256 = "Computing..."
                        });
                    }

                    App.Current.Dispatcher.Invoke(() => ManifestFiles = list);

                    foreach (var item in list)
                    {
                        if (ct.IsCancellationRequested) break;
                        string full = Path.Combine(TargetPath, item.Name);
                        try
                        {
                            string hash = await _checksumService.CalculateSHA256Async(full, ct);
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                item.Sha256 = hash;
                                item.StatusIcon = "\uE73E";
                            });
                        }
                        catch
                        {
                            App.Current.Dispatcher.Invoke(() => item.Sha256 = "Access Denied");
                        }
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ManifestStatusText = $"Calculated checksums for {list.Count} files.";
                    });
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() => ManifestStatusText = $"Error calculating checksums: {ex.Message}");
                }
                finally
                {
                    App.Current.Dispatcher.Invoke(() => IsManifestBusy = false);
                }
            }, ct);
        }

        private void CopyDirectoryManifest()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Rift Vault Checksum List (SHA-256)");
                sb.AppendLine($"# Target: {TargetPath}");
                sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                foreach (var item in ManifestFiles)
                {
                    if (!string.IsNullOrEmpty(item.Sha256) && item.Sha256 != "Computing..." && item.Sha256 != "Access Denied")
                    {
                        sb.AppendLine($"{item.Sha256} *{item.Name}");
                    }
                }
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch { }
        }
        #endregion

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.##} {suffixes[order]}";
        }
    }
}
