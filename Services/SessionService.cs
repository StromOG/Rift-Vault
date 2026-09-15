using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RiftVault.Models;

namespace RiftVault.Services
{
    public interface ISessionService
    {
        WorkspaceSession LoadSession(string name);
        void SaveSession(WorkspaceSession session);
        List<string> GetAvailableSessions();
    }

    public class SessionService : ISessionService
    {
        private readonly string _sessionDir;

        public SessionService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "sessions");
            Directory.CreateDirectory(appData);
            _sessionDir = appData;
        }

        public WorkspaceSession LoadSession(string name)
        {
            string path = Path.Combine(_sessionDir, $"{name}.rvsession");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var session = JsonSerializer.Deserialize<WorkspaceSession>(json);
                    if (session != null) return session;
                }
                catch { /* Corrupted session */ }
            }
            return new WorkspaceSession { Name = name };
        }

        public void SaveSession(WorkspaceSession session)
        {
            try
            {
                session.LastSaved = DateTime.Now;
                string path = Path.Combine(_sessionDir, $"{session.Name}.rvsession");
                string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { /* Ignore save errors */ }
        }

        public List<string> GetAvailableSessions()
        {
            var sessions = new List<string>();
            try
            {
                var files = Directory.GetFiles(_sessionDir, "*.rvsession");
                foreach (var file in files)
                {
                    sessions.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch { }
            return sessions;
        }
    }
}