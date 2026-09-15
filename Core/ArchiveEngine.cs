using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using SevenZipExtractor;
using RiftVault.Models;

namespace RiftVault.Core
{
    public interface IArchiveEngine
    {
        Task<List<FileItem>> GetArchiveContentsAsync(string archivePath, CancellationToken ct);
        Task ExtractArchiveAsync(string archivePath, string destinationPath, Action<double> onProgress, CancellationToken ct);
        Task CreateZipAsync(string zipPath, IEnumerable<string> sourcePaths, Action<double> onProgress, CancellationToken ct);
    }

    public class ArchiveEngine : IArchiveEngine
    {
        public async Task<List<FileItem>> GetArchiveContentsAsync(string archivePath, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var items = new List<FileItem>();
                string ext = Path.GetExtension(archivePath).ToLower();

                if (ext == ".zip")
                {
                    using var fs = File.OpenRead(archivePath);
                    using var zf = new ZipFile(fs);
                    foreach (ZipEntry zipEntry in zf)
                    {
                        if (ct.IsCancellationRequested) break;
                        items.Add(new FileItem
                        {
                            Name = Path.GetFileName(zipEntry.Name),
                            Path = zipEntry.Name, // Virtual path inside archive
                            IsDirectory = zipEntry.IsDirectory,
                            Size = zipEntry.Size,
                            DateModified = zipEntry.DateTime
                        });
                    }
                }
                else if (ext == ".7z" || ext == ".rar")
                {
                    using var archiveFile = new ArchiveFile(archivePath);
                    foreach (var entry in archiveFile.Entries)
                    {
                        if (ct.IsCancellationRequested) break;
                        items.Add(new FileItem
                        {
                            Name = entry.FileName,
                            Path = entry.FileName,
                            IsDirectory = entry.IsFolder,
                            Size = (long)entry.Size,
                            DateModified = entry.LastWriteTime
                        });
                    }
                }
                return items;
            }, ct);
        }

        public async Task ExtractArchiveAsync(string archivePath, string destinationPath, Action<double> onProgress, CancellationToken ct)
        {
            await Task.Run(() =>
            {
                string ext = Path.GetExtension(archivePath).ToLower();
                if (ext == ".zip")
                {
                    var fastZip = new FastZip();
                    // FastZip doesn't support granular progress reporting easily without custom events
                    fastZip.ExtractZip(archivePath, destinationPath, null);
                    onProgress?.Invoke(1.0);
                }
                else if (ext == ".7z" || ext == ".rar")
                {
                    using var archiveFile = new ArchiveFile(archivePath);
                    archiveFile.Extract(destinationPath, true);
                    onProgress?.Invoke(1.0);
                }
            }, ct);
        }

        public async Task CreateZipAsync(string zipPath, IEnumerable<string> sourcePaths, Action<double> onProgress, CancellationToken ct)
        {
            await Task.Run(() =>
            {
                var fastZip = new FastZip();
                fastZip.CreateEmptyDirectories = true;
                
                // Simplified: assuming sourcePaths are all in the same parent directory
                string? parentDir = null;
                foreach(var p in sourcePaths) { parentDir = Path.GetDirectoryName(p); break; }
                
                if (parentDir != null)
                {
                    fastZip.CreateZip(zipPath, parentDir, true, null);
                }
                onProgress?.Invoke(1.0);
            }, ct);
        }
    }
}