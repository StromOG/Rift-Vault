using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RiftVault.Services
{
    public interface ITagService
    {
        void AddUserTag(string filePath, string tagHexColor);
        void RemoveUserTag(string filePath, string tagHexColor);
        List<string> ReadUserTags(string filePath);
        
        void WriteAITags(string filePath, List<string> tags);
        List<string> ReadAITags(string filePath);
    }

    public class TagService : ITagService
    {
        // Windows supports Alternate Data Streams natively via standard File I/O using the colon syntax.
        private const string UserTagStream = ":RiftUserTags";
        private const string AITagStream = ":RiftAITags";

        public void AddUserTag(string filePath, string tagHexColor)
        {
            try
            {
                var tags = ReadUserTags(filePath);
                if (!tags.Contains(tagHexColor))
                {
                    tags.Add(tagHexColor);
                    File.WriteAllText(filePath + UserTagStream, string.Join(",", tags));
                }
            }
            catch { /* Ignore ADS errors on unsupported file systems (e.g., FAT32) */ }
        }

        public void RemoveUserTag(string filePath, string tagHexColor)
        {
            try
            {
                var tags = ReadUserTags(filePath);
                if (tags.Remove(tagHexColor))
                {
                    if (tags.Count == 0) File.Delete(filePath + UserTagStream);
                    else File.WriteAllText(filePath + UserTagStream, string.Join(",", tags));
                }
            }
            catch { }
        }

        public List<string> ReadUserTags(string filePath)
        {
            try
            {
                string streamPath = filePath + UserTagStream;
                if (File.Exists(streamPath)) // File.Exists works for ADS in modern .NET
                {
                    string content = File.ReadAllText(streamPath);
                    return content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            catch { }
            return new List<string>();
        }

        public void WriteAITags(string filePath, List<string> tags)
        {
            try
            {
                File.WriteAllText(filePath + AITagStream, string.Join(",", tags));
            }
            catch { }
        }

        public List<string> ReadAITags(string filePath)
        {
            try
            {
                string streamPath = filePath + AITagStream;
                if (File.Exists(streamPath))
                {
                    string content = File.ReadAllText(streamPath);
                    return content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            catch { }
            return new List<string>();
        }
    }
}