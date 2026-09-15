using System;
using System.IO;
using System.Text.Json;
using RiftVault.Models;

namespace RiftVault.Services
{
    public interface ISettingsService
    {
        AppSettings Current { get; }
        void Save();
        void ResetToDefault();
        void Export(string destinationPath);
        bool Import(string sourcePath);
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private AppSettings _current;

        public AppSettings Current => _current;

        public SettingsService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault");
            Directory.CreateDirectory(appData);
            _settingsPath = Path.Combine(appData, "settings.json");
            
            _current = Load();
        }

        private AppSettings Load()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (settings.ToolbarBlocks == null || settings.ToolbarBlocks.Count == 0 || !settings.ToolbarBlocks.Any(b => b.IsVisible))
                        {
                            settings.ToolbarBlocks = AppSettings.GetDefaultToolbarBlocks();
                        }
                        else
                        {
                            // Ensure any missing block definitions are appended to the available pool
                            var defaultBlocks = AppSettings.GetDefaultToolbarBlocks();
                            foreach (var def in defaultBlocks)
                            {
                                if (!settings.ToolbarBlocks.Any(b => b.Id == def.Id))
                                {
                                    def.IsVisible = false;
                                    def.OrderIndex = settings.ToolbarBlocks.Count;
                                    settings.ToolbarBlocks.Add(def);
                                }
                            }
                        }
                        return settings;
                    }
                }
                catch { /* Fallback to default */ }
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_current, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch { /* Ignore save errors in sandbox */ }
        }

        public void ResetToDefault()
        {
            _current = new AppSettings();
            Save();
        }

        public void Export(string destinationPath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_current, options);
            File.WriteAllText(destinationPath, json);
        }

        public bool Import(string sourcePath)
        {
            if (!File.Exists(sourcePath)) return false;
            try
            {
                string json = File.ReadAllText(sourcePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _current = settings;
                    Save();
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}