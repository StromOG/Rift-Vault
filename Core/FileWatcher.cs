using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RiftVault.Core
{
    public class FileWatcher : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadDirectoryChangesW(
            IntPtr hDirectory,
            IntPtr lpBuffer,
            uint nBufferLength,
            bool bWatchSubtree,
            uint dwNotifyFilter,
            out uint lpBytesReturned,
            IntPtr lpOverlapped,
            IntPtr lpCompletionRoutine);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint FILE_LIST_DIRECTORY = 0x0001;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        private const uint FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001;
        private const uint FILE_NOTIFY_CHANGE_DIR_NAME = 0x00000002;
        private const uint FILE_NOTIFY_CHANGE_SIZE = 0x00000008;
        private const uint FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010;

        private IntPtr _hDir = IntPtr.Zero;
        private CancellationTokenSource? _cts;
        private Task? _watchTask;

        public event Action<string>? OnFileChanged;

        public void StartWatching(string directoryPath)
        {
            StopWatching();

            _hDir = CreateFile(
                directoryPath,
                FILE_LIST_DIRECTORY,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (_hDir == new IntPtr(-1)) return;

            _cts = new CancellationTokenSource();
            _watchTask = Task.Run(() => WatchLoop(_cts.Token), _cts.Token);
        }

        private void WatchLoop(CancellationToken ct)
        {
            uint bufferSize = 32768;
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool success = ReadDirectoryChangesW(
                        _hDir,
                        buffer,
                        bufferSize,
                        false,
                        FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_DIR_NAME | FILE_NOTIFY_CHANGE_SIZE | FILE_NOTIFY_CHANGE_LAST_WRITE,
                        out uint bytesReturned,
                        IntPtr.Zero,
                        IntPtr.Zero);

                    if (success && bytesReturned > 0)
                    {
                        // Parse FILE_NOTIFY_INFORMATION buffer here if specific files are needed.
                        // For performance, we trigger a generic refresh event.
                        OnFileChanged?.Invoke("Changed");
                    }
                    else
                    {
                        break;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void StopWatching()
        {
            _cts?.Cancel();
            if (_hDir != IntPtr.Zero && _hDir != new IntPtr(-1))
            {
                CloseHandle(_hDir);
                _hDir = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            StopWatching();
            _cts?.Dispose();
        }
    }
}