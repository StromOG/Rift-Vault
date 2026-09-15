using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RiftVault.Core
{
    public interface IMetadataReader
    {
        Task<Dictionary<string, string>> GetMetadataAsync(string filePath);
    }

    public class MetadataReader : IMetadataReader
    {
        public async Task<Dictionary<string, string>> GetMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var metadata = new Dictionary<string, string>();
                
                try
                {
                    // Basic File Info
                    var info = new FileInfo(filePath);
                    metadata["Size"] = info.Length.ToString();
                    metadata["Created"] = info.CreationTime.ToString("g");
                    metadata["Modified"] = info.LastWriteTime.ToString("g");
                    metadata["Extension"] = info.Extension;

                    // In a full implementation, this would use Windows Property System (IPropertyStore)
                    // via COM interop to read EXIF, ID3, and PDF metadata natively without heavy libraries.
                    // For this sandbox, we return the basic metadata.
                }
                catch
                {
                    // Ignore access errors
                }

                return metadata;
            });
        }
    }
}