using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using RiftVault.Win32;

namespace RiftVault.Core
{
    public interface IThumbnailCache
    {
        Task<BitmapSource?> GetThumbnailAsync(string path, int size, CancellationToken ct);
        void Clear();
    }

    public class ThumbnailCache : IThumbnailCache
    {
        private readonly int _capacity = 512;
        private readonly Dictionary<string, LinkedListNode<CacheItem>> _cache = new();
        private readonly LinkedList<CacheItem> _lruList = new();
        private readonly object _lock = new();

        private class CacheItem
        {
            public string Path { get; }
            public BitmapSource Thumbnail { get; }

            public CacheItem(string path, BitmapSource thumbnail)
            {
                Path = path;
                Thumbnail = thumbnail;
            }
        }

        public async Task<BitmapSource?> GetThumbnailAsync(string path, int size, CancellationToken ct)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Thumbnail;
                }
            }

            // Not in cache, load async
            var bitmap = await Task.Run(() => ShellHelper.GetThumbnail(path, size, size), ct).ConfigureAwait(false);

            if (bitmap != null && !ct.IsCancellationRequested)
            {
                lock (_lock)
                {
                    if (_cache.Count >= _capacity)
                    {
                        var last = _lruList.Last;
                        if (last != null)
                        {
                            _cache.Remove(last.Value.Path);
                            _lruList.RemoveLast();
                        }
                    }

                    var newItem = new CacheItem(path, bitmap);
                    var newNode = new LinkedListNode<CacheItem>(newItem);
                    _lruList.AddFirst(newNode);
                    _cache[path] = newNode;
                }
            }

            return bitmap;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }
    }
}