using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace RiftVault.Core
{
    public interface IShellFileService
    {
        Task<bool> CreateFolderAsync(string folderPath);
        Task<bool> CreateFileAsync(string filePath, string content = "");
        Task<bool> DeleteItemsAsync(IEnumerable<string> paths, bool permanent = false);
        Task<bool> MoveItemAsync(string source, string destination);
        Task<bool> CopyItemAsync(string source, string destination);
        Task<bool> RenameItemAsync(string sourcePath, string newName);
    }

    /// <summary>
    /// High-performance Windows Shell File Service providing full explorer capabilities:
    /// - Normal standard user execution without requiring the app to be elevated.
    /// - Windows Recycle Bin support by default to prevent accidental data loss.
    /// - Seamless one-shot UAC elevation on-demand when operating on protected system paths.
    /// </summary>
    public class ShellFileService : IShellFileService
    {
        private readonly ILogger<ShellFileService> _logger;

        public ShellFileService(ILogger<ShellFileService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CreateFolderAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return false;

            try
            {
                Directory.CreateDirectory(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogInformation("Creating folder in protected location, requesting single-action UAC: {Path}", folderPath);
                return await ExecuteElevatedCmdAsync($"md \"{folderPath}\"");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create folder: {Path}", folderPath);
                return false;
            }
        }

        public async Task<bool> CreateFileAsync(string filePath, string content = "")
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            try
            {
                await File.WriteAllTextAsync(filePath, content);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogInformation("Creating file in protected location, requesting single-action UAC: {Path}", filePath);
                if (string.IsNullOrEmpty(content))
                {
                    return await ExecuteElevatedCmdAsync($"type nul > \"{filePath}\"");
                }
                else
                {
                    string safeContent = content.Replace("'", "''");
                    string safePath = filePath.Replace("'", "''");
                    return await ExecuteElevatedPowerShellAsync($"[System.IO.File]::WriteAllText('{safePath}', '{safeContent}')");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create file: {Path}", filePath);
                return false;
            }
        }

        public async Task<bool> DeleteItemsAsync(IEnumerable<string> paths, bool permanent = false)
        {
            bool allSucceeded = true;

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                try
                {
                    bool isDir = Directory.Exists(path);
                    bool isFile = File.Exists(path);

                    if (!isDir && !isFile) continue;

                    if (permanent)
                    {
                        if (isDir) Directory.Delete(path, true);
                        else File.Delete(path);
                    }
                    else
                    {
                        // Send to Windows Recycle Bin with Undo support
                        if (isDir)
                        {
                            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                        }
                        else
                        {
                            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogInformation("Deleting protected item, requesting single-action UAC: {Path}", path);
                    string safePath = path.Replace("'", "''");
                    bool res = await ExecuteElevatedPowerShellAsync($"Remove-Item -LiteralPath '{safePath}' -Recurse -Force");
                    if (!res) allSucceeded = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete item: {Path}", path);
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        public async Task<bool> MoveItemAsync(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination)) return false;

            try
            {
                if (Directory.Exists(source))
                {
                    Directory.Move(source, destination);
                }
                else if (File.Exists(source))
                {
                    File.Move(source, destination, true);
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogInformation("Moving item in protected location, requesting single-action UAC: {Source} -> {Dest}", source, destination);
                string safeSrc = source.Replace("'", "''");
                string safeDst = destination.Replace("'", "''");
                return await ExecuteElevatedPowerShellAsync($"Move-Item -LiteralPath '{safeSrc}' -Destination '{safeDst}' -Force");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move item: {Source} -> {Dest}", source, destination);
                return false;
            }
        }

        public async Task<bool> CopyItemAsync(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination)) return false;

            try
            {
                if (Directory.Exists(source))
                {
                    FileSystem.CopyDirectory(source, destination, UIOption.OnlyErrorDialogs);
                }
                else if (File.Exists(source))
                {
                    FileSystem.CopyFile(source, destination, UIOption.OnlyErrorDialogs);
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogInformation("Copying item in protected location, requesting single-action UAC: {Source} -> {Dest}", source, destination);
                string safeSrc = source.Replace("'", "''");
                string safeDst = destination.Replace("'", "''");
                return await ExecuteElevatedPowerShellAsync($"Copy-Item -LiteralPath '{safeSrc}' -Destination '{safeDst}' -Recurse -Force");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy item: {Source} -> {Dest}", source, destination);
                return false;
            }
        }

        public async Task<bool> RenameItemAsync(string sourcePath, string newName)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(newName)) return false;

            string? parent = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(parent)) return false;

            string destPath = Path.Combine(parent, newName);
            return await MoveItemAsync(sourcePath, destPath);
        }

        private static async Task<bool> ExecuteElevatedCmdAsync(string cmdCommand)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {cmdCommand}",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return false;
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // User declined UAC prompt
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        private static async Task<bool> ExecuteElevatedPowerShellAsync(string psCommand)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{psCommand}\"",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) return false;
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // User declined UAC prompt
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
