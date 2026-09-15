using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.Core;
using RiftVault.Models;

namespace RiftVault.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {
        private readonly ISearchEngine _searchEngine;
        private CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FileItem> _results = new();

        [ObservableProperty]
        private bool _isSearching;

        public SearchViewModel(ISearchEngine searchEngine)
        {
            _searchEngine = searchEngine;
        }

        partial void OnSearchQueryChanged(string value)
        {
            PerformSearch(value);
        }

        private async void PerformSearch(string query)
        {
            _searchCts?.Cancel();
            if (string.IsNullOrWhiteSpace(query))
            {
                Results.Clear();
                IsSearching = false;
                return;
            }

            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            try
            {
                IsSearching = true;
                
                // Add a small debounce delay
                await Task.Delay(200, ct);

                var searchResults = await _searchEngine.GlobalSearchAsync(query, ct);
                
                if (ct.IsCancellationRequested) return;

                App.Current.Dispatcher.Invoke(() =>
                {
                    Results.Clear();
                    foreach (var item in searchResults) Results.Add(item);
                });
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (!ct.IsCancellationRequested) IsSearching = false;
            }
        }
    }
}