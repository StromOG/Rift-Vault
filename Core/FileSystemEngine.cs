using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RiftVault.Models;
using RiftVault.Win32;

namespace RiftVault.Core
{
    public interface IFileSystemEngine
    {
        Task<List<FileItem>> GetDirectoryContentsAsync(string path, CancellationToken ct);
    }

    public class FileSystemEngine : IFileSystemEngine
    {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public static bool IsThisPc(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            string resolved = ResolvePath(path);
            return resolved.Equals("This PC", StringComparison.OrdinalIgnoreCase)
                || resolved.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                || resolved.Equals("shell:MyComputerFolder", StringComparison.OrdinalIgnoreCase)
                || resolved.Equals("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolvePath(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "This PC";

            string trimmed = input.Trim();

            // 1. "This PC" / Known GUIDs
            if (trimmed.Equals("This PC", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Computer", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("shell:MyComputerFolder", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", StringComparison.OrdinalIgnoreCase))
            {
                return "This PC";
            }

            // 2. Windows Shell Special Folders (shell:Personal, shell:Downloads, etc.)
            if (trimmed.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                string folder = trimmed.Substring(6).Trim().ToLowerInvariant();
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                string resolvedShell = folder switch
                {
                    "personal" or "documents" or "mydocuments" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "downloads" => Path.Combine(userProfile, "Downloads"),
                    "pictures" or "mypictures" or "my pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "music" or "mymusic" or "my music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "videos" or "myvideos" or "my video" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "appdata" or "roaming appdata" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "local appdata" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "common appdata" or "programdata" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "windows" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "system" or "system32" => Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "programfiles" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "programfilesx86" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "recent" or "recentplacesfolder" => Environment.GetFolderPath(Environment.SpecialFolder.Recent),
                    "favorites" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
                    "startup" => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "fonts" => Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "userprofile" or "profile" => userProfile,
                    "mycomputerfolder" or "thispc" => "This PC",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(resolvedShell) && (Directory.Exists(resolvedShell) || resolvedShell == "This PC"))
                {
                    return resolvedShell;
                }
            }

            // 3. User Home Shorthand (~)
            if (trimmed == "~" || trimmed.StartsWith("~/") || trimmed.StartsWith(@"~\"))
            {
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (trimmed == "~") return userHome;
                string sub = trimmed.Substring(2).Replace('/', '\\');
                return Path.Combine(userHome, sub);
            }

            // 4. Expand Environment Variables (%TEMP%, %APPDATA%, etc.)
            string expanded = Environment.ExpandEnvironmentVariables(trimmed);

            // 5. Single drive letter like "C:" -> "C:\" or "c" -> "C:\"
            if (expanded.Length == 2 && expanded[1] == ':' && char.IsLetter(expanded[0]))
            {
                return expanded.ToUpperInvariant() + "\\";
            }
            if (expanded.Length == 1 && char.IsLetter(expanded[0]))
            {
                string candidate = expanded.ToUpperInvariant() + ":\\";
                if (Directory.Exists(candidate)) return candidate;
            }

            return expanded;
        }

        public async Task<List<FileItem>> GetDirectoryContentsAsync(string path, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var items = new List<FileItem>();

                // 1. Resolve path (shell:, %, ~, etc.)
                string normalizedPath = ResolvePath(path);

                // 2. "This PC" / Computer Root Virtual Folder
                if (IsThisPc(normalizedPath))
                {
                    return GetThisPCContents();
                }

                // Ensure trailing slash for search pattern
                string searchPath;
                if (normalizedPath.EndsWith('\\') || normalizedPath.EndsWith('/'))
                {
                    searchPath = normalizedPath + "*";
                }
                else
                {
                    searchPath = normalizedPath + "\\*";
                }

                // Support Win32 extended-length paths (up to 32,767 characters)
                string win32SearchPath = FormatWin32LongPath(searchPath);

                IntPtr hFind = NativeMethods.FindFirstFileEx(
                    win32SearchPath,
                    NativeMethods.FINDEX_INFO_LEVELS.FindExInfoBasic,
                    out NativeMethods.WIN32_FIND_DATA findData,
                    NativeMethods.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                    IntPtr.Zero,
                    NativeMethods.FIND_FIRST_EX_LARGE_FETCH);

                if (hFind != INVALID_HANDLE_VALUE)
                {
                    try
                    {
                        do
                        {
                            if (ct.IsCancellationRequested) break;

                            string name = findData.cFileName;
                            if (name == "." || name == "..") continue;

                            bool isDir = (findData.dwFileAttributes & (uint)FileAttributes.Directory) != 0;
                            long size = ((long)findData.nFileSizeHigh << 32) | findData.nFileSizeLow;

                            items.Add(new FileItem
                            {
                                Name = name,
                                Path = Path.Combine(normalizedPath, name),
                                Extension = isDir ? "" : Path.GetExtension(name),
                                IsDirectory = isDir,
                                Size = size,
                                DateModified = FileTimeToDateTime(findData.ftLastWriteTime),
                                DateCreated = FileTimeToDateTime(findData.ftCreationTime)
                            });

                        } while (NativeMethods.FindNextFile(hFind, out findData));
                    }
                    finally
                    {
                        NativeMethods.FindClose(hFind);
                    }
                }

                // 3. Resilient Fallback: If FindFirstFileEx returned nothing or failed on a protected system folder
                // (e.g. C:\Windows, C:\Program Files, C:\ProgramData), use .NET EnumerationOptions with IgnoreInaccessible = true
                if (items.Count == 0 && Directory.Exists(normalizedPath))
                {
                    try
                    {
                        var di = new DirectoryInfo(normalizedPath);
                        var options = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.None, // Do not skip hidden or system files
                            RecurseSubdirectories = false,
                            ReturnSpecialDirectories = false
                        };

                        foreach (var info in di.EnumerateFileSystemInfos("*", options))
                        {
                            if (ct.IsCancellationRequested) break;

                            bool isDir = (info.Attributes & FileAttributes.Directory) != 0;
                            long size = 0;
                            if (!isDir && info is FileInfo fi)
                            {
                                try { size = fi.Length; } catch { }
                            }

                            items.Add(new FileItem
                            {
                                Name = info.Name,
                                Path = info.FullName,
                                Extension = isDir ? "" : (info.Extension ?? ""),
                                IsDirectory = isDir,
                                Size = size,
                                DateModified = info.LastWriteTime,
                                DateCreated = info.CreationTime
                            });
                        }
                    }
                    catch
                    {
                        // Directory is strictly locked or inaccessible
                    }
                }

                return items;
            }, ct).ConfigureAwait(false);
        }

        private static List<FileItem> GetThisPCContents()
        {
            var items = new List<FileItem>();

            // 1. All Drives (Physical, Virtual, Removable, Network)
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    string driveLetter = drive.Name.TrimEnd('\\');
                    string volLabel = "";
                    long totalSize = 0;
                    try
                    {
                        if (drive.IsReady)
                        {
                            volLabel = drive.VolumeLabel;
                            totalSize = drive.TotalSize;
                        }
                    }
                    catch { }

                    string displayName = !string.IsNullOrEmpty(volLabel)
                        ? $"{volLabel} ({driveLetter})"
                        : $"Local Disk ({driveLetter})";

                    if (drive.DriveType == DriveType.CDRom)
                        displayName = $"CD/DVD Drive ({driveLetter})";
                    else if (drive.DriveType == DriveType.Network)
                        displayName = $"Network Drive ({driveLetter})";
                    else if (drive.DriveType == DriveType.Removable)
                        displayName = string.IsNullOrEmpty(volLabel) ? $"USB Drive ({driveLetter})" : $"{volLabel} ({driveLetter})";

                    items.Add(new FileItem
                    {
                        Name = displayName,
                        Path = drive.RootDirectory.FullName,
                        Extension = "",
                        IsDirectory = true,
                        Size = totalSize,
                        DateModified = DateTime.Now,
                        DateCreated = DateTime.Now
                    });
                }
            }
            catch { }

            // 2. Standard User Shell Folders (Desktop, Documents, Downloads, Music, Pictures, Videos)
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var shellFolders = new (string Name, string Path)[]
                {
                    ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                    ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                    ("Downloads", Path.Combine(userProfile, "Downloads")),
                    ("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                    ("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
                    ("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))
                };

                foreach (var sf in shellFolders)
                {
                    if (Directory.Exists(sf.Path))
                    {
                        items.Add(new FileItem
                        {
                            Name = sf.Name,
                            Path = sf.Path,
                            Extension = "",
                            IsDirectory = true,
                            Size = 0,
                            DateModified = Directory.GetLastWriteTime(sf.Path),
                            DateCreated = Directory.GetCreationTime(sf.Path)
                        });
                    }
                }
            }
            catch { }

            return items;
        }

        private static string FormatWin32LongPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // If already prefixed with \\?\ or \\.\, leave as is
            if (path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\")) return path;

            // UNC network path: \\server\share -> \\?\UNC\server\share
            if (path.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + path.Substring(2);
            }

            // Standard local drive absolute path: C:\... -> \\?\C:\...
            if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
            {
                return @"\\?\" + path;
            }

            return path;
        }

        private static DateTime FileTimeToDateTime(NativeMethods.FILETIME ft)
        {
            long hFT2 = (((long)ft.dwHighDateTime) << 32) | ft.dwLowDateTime;
            try
            {
                return DateTime.FromFileTimeUtc(hFT2).ToLocalTime();
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}