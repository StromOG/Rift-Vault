using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RiftVault.AI;
using RiftVault.Models;

namespace RiftVault.ViewModels
{
    public partial class SmartShelfViewModel : ObservableObject
    {
        private readonly SmartShelf _smartShelf;

        [ObservableProperty]
        private ObservableCollection<FileItem> _predictedItems = new();

        public SmartShelfViewModel(SmartShelf smartShelf)
        {
            _smartShelf = smartShelf;
        }

        public void RefreshPredictions()
        {
            Task.Run(() =>
            {
                var paths = _smartShelf.PredictNextFiles(6);
                App.Current.Dispatcher.Invoke(() =>
                {
                    PredictedItems.Clear();
                    foreach (var path in paths)
                    {
                        var info = new FileInfo(path);
                        PredictedItems.Add(new FileItem
                        {
                            Name = info.Name,
                            Path = info.FullName,
                            Extension = info.Extension,
                            IsDirectory = false,
                            Size = info.Length,
                            DateModified = info.LastWriteTime
                        });
                    }
                });
            });
        }
    }
}