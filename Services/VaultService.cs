using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RiftVault.Services
{
    public interface IVaultService
    {
        bool IsLocked { get; }
        bool Unlock(string password);
        void Lock();
        Task EncryptFileToVaultAsync(string sourceFile, string vaultDir, CancellationToken ct);
        Task DecryptFileFromVaultAsync(string vaultFile, string destinationFile, CancellationToken ct);
    }

    public class VaultService : IVaultService
    {
        private byte[]? _sessionKey;
        public bool IsLocked => _sessionKey == null;

        private const int Iterations = 100000;
        private const int SaltSize = 16;

        public bool Unlock(string password)
        {
            // In a real app, we verify against a stored hash.
            // Here we derive the key and assume success for the session.
            using var deriveBytes = new Rfc2898DeriveBytes(password, new byte[SaltSize] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, Iterations, HashAlgorithmName.SHA256);
            _sessionKey = deriveBytes.GetBytes(32); // 256-bit key
            return true;
        }

        public void Lock()
        {
            if (_sessionKey != null)
            {
                Array.Clear(_sessionKey, 0, _sessionKey.Length);
                _sessionKey = null;
            }
        }

        public async Task EncryptFileToVaultAsync(string sourceFile, string vaultDir, CancellationToken ct)
        {
            if (IsLocked || _sessionKey == null) throw new UnauthorizedAccessException("Vault is locked.");

            string destFile = Path.Combine(vaultDir, Path.GetFileName(sourceFile) + ".rvault");

            await Task.Run(async () =>
            {
                using var aes = Aes.Create();
                aes.Key = _sessionKey;
                aes.GenerateIV();

                using var fsOut = new FileStream(destFile, FileMode.Create);
                fsOut.Write(aes.IV, 0, aes.IV.Length);

                using var cs = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
                using var fsIn = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
                await fsIn.CopyToAsync(cs, ct);
            }, ct).ConfigureAwait(false);
        }

        public async Task DecryptFileFromVaultAsync(string vaultFile, string destinationFile, CancellationToken ct)
        {
            if (IsLocked || _sessionKey == null) throw new UnauthorizedAccessException("Vault is locked.");

            await Task.Run(async () =>
            {
                using var fsIn = new FileStream(vaultFile, FileMode.Open, FileAccess.Read);
                byte[] iv = new byte[16];
                fsIn.Read(iv, 0, iv.Length);

                using var aes = Aes.Create();
                aes.Key = _sessionKey;
                aes.IV = iv;

                using var cs = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var fsOut = new FileStream(destinationFile, FileMode.Create);
                await cs.CopyToAsync(fsOut, ct);
            }, ct).ConfigureAwait(false);
        }
    }
}