using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RiftVault.Models
{
    public partial class FileItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _extension = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private DateTime _dateCreated;

        [ObservableProperty]
        private BitmapSource? _thumbnail;

        [ObservableProperty]
        private bool _isThumbnailLoaded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private List<FileTag> _tags = new();

        [ObservableProperty]
        private List<string> _aiTags = new();

        public string DisplaySize => IsDirectory ? "" : FormatSize(Size);

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
    }
}