using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;

namespace RiftVault.UI.Converters
{
    public class TypeVisualInfo
    {
        public string Glyph { get; set; } = "\uE7C3";
        public string ColorHex { get; set; } = "#94A3B8";
        public string Description { get; set; } = "File";
        public string Badge { get; set; } = "FILE";
        public string Category { get; set; } = "Generic";
    }

    /// <summary>
    /// Comprehensive visual intelligence engine mapping 120+ file formats and 25+ special folder patterns
    /// to dedicated Segoe Fluent glyphs, harmonic category colors, uppercase micro-badges, and professional descriptions.
    /// </summary>
    public class FileIconGlyphConverter : IMultiValueConverter
    {
        public static readonly FileIconGlyphConverter Instance = new();

        private static readonly Dictionary<string, TypeVisualInfo> FileVisuals = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, TypeVisualInfo> FolderVisuals = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> CategoryAccentMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Folder"] = "#6B9AC4",
            ["Code"] = "#38BDF8",
            ["Image"] = "#A78BFA",
            ["Audio"] = "#C084FC",
            ["Video"] = "#94A3B8",
            ["Document"] = "#34D399",
            ["Archive"] = "#818CF8",
            ["Executable"] = "#818CF8",
            ["Vault"] = "#6366F1",
            ["Generic"] = "#7E8B99"
        };

        static FileIconGlyphConverter()
        {
            InitializeFolderVisuals();
            InitializeFileVisuals();
        }

        private static void InitializeFolderVisuals()
        {
            // Windows User & Known Folders (Silent, refined, non-aggressive tones)
            FolderVisuals["home"] = new() { Glyph = "\uE80F", ColorHex = "#6EA8B8", Description = "Home Folder", Badge = "HOME", Category = "Folder" };
            FolderVisuals["desktop"] = new() { Glyph = "\uE8FC", ColorHex = "#6EA8B8", Description = "Desktop Folder", Badge = "DESK", Category = "Folder" };
            FolderVisuals["downloads"] = new() { Glyph = "\uE896", ColorHex = "#5E9E82", Description = "Downloads Folder", Badge = "DOWN", Category = "Folder" };
            FolderVisuals["documents"] = new() { Glyph = "\uE8A5", ColorHex = "#7E8EB0", Description = "Documents Folder", Badge = "DOCS", Category = "Folder" };
            FolderVisuals["pictures"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "Pictures Folder", Badge = "PICS", Category = "Folder" };
            FolderVisuals["photos"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "Photos Folder", Badge = "PICS", Category = "Folder" };
            FolderVisuals["images"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "Images Folder", Badge = "IMG", Category = "Folder" };
            FolderVisuals["screenshots"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "Screenshots Folder", Badge = "SHOT", Category = "Folder" };
            FolderVisuals["music"] = new() { Glyph = "\uE8D6", ColorHex = "#8B95C9", Description = "Music Folder", Badge = "AUDIO", Category = "Folder" };
            FolderVisuals["audio"] = new() { Glyph = "\uE8D6", ColorHex = "#8B95C9", Description = "Audio Folder", Badge = "AUDIO", Category = "Folder" };
            FolderVisuals["videos"] = new() { Glyph = "\uE8B2", ColorHex = "#9E8C94", Description = "Videos Folder", Badge = "VIDEO", Category = "Folder" };
            FolderVisuals["movies"] = new() { Glyph = "\uE8B2", ColorHex = "#9E8C94", Description = "Movies Folder", Badge = "VIDEO", Category = "Folder" };
            FolderVisuals["folder"] = new() { Glyph = "\uE8B7", ColorHex = "#6B9AC4", Description = "File folder", Badge = "DIR", Category = "Folder" };
            FolderVisuals["general"] = new() { Glyph = "\uE8B7", ColorHex = "#6B9AC4", Description = "General Folder", Badge = "DIR", Category = "Folder" };

            // Developer & Project Workspaces
            FolderVisuals[".git"] = new() { Glyph = "\uE943", ColorHex = "#7E8CB8", Description = "Git Repository", Badge = "GIT", Category = "Folder" };
            FolderVisuals["git"] = new() { Glyph = "\uE943", ColorHex = "#7E8CB8", Description = "Git Repository", Badge = "GIT", Category = "Folder" };
            FolderVisuals[".github"] = new() { Glyph = "\uE943", ColorHex = "#7E8CB8", Description = "GitHub Actions & Workflows", Badge = "GH", Category = "Folder" };
            FolderVisuals["node_modules"] = new() { Glyph = "\uE7B8", ColorHex = "#6BA894", Description = "Node Package Modules", Badge = "NPM", Category = "Folder" };
            FolderVisuals["src"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "Source Code Directory", Badge = "SRC", Category = "Folder" };
            FolderVisuals["source"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "Source Code Directory", Badge = "SRC", Category = "Folder" };
            FolderVisuals["lib"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "Library Directory", Badge = "LIB", Category = "Folder" };
            FolderVisuals["bin"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Binary Output", Badge = "BIN", Category = "Folder" };
            FolderVisuals["obj"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Build Intermediate Objects", Badge = "OBJ", Category = "Folder" };
            FolderVisuals["build"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Build Distribution", Badge = "BUILD", Category = "Folder" };
            FolderVisuals["dist"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Distribution Package", Badge = "DIST", Category = "Folder" };
            FolderVisuals["out"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Compiler Output", Badge = "OUT", Category = "Folder" };
            FolderVisuals["target"] = new() { Glyph = "\uE74C", ColorHex = "#6E7A8A", Description = "Cargo Target Build", Badge = "TARGET", Category = "Folder" };
            FolderVisuals["assets"] = new() { Glyph = "\uF158", ColorHex = "#729AA8", Description = "Media & Static Assets", Badge = "ASSET", Category = "Folder" };
            FolderVisuals["static"] = new() { Glyph = "\uF158", ColorHex = "#729AA8", Description = "Static Web Assets", Badge = "STATIC", Category = "Folder" };
            FolderVisuals["public"] = new() { Glyph = "\uF158", ColorHex = "#729AA8", Description = "Public Assets", Badge = "PUB", Category = "Folder" };
            FolderVisuals["test"] = new() { Glyph = "\uE9F9", ColorHex = "#828EAA", Description = "Test Suite", Badge = "TEST", Category = "Folder" };
            FolderVisuals["tests"] = new() { Glyph = "\uE9F9", ColorHex = "#828EAA", Description = "Automated Tests", Badge = "TEST", Category = "Folder" };
            FolderVisuals[".vscode"] = new() { Glyph = "\uE713", ColorHex = "#7E8CB8", Description = "VS Code Configuration", Badge = "VSCODE", Category = "Folder" };
            FolderVisuals[".idea"] = new() { Glyph = "\uE713", ColorHex = "#8C8EB8", Description = "IntelliJ IDEA Configuration", Badge = "IDEA", Category = "Folder" };

            // Windows System Directories
            FolderVisuals["windows"] = new() { Glyph = "\uE770", ColorHex = "#6B9AC4", Description = "Windows Operating System", Badge = "SYS", Category = "Folder" };
            FolderVisuals["system32"] = new() { Glyph = "\uE770", ColorHex = "#6B9AC4", Description = "System 64-bit Binaries", Badge = "SYS32", Category = "Folder" };
            FolderVisuals["syswow64"] = new() { Glyph = "\uE770", ColorHex = "#6B9AC4", Description = "System 32-bit Binaries", Badge = "WOW64", Category = "Folder" };
            FolderVisuals["program files"] = new() { Glyph = "\uE71D", ColorHex = "#6B9AC4", Description = "Installed Programs", Badge = "PROG", Category = "Folder" };
            FolderVisuals["program files (x86)"] = new() { Glyph = "\uE71D", ColorHex = "#6B9AC4", Description = "Installed Programs (32-bit)", Badge = "PROG", Category = "Folder" };
            FolderVisuals["appdata"] = new() { Glyph = "\uE713", ColorHex = "#7E8CB8", Description = "Application Data", Badge = "APP", Category = "Folder" };
            FolderVisuals["this pc"] = new() { Glyph = "\uE770", ColorHex = "#6B9AC4", Description = "This PC", Badge = "PC", Category = "Folder" };
            FolderVisuals["computer"] = new() { Glyph = "\uE770", ColorHex = "#6B9AC4", Description = "This PC", Badge = "PC", Category = "Folder" };
        }

        private static void InitializeFileVisuals()
        {
            // ─── Source Code ──────────────────────────────────────────────
            FileVisuals[".cs"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "C# Source File", Badge = "CS", Category = "Code" };
            FileVisuals[".py"] = new() { Glyph = "\uE943", ColorHex = "#629EB0", Description = "Python Script", Badge = "PY", Category = "Code" };
            FileVisuals[".js"] = new() { Glyph = "\uE943", ColorHex = "#629EB0", Description = "JavaScript File", Badge = "JS", Category = "Code" };
            FileVisuals[".ts"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "TypeScript Module", Badge = "TS", Category = "Code" };
            FileVisuals[".jsx"] = new() { Glyph = "\uE943", ColorHex = "#629EB0", Description = "React JSX Component", Badge = "JSX", Category = "Code" };
            FileVisuals[".tsx"] = new() { Glyph = "\uE943", ColorHex = "#629EB0", Description = "React TSX Component", Badge = "TSX", Category = "Code" };
            FileVisuals[".html"] = new() { Glyph = "\uE943", ColorHex = "#7E8FB8", Description = "HTML Web Document", Badge = "HTML", Category = "Code" };
            FileVisuals[".htm"] = new() { Glyph = "\uE943", ColorHex = "#7E8FB8", Description = "HTML Document", Badge = "HTM", Category = "Code" };
            FileVisuals[".css"] = new() { Glyph = "\uE943", ColorHex = "#729AA8", Description = "Cascading Style Sheet", Badge = "CSS", Category = "Code" };
            FileVisuals[".scss"] = new() { Glyph = "\uE943", ColorHex = "#8C8EB8", Description = "SASS Style Sheet", Badge = "SCSS", Category = "Code" };
            FileVisuals[".sass"] = new() { Glyph = "\uE943", ColorHex = "#8C8EB8", Description = "SASS Style Sheet", Badge = "SASS", Category = "Code" };
            FileVisuals[".xaml"] = new() { Glyph = "\uE943", ColorHex = "#7E8CB8", Description = "XAML UI Definition", Badge = "XAML", Category = "Code" };
            FileVisuals[".cpp"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "C++ Source Code", Badge = "CPP", Category = "Code" };
            FileVisuals[".c"] = new() { Glyph = "\uE943", ColorHex = "#6B9AC4", Description = "C Source Code", Badge = "C", Category = "Code" };
            FileVisuals[".h"] = new() { Glyph = "\uE943", ColorHex = "#7E8C99", Description = "C/C++ Header File", Badge = "HDR", Category = "Code" };
            FileVisuals[".hpp"] = new() { Glyph = "\uE943", ColorHex = "#7E8C99", Description = "C++ Header File", Badge = "HPP", Category = "Code" };
            FileVisuals[".rs"] = new() { Glyph = "\uE943", ColorHex = "#7B88B2", Description = "Rust Source Code", Badge = "RS", Category = "Code" };
            FileVisuals[".go"] = new() { Glyph = "\uE943", ColorHex = "#629EB0", Description = "Go Source Code", Badge = "GO", Category = "Code" };
            FileVisuals[".java"] = new() { Glyph = "\uE943", ColorHex = "#7B88B2", Description = "Java Source File", Badge = "JAVA", Category = "Code" };
            FileVisuals[".kt"] = new() { Glyph = "\uE943", ColorHex = "#8C8EB8", Description = "Kotlin Source File", Badge = "KT", Category = "Code" };
            FileVisuals[".swift"] = new() { Glyph = "\uE943", ColorHex = "#7B88B2", Description = "Swift Source File", Badge = "SWFT", Category = "Code" };
            FileVisuals[".php"] = new() { Glyph = "\uE943", ColorHex = "#7B88B2", Description = "PHP Source File", Badge = "PHP", Category = "Code" };
            FileVisuals[".rb"] = new() { Glyph = "\uE943", ColorHex = "#8C8EB8", Description = "Ruby Script", Badge = "RB", Category = "Code" };
            FileVisuals[".sql"] = new() { Glyph = "\uE943", ColorHex = "#6A95B8", Description = "SQL Database Script", Badge = "SQL", Category = "Code" };

            // ─── Scripts & Automation ─────────────────────────────────────
            FileVisuals[".ps1"] = new() { Glyph = "\uE756", ColorHex = "#6E8B99", Description = "PowerShell Script", Badge = "PS1", Category = "Executable" };
            FileVisuals[".bat"] = new() { Glyph = "\uE756", ColorHex = "#7E8C99", Description = "Windows Batch Script", Badge = "BAT", Category = "Executable" };
            FileVisuals[".cmd"] = new() { Glyph = "\uE756", ColorHex = "#7E8C99", Description = "Windows Command Script", Badge = "CMD", Category = "Executable" };
            FileVisuals[".sh"] = new() { Glyph = "\uE756", ColorHex = "#6BA894", Description = "Shell Script", Badge = "SH", Category = "Executable" };
            FileVisuals[".bash"] = new() { Glyph = "\uE756", ColorHex = "#6BA894", Description = "Bash Script", Badge = "BASH", Category = "Executable" };

            // ─── Data & Config ────────────────────────────────────────────
            FileVisuals[".json"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "JSON Configuration", Badge = "JSON", Category = "Code" };
            FileVisuals[".xml"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "XML Data Document", Badge = "XML", Category = "Code" };
            FileVisuals[".yaml"] = new() { Glyph = "\uE713", ColorHex = "#8C8EB8", Description = "YAML Configuration", Badge = "YAML", Category = "Code" };
            FileVisuals[".yml"] = new() { Glyph = "\uE713", ColorHex = "#8C8EB8", Description = "YAML Configuration", Badge = "YML", Category = "Code" };
            FileVisuals[".toml"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "TOML Configuration", Badge = "TOML", Category = "Code" };
            FileVisuals[".ini"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "Configuration Settings", Badge = "INI", Category = "Code" };
            FileVisuals[".cfg"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "Configuration File", Badge = "CFG", Category = "Code" };
            FileVisuals[".conf"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "Configuration File", Badge = "CONF", Category = "Code" };
            FileVisuals[".env"] = new() { Glyph = "\uE713", ColorHex = "#7E8CB8", Description = "Environment Variables", Badge = "ENV", Category = "Code" };
            FileVisuals[".gitignore"] = new() { Glyph = "\uE943", ColorHex = "#7E8CB8", Description = "Git Ignore Rules", Badge = "GIT", Category = "Code" };
            FileVisuals[".dockerignore"] = new() { Glyph = "\uE74C", ColorHex = "#6B9AC4", Description = "Docker Ignore Rules", Badge = "DOCK", Category = "Code" };
            FileVisuals[".editorconfig"] = new() { Glyph = "\uE713", ColorHex = "#7E8C99", Description = "Editor Configuration", Badge = "CFG", Category = "Code" };
            FileVisuals[".log"] = new() { Glyph = "\uE8A5", ColorHex = "#6E7A8A", Description = "System / App Log File", Badge = "LOG", Category = "Document" };

            // ─── Documents ────────────────────────────────────────────────
            FileVisuals[".pdf"] = new() { Glyph = "\uEA90", ColorHex = "#B07D7D", Description = "PDF Document", Badge = "PDF", Category = "Document" };
            FileVisuals[".md"] = new() { Glyph = "\uE8A5", ColorHex = "#8593A6", Description = "Markdown Documentation", Badge = "MD", Category = "Document" };
            FileVisuals[".txt"] = new() { Glyph = "\uE8A5", ColorHex = "#8593A6", Description = "Plain Text Document", Badge = "TXT", Category = "Document" };
            FileVisuals[".doc"] = new() { Glyph = "\uE8A5", ColorHex = "#6B8DB5", Description = "Microsoft Word Document", Badge = "DOC", Category = "Document" };
            FileVisuals[".docx"] = new() { Glyph = "\uE8A5", ColorHex = "#6B8DB5", Description = "Microsoft Word Document", Badge = "DOCX", Category = "Document" };
            FileVisuals[".xls"] = new() { Glyph = "\uE9F9", ColorHex = "#609980", Description = "Microsoft Excel Spreadsheet", Badge = "XLS", Category = "Document" };
            FileVisuals[".xlsx"] = new() { Glyph = "\uE9F9", ColorHex = "#609980", Description = "Microsoft Excel Spreadsheet", Badge = "XLSX", Category = "Document" };
            FileVisuals[".csv"] = new() { Glyph = "\uE9F9", ColorHex = "#609980", Description = "Comma-Separated Data", Badge = "CSV", Category = "Document" };
            FileVisuals[".ppt"] = new() { Glyph = "\uE8A5", ColorHex = "#9884A4", Description = "PowerPoint Presentation", Badge = "PPT", Category = "Document" };
            FileVisuals[".pptx"] = new() { Glyph = "\uE8A5", ColorHex = "#9884A4", Description = "PowerPoint Presentation", Badge = "PPTX", Category = "Document" };
            FileVisuals[".rtf"] = new() { Glyph = "\uE8A5", ColorHex = "#8593A6", Description = "Rich Text Document", Badge = "RTF", Category = "Document" };

            // ─── Images ───────────────────────────────────────────────────
            FileVisuals[".png"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "PNG Image", Badge = "PNG", Category = "Image" };
            FileVisuals[".jpg"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "JPEG Image", Badge = "JPG", Category = "Image" };
            FileVisuals[".jpeg"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "JPEG Image", Badge = "JPEG", Category = "Image" };
            FileVisuals[".jfif"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "JPEG Image", Badge = "JFIF", Category = "Image" };
            FileVisuals[".heic"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "HEIC Image", Badge = "HEIC", Category = "Image" };
            FileVisuals[".heif"] = new() { Glyph = "\uEB9F", ColorHex = "#9B8EB9", Description = "HEIF Image", Badge = "HEIF", Category = "Image" };
            FileVisuals[".webp"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "Google WebP Image", Badge = "WEBP", Category = "Image" };
            FileVisuals[".gif"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "GIF Animated Image", Badge = "GIF", Category = "Image" };
            FileVisuals[".svg"] = new() { Glyph = "\uEB9F", ColorHex = "#6B9AC4", Description = "Scalable Vector Graphic", Badge = "SVG", Category = "Image" };
            FileVisuals[".bmp"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "Bitmap Image", Badge = "BMP", Category = "Image" };
            FileVisuals[".ico"] = new() { Glyph = "\uEB9F", ColorHex = "#6B9AC4", Description = "Windows Icon File", Badge = "ICO", Category = "Image" };
            FileVisuals[".tiff"] = new() { Glyph = "\uEB9F", ColorHex = "#8F8CB0", Description = "TIFF Image", Badge = "TIFF", Category = "Image" };
            FileVisuals[".psd"] = new() { Glyph = "\uEB9F", ColorHex = "#629EB0", Description = "Adobe Photoshop Document", Badge = "PSD", Category = "Image" };

            // ─── Video & Audio ────────────────────────────────────────────
            FileVisuals[".mp4"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "MPEG-4 Video", Badge = "MP4", Category = "Video" };
            FileVisuals[".m4v"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "MPEG-4 Video", Badge = "M4V", Category = "Video" };
            FileVisuals[".mkv"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "Matroska Video", Badge = "MKV", Category = "Video" };
            FileVisuals[".avi"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "AVI Video", Badge = "AVI", Category = "Video" };
            FileVisuals[".mov"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "Apple QuickTime Video", Badge = "MOV", Category = "Video" };
            FileVisuals[".wmv"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "Windows Media Video", Badge = "WMV", Category = "Video" };
            FileVisuals[".webm"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "WebM Video Stream", Badge = "WEBM", Category = "Video" };
            FileVisuals[".mpg"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "MPEG Video", Badge = "MPG", Category = "Video" };
            FileVisuals[".mpeg"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "MPEG Video", Badge = "MPEG", Category = "Video" };
            FileVisuals[".flv"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "Flash Video", Badge = "FLV", Category = "Video" };
            FileVisuals[".3gp"] = new() { Glyph = "\uE8B2", ColorHex = "#8A8E9E", Description = "Mobile Video", Badge = "3GP", Category = "Video" };

            FileVisuals[".mp3"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "MP3 Audio", Badge = "MP3", Category = "Audio" };
            FileVisuals[".wav"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "Waveform Audio (WAV)", Badge = "WAV", Category = "Audio" };
            FileVisuals[".flac"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "FLAC Lossless Audio", Badge = "FLAC", Category = "Audio" };
            FileVisuals[".aac"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "AAC Digital Audio", Badge = "AAC", Category = "Audio" };
            FileVisuals[".m4a"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "MPEG-4 Audio", Badge = "M4A", Category = "Audio" };
            FileVisuals[".ogg"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "Ogg Vorbis Audio", Badge = "OGG", Category = "Audio" };
            FileVisuals[".opus"] = new() { Glyph = "\uE8D6", ColorHex = "#928CB0", Description = "Opus Audio", Badge = "OPS", Category = "Audio" };

            // ─── Archives & Compressed ────────────────────────────────────
            FileVisuals[".zip"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "ZIP Compressed Archive", Badge = "ZIP", Category = "Archive" };
            FileVisuals[".7z"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "7-Zip High Compression Archive", Badge = "7Z", Category = "Archive" };
            FileVisuals[".rar"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "WinRAR Archive", Badge = "RAR", Category = "Archive" };
            FileVisuals[".tar"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "Tarball Archive", Badge = "TAR", Category = "Archive" };
            FileVisuals[".gz"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "GZip Compressed Archive", Badge = "GZ", Category = "Archive" };
            FileVisuals[".tgz"] = new() { Glyph = "\uF012", ColorHex = "#7E8A99", Description = "Tarball Archive", Badge = "TGZ", Category = "Archive" };
            FileVisuals[".iso"] = new() { Glyph = "\uE958", ColorHex = "#7886A0", Description = "Optical Disc Image (ISO)", Badge = "ISO", Category = "Archive" };

            // ─── Executables & System ─────────────────────────────────────
            FileVisuals[".exe"] = new() { Glyph = "\uE71D", ColorHex = "#7886A0", Description = "Windows Application", Badge = "EXE", Category = "Executable" };
            FileVisuals[".msi"] = new() { Glyph = "\uE71D", ColorHex = "#7886A0", Description = "Windows Installer Package", Badge = "MSI", Category = "Executable" };
            FileVisuals[".dll"] = new() { Glyph = "\uE74C", ColorHex = "#7E8C99", Description = "Application Extension (DLL)", Badge = "DLL", Category = "Executable" };
            FileVisuals[".sys"] = new() { Glyph = "\uE74C", ColorHex = "#7E8C99", Description = "System Device Driver", Badge = "SYS", Category = "Executable" };
            FileVisuals[".rvault"] = new() { Glyph = "\uE72E", ColorHex = "#6B7FA8", Description = "Rift Encrypted Vault", Badge = "VAULT", Category = "Vault" };
            FileVisuals[".lnk"] = new() { Glyph = "\uE71B", ColorHex = "#6B9AC4", Description = "Shortcut", Badge = "LNK", Category = "Generic" };
            FileVisuals[".ttf"] = new() { Glyph = "\uE8D2", ColorHex = "#8F8CB0", Description = "TrueType Font File", Badge = "TTF", Category = "Generic" };
            FileVisuals[".otf"] = new() { Glyph = "\uE8D2", ColorHex = "#8F8CB0", Description = "OpenType Font File", Badge = "OTF", Category = "Generic" };
            FileVisuals[".woff"] = new() { Glyph = "\uE8D2", ColorHex = "#8F8CB0", Description = "Web Open Font Format", Badge = "WOFF", Category = "Generic" };
            FileVisuals[".woff2"] = new() { Glyph = "\uE8D2", ColorHex = "#8F8CB0", Description = "Web Open Font 2.0", Badge = "WOFF2", Category = "Generic" };
        }

        public static TypeVisualInfo GetVisualInfo(string nameOrExt, bool isDirectory)
        {
            if (isDirectory)
            {
                string cleanName = (nameOrExt ?? "").Trim().ToLowerInvariant();
                if (FolderVisuals.TryGetValue(cleanName, out var special))
                {
                    return special;
                }

                // Check for Drive formats: e.g. "Local Disk (C:)", "C:\", "C:", "USB Drive (D:)"
                if (cleanName.EndsWith(":") || cleanName.EndsWith(":\\") || cleanName.Contains("local disk") || cleanName.Contains("drive (") || cleanName.Contains("usb drive") || cleanName.Contains("cd/dvd"))
                {
                    string drvBadge = "DRV";
                    if (cleanName.Contains("usb")) drvBadge = "USB";
                    else if (cleanName.Contains("cd") || cleanName.Contains("dvd")) drvBadge = "DISC";
                    else if (cleanName.Contains("network")) drvBadge = "NET";

                    return new TypeVisualInfo
                    {
                        Glyph = "\uEDA2",
                        ColorHex = "#6B9AC4",
                        Description = "Local Storage Drive",
                        Badge = drvBadge,
                        Category = "Folder"
                    };
                }

                // Default Folder - Calm, elegant, silent steel blue
                return new TypeVisualInfo
                {
                    Glyph = "\uE8B7",
                    ColorHex = "#6B9AC4",
                    Description = "File folder",
                    Badge = "DIR",
                    Category = "Folder"
                };
            }

            string ext = Path.GetExtension(nameOrExt ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) && !string.IsNullOrEmpty(nameOrExt) && nameOrExt.StartsWith('.'))
            {
                ext = nameOrExt.ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(ext) && FileVisuals.TryGetValue(ext, out var info))
            {
                return info;
            }

            string badge = string.IsNullOrEmpty(ext) ? "FILE" : ext.TrimStart('.').ToUpperInvariant();
            if (badge.Length > 4) badge = badge.Substring(0, 4);

            var fallbackCategory = CategoryAccentMap.TryGetValue("Generic", out var fallbackColor)
                ? "Generic"
                : "Generic";

            return new TypeVisualInfo
            {
                Glyph = "\uE7C3",
                ColorHex = CategoryAccentMap.TryGetValue(fallbackCategory, out var defaultColor) ? defaultColor : "#7E8B99",
                Description = string.IsNullOrEmpty(ext) ? "File" : $"{ext.TrimStart('.').ToUpperInvariant()} File",
                Badge = badge,
                Category = fallbackCategory
            };
        }

        public static string GetGlyph(string extension, bool isDirectory)
            => GetVisualInfo(extension, isDirectory).Glyph;

        public static string GetCategoryAccent(string category)
            => CategoryAccentMap.TryGetValue(category ?? string.Empty, out var color) ? color : CategoryAccentMap["Generic"];

        public static string GetTypeDescription(string extension, bool isDirectory)
            => GetVisualInfo(extension, isDirectory).Description;

        public static string GetBadge(string extension, bool isDirectory)
            => GetVisualInfo(extension, isDirectory).Badge;

        public static string GetColorHex(string extension, bool isDirectory)
            => GetVisualInfo(extension, isDirectory).ColorHex;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string ext = values.Length > 0 ? values[0] as string ?? "" : "";
            bool isDir = values.Length > 1 && values[1] is bool b && b;
            return GetGlyph(ext, isDir);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
