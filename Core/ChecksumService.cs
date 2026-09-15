using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RiftVault.Core
{
    public interface IChecksumService
    {
        Task<string> CalculateMD5Async(string filePath, CancellationToken ct);
        Task<string> CalculateSHA256Async(string filePath, CancellationToken ct);
        Task<string> CalculateSHA512Async(string filePath, CancellationToken ct);
    }

    public class ChecksumService : IChecksumService
    {
        public async Task<string> CalculateMD5Async(string filePath, CancellationToken ct)
        {
            using var md5 = MD5.Create();
            return await ComputeHashAsync(md5, filePath, ct);
        }

        public async Task<string> CalculateSHA256Async(string filePath, CancellationToken ct)
        {
            using var sha256 = SHA256.Create();
            return await ComputeHashAsync(sha256, filePath, ct);
        }

        public async Task<string> CalculateSHA512Async(string filePath, CancellationToken ct)
        {
            using var sha512 = SHA512.Create();
            return await ComputeHashAsync(sha512, filePath, ct);
        }

        private async Task<string> ComputeHashAsync(HashAlgorithm algorithm, string filePath, CancellationToken ct)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
            byte[] hashBytes = await algorithm.ComputeHashAsync(stream, ct);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}