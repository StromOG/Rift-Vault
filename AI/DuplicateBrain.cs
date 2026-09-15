using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RiftVault.Core;
using RiftVault.Models;

namespace RiftVault.AI
{
    public class DuplicateGroup
    {
        public string Original { get; set; } = string.Empty;
        public List<string> Copies { get; set; } = new();
        public long Size { get; set; }
    }

    public class DuplicateBrain
    {
        private readonly IFileSystemEngine _fsEngine;
        private readonly IChecksumService _checksumService;

        public DuplicateBrain(IFileSystemEngine fsEngine, IChecksumService checksumService)
        {
            _fsEngine = fsEngine;
            _checksumService = checksumService;
        }

        public async Task<List<DuplicateGroup>> FindExactDuplicatesAsync(string directoryPath, CancellationToken ct)
        {
            var allFiles = await _fsEngine.GetDirectoryContentsAsync(directoryPath, ct);
            var results = new List<DuplicateGroup>();

            await Task.Run(async () =>
            {
                // Phase 1: Group by size (instant)
                var sizeGroups = allFiles
                    .Where(f => !f.IsDirectory && f.Size > 0)
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .ToList();

                // Phase 2: MD5 Hash comparison for files with same size
                foreach (var group in sizeGroups)
                {
                    if (ct.IsCancellationRequested) break;

                    var hashGroups = new Dictionary<string, List<FileItem>>();
                    
                    foreach (var file in group)
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            string hash = await _checksumService.CalculateMD5Async(file.Path, ct);
                            if (!hashGroups.ContainsKey(hash)) hashGroups[hash] = new List<FileItem>();
                            hashGroups[hash].Add(file);
                        }
                        catch { /* Ignore locked files */ }
                    }

                    foreach (var hg in hashGroups.Values.Where(v => v.Count > 1))
                    {
                        var sorted = hg.OrderBy(f => f.DateCreated).ToList();
                        results.Add(new DuplicateGroup
                        {
                            Original = sorted.First().Path,
                            Copies = sorted.Skip(1).Select(f => f.Path).ToList(),
                            Size = group.Key
                        });
                    }
                }
            }, ct).ConfigureAwait(false);

            return results;
        }
    }
}