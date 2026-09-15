using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiftVault.Services;

namespace RiftVault.AI
{
    public class AIEngine
    {
        private readonly SmartShelf _smartShelf;
        private readonly FileDNA _fileDNA;
        private readonly DuplicateBrain _duplicateBrain;
        private readonly AutoOrganizer _autoOrganizer;
        private readonly LocalIntelligenceEngine _localIntelligence;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AIEngine> _logger;

        public LocalIntelligenceEngine LocalIntelligence => _localIntelligence;

        public AIEngine(
            SmartShelf smartShelf, 
            FileDNA fileDNA, 
            DuplicateBrain duplicateBrain, 
            AutoOrganizer autoOrganizer,
            LocalIntelligenceEngine localIntelligence,
            ISettingsService settingsService,
            ILogger<AIEngine> logger)
        {
            _smartShelf = smartShelf;
            _fileDNA = fileDNA;
            _duplicateBrain = duplicateBrain;
            _autoOrganizer = autoOrganizer;
            _localIntelligence = localIntelligence;
            _settingsService = settingsService;
            _logger = logger;
        }

        public void RecordFileAccess(string filePath)
        {
            if (_settingsService.Current.EnableSmartShelf)
            {
                Task.Run(() => _smartShelf.RecordAccess(filePath));
            }
        }

        public async Task AnalyzeFolderAsync(string folderPath)
        {
            if (!_settingsService.Current.EnableFileDNA) return;

            try
            {
                // Runs on background thread, throttled internally
                await _fileDNA.ScanDirectoryAsync(folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FileDNA analysis failed.");
            }
        }
    }
}