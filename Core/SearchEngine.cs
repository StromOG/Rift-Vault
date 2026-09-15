using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RiftVault.Models;

namespace RiftVault.Core
{
    public interface ISearchEngine
    {
        Task<List<FileItem>> SearchDirectoryAsync(string path, string query, CancellationToken ct);
        Task<List<FileItem>> GlobalSearchAsync(string query, CancellationToken ct);
    }

    public class SearchEngine : ISearchEngine
    {
        private readonly IFileSystemEngine _fsEngine;

        public SearchEngine(IFileSystemEngine fsEngine)
        {
            _fsEngine = fsEngine;
        }

        public async Task<List<FileItem>> SearchDirectoryAsync(string path, string query, CancellationToken ct)
        {
            var allFiles = await _fsEngine.GetDirectoryContentsAsync(path, ct);
            if (string.IsNullOrWhiteSpace(query)) return allFiles;

            return await Task.Run(() =>
            {
                var regex = CreateFuzzyRegex(query);
                return allFiles
                    .Where(f => regex.IsMatch(f.Name) || f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(f => f.Name)
                    .ToList();
            }, ct);
        }

        public async Task<List<FileItem>> GlobalSearchAsync(string query, CancellationToken ct)
        {
            // Simplified global search for demonstration.
            // In a production scenario, this would interface with Windows Search Index (ISearchQueryHelper)
            // or maintain a local SQLite index of frequently accessed files.
            var results = new List<FileItem>();
            string[] commonPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            foreach (var path in commonPaths)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var files = await SearchDirectoryAsync(path, query, ct);
                    results.AddRange(files.Take(20)); // Limit per directory to keep it fast
                }
                catch { /* Ignore access denied */ }
            }

            return results.DistinctBy(f => f.Path).Take(50).ToList();
        }

        private Regex CreateFuzzyRegex(string query)
        {
            // "doc" -> ".*d.*o.*c.*"
            var pattern = ".*" + string.Join(".*", query.Select(c => Regex.Escape(c.ToString()))) + ".*";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}