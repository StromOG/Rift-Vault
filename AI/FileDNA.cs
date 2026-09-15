using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using RiftVault.Services;

namespace RiftVault.AI
{
    public class ImageData
    {
        [LoadColumn(0)] public string ImagePath { get; set; } = string.Empty;
    }

    public class ImagePrediction
    {
        [ColumnName("softmaxout_1")]
        public float[] PredictedLabels { get; set; } = Array.Empty<float>();
    }

    public class FileDNA
    {
        private readonly ITagService _tagService;
        private readonly ILogger<FileDNA> _logger;
        private readonly MLContext _mlContext;
        private readonly string _modelPath;
        private PredictionEngine<ImageData, ImagePrediction>? _predictionEngine;
        private readonly SemaphoreSlim _throttle = new(1, 1);
        private readonly string[] _labels = { "outdoor", "indoor", "person", "food", "text", "document", "screenshot", "nature", "vehicle", "animal", "art", "night", "group", "close-up", "landscape" };

        public FileDNA(ITagService tagService, ILogger<FileDNA> logger)
        {
            _tagService = tagService;
            _logger = logger;
            _mlContext = new MLContext(seed: 1);
            
            // In a real build, the ONNX model is extracted from embedded resources to AppData
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "ai");
            Directory.CreateDirectory(appData);
            _modelPath = Path.Combine(appData, "mobilenetv3.onnx");
        }

        public async Task ScanDirectoryAsync(string directoryPath)
        {
            if (!File.Exists(_modelPath)) return; // Skip if model not deployed

            await Task.Run(async () =>
            {
                try
                {
                    if (_predictionEngine == null) InitializeModel();

                    var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var file in files)
                    {
                        await _throttle.WaitAsync();
                        try
                        {
                            var existingTags = _tagService.ReadAITags(file);
                            if (existingTags.Any()) continue; // Already scanned

                            var prediction = _predictionEngine!.Predict(new ImageData { ImagePath = file });
                            var topTags = GetTopLabels(prediction.PredictedLabels, 3, 0.4f);
                            
                            if (topTags.Any())
                            {
                                _tagService.WriteAITags(file, topTags);
                            }
                        }
                        finally
                        {
                            _throttle.Release();
                            await Task.Delay(250); // Throttle to ~4 images per second
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FileDNA Scan failed.");
                }
            }).ConfigureAwait(false);
        }

        private void InitializeModel()
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(new List<ImageData>());
            var pipeline = _mlContext.Transforms.LoadImages(outputColumnName: "image", imageFolder: "", inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(outputColumnName: "image", imageWidth: 224, imageHeight: 224, inputColumnName: "image"))
                .Append(_mlContext.Transforms.ExtractPixels(outputColumnName: "input_1", inputColumnName: "image"))
                .Append(_mlContext.Transforms.ApplyOnnxModel(modelFile: _modelPath, outputColumnNames: new[] { "softmaxout_1" }, inputColumnNames: new[] { "input_1" }));

            var model = pipeline.Fit(dataView);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(model);
        }

        private List<string> GetTopLabels(float[] probabilities, int count, float threshold)
        {
            if (probabilities == null || probabilities.Length == 0) return new List<string>();

            return probabilities
                .Select((p, i) => new { Prob = p, Index = i })
                .Where(x => x.Prob >= threshold && x.Index < _labels.Length)
                .OrderByDescending(x => x.Prob)
                .Take(count)
                .Select(x => _labels[x.Index])
                .ToList();
        }
    }
}