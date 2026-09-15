using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using RiftVault.Core;
using RiftVault.Models;

namespace RiftVault.AI
{
    public class FileFeatureData
    {
        public string Path { get; set; } = string.Empty;
        public float SizeCategory { get; set; }
        public float ExtensionHash { get; set; }
        public float AgeCategory { get; set; }
    }

    public class ClusterPrediction
    {
        [ColumnName("PredictedLabel")]
        public uint PredictedClusterId;
        [ColumnName("Score")]
        public float[] Distances = Array.Empty<float>();
    }

    public class AutoOrganizer
    {
        private readonly IFileSystemEngine _fsEngine;
        private readonly MLContext _mlContext;

        public AutoOrganizer(IFileSystemEngine fsEngine)
        {
            _fsEngine = fsEngine;
            _mlContext = new MLContext(seed: 42);
        }

        public async Task<Dictionary<string, List<string>>> SuggestOrganizationAsync(string directoryPath)
        {
            var files = await _fsEngine.GetDirectoryContentsAsync(directoryPath, CancellationToken.None);
            var fileList = files.Where(f => !f.IsDirectory).ToList();
            
            if (fileList.Count < 10) return new Dictionary<string, List<string>>(); // Not enough files to cluster

            return await Task.Run(() =>
            {
                var featureData = fileList.Select(f => new FileFeatureData
                {
                    Path = f.Path,
                    SizeCategory = (float)Math.Log10(f.Size + 1),
                    ExtensionHash = Math.Abs(f.Extension.GetHashCode() % 100) / 100f,
                    AgeCategory = (float)(DateTime.Now - f.DateModified).TotalDays / 365f
                }).ToList();

                var dataView = _mlContext.Data.LoadFromEnumerable(featureData);
                
                var pipeline = _mlContext.Transforms.Concatenate("Features", nameof(FileFeatureData.SizeCategory), nameof(FileFeatureData.ExtensionHash), nameof(FileFeatureData.AgeCategory))
                    .Append(_mlContext.Clustering.Trainers.KMeans("Features", numberOfClusters: Math.Min(5, fileList.Count / 5)));

                var model = pipeline.Fit(dataView);
                var predictor = _mlContext.Model.CreatePredictionEngine<FileFeatureData, ClusterPrediction>(model);

                var clusters = new Dictionary<string, List<string>>();
                
                foreach (var item in featureData)
                {
                    var prediction = predictor.Predict(item);
                    string ext = Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "UNKNOWN";
                    
                    // Naming the cluster based on dominant extension or generic name
                    string folderName = $"Group_{prediction.PredictedClusterId}_{ext}";
                    
                    if (!clusters.ContainsKey(folderName)) clusters[folderName] = new List<string>();
                    clusters[folderName].Add(item.Path);
                }

                return clusters;
            }).ConfigureAwait(false);
        }
    }
}