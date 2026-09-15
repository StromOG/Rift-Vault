using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RiftVault.Services
{
    public interface ITeleportService
    {
        string? GetTeleportPath(int slot);
        void SetTeleportPath(int slot, string path);
        Dictionary<int, string> GetAllTeleports();
    }

    public class TeleportService : ITeleportService
    {
        private readonly string _teleportFile;
        private readonly Dictionary<int, string> _teleports = new();

        public TeleportService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault");
            Directory.CreateDirectory(appData);
            _teleportFile = Path.Combine(appData, "teleports.json");
            Load();
        }

        private void Load()
        {
            if (File.Exists(_teleportFile))
            {
                try
                {
                    string json = File.ReadAllText(_teleportFile);
                    var data = JsonSerializer.Deserialize<Dictionary<int, string>>(json);
                    if (data != null)
                    {
                        foreach (var kvp in data) _teleports[kvp.Key] = kvp.Value;
                    }
                }
                catch { }
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_teleports);
                File.WriteAllText(_teleportFile, json);
            }
            catch { }
        }

        public string? GetTeleportPath(int slot)
        {
            return _teleports.TryGetValue(slot, out var path) ? path : null;
        }

        public void SetTeleportPath(int slot, string path)
        {
            if (slot < 1 || slot > 9) return;
            _teleports[slot] = path;
            Save();
        }

        public Dictionary<int, string> GetAllTeleports()
        {
            return new Dictionary<int, string>(_teleports);
        }
    }
}