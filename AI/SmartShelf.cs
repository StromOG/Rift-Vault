using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using RiftVault.Models;

namespace RiftVault.AI
{
    public class FileAccessRecord
    {
        [LoadColumn(0)] public string FilePath { get; set; } = string.Empty;
        [LoadColumn(1)] public float HourOfDay { get; set; }
        [LoadColumn(2)] public float DayOfWeek { get; set; }
        [LoadColumn(3)] public float AccessCount { get; set; }
        [LoadColumn(4)] public float Label { get; set; } // Probability of next access
    }

    public class FilePrediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }

    public class SmartShelf
    {
        private readonly string _dataFilePath;
        private readonly Dictionary<string, int> _accessCounts = new();
        private readonly MLContext _mlContext;

        public SmartShelf()
        {
            _mlContext = new MLContext(seed: 0);
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "ai");
            Directory.CreateDirectory(appData);
            _dataFilePath = Path.Combine(appData, "usage.json");
            LoadHistory();
        }

        public void RecordAccess(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (_accessCounts.ContainsKey(filePath))
                _accessCounts[filePath]++;
            else
                _accessCounts[filePath] = 1;

            SaveHistory();
            
            // Retrain model periodically in a real scenario
            // TrainModel(); 
        }

        public List<string> PredictNextFiles(int count = 6)
        {
            // If model isn't trained or not enough data, fallback to most frequent/recent
            if (_accessCounts.Count == 0) return new List<string>();

            return _accessCounts
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .Where(File.Exists)
                .Take(count)
                .ToList();
        }

        private void LoadHistory()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (data != null)
                    {
                        foreach (var kvp in data) _accessCounts[kvp.Key] = kvp.Value;
                    }
                }
                catch { /* Corrupted history, start fresh */ }
            }
        }

        private void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(_accessCounts);
                File.WriteAllText(_dataFilePath, json);
            }
            catch { }
        }

        // ML.NET FastTree Regression implementation would go here
        // private void TrainModel() { ... }
    }
}