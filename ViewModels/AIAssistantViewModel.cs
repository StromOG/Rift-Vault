using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RiftVault.AI;
using RiftVault.Services;

namespace RiftVault.ViewModels
{
    public class ChatMessageViewModel : ObservableObject
    {
        public bool IsUser { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm");
        public string Provider { get; set; } = "Assistant";
        public string LatencyText { get; set; } = "";
        public ObservableCollection<AIActionResult> Actions { get; set; } = new();
        public ObservableCollection<string> NextOptions { get; set; } = new();
        public bool HasNextOptions => NextOptions.Count > 0;
        public bool HasActions => Actions.Count > 0;
        public bool HasCodeSnippet => Text.Contains("```");

        private bool _isCopied;
        public bool IsCopied
        {
            get => _isCopied;
            set => SetProperty(ref _isCopied, value);
        }

        public string ExtractedCode
        {
            get
            {
                if (!Text.Contains("```")) return string.Empty;
                int start = Text.IndexOf("```");
                int lineEnd = Text.IndexOf('\n', start);
                int end = Text.LastIndexOf("```");
                if (lineEnd >= 0 && end > lineEnd)
                {
                    return Text.Substring(lineEnd + 1, end - lineEnd - 1).Trim();
                }
                return string.Empty;
            }
        }
    }

    public class ChatSessionViewModel : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New Conversation";
        public string FolderPath { get; set; } = "";
        public string Timestamp { get; set; } = DateTime.Now.ToString("MMM dd, HH:mm");
        public int MessageCount { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; } = new();
    }

    public partial class AIAssistantViewModel : ObservableObject
    {
        private readonly LocalIntelligenceEngine _engine;
        private readonly ISettingsService? _settingsService;
        private CancellationTokenSource? _generationCts;

        public event Action<AIActionResult>? ActionRequested;
        public event Action? AISettingsRequested;

        [RelayCommand]
        public void OpenAISettings()
        {
            AISettingsRequested?.Invoke();
        }

        [RelayCommand]
        public void ToggleHistory()
        {
            ActiveTab = ActiveTab == "History" ? "Chat" : "History";
        }

        // ─── Top Navigation Tabs ("Chat", "History") ─────────────────────
        [ObservableProperty]
        private string _activeTab = "Chat";

        [ObservableProperty]
        private ObservableCollection<ChatMessageViewModel> _messages = new();

        [ObservableProperty]
        private string _promptInput = string.Empty;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private string _currentDirectory = string.Empty;

        [ObservableProperty]
        private string _directoryContextText = "Ready";

        [ObservableProperty]
        private string _selectedFileText = "No file selected";

        [ObservableProperty]
        private string _activeEngineStatus = "Runs on your PC · Ready";

        // ─── Setup & Provider State ────────────────────────────────────
        [ObservableProperty]
        private string _selectedProvider = "BuiltIn"; // "BuiltIn", "Ollama", "Cloud"

        [ObservableProperty]
        private string _ollamaEndpoint = "http://localhost:11434";

        [ObservableProperty]
        private string _selectedOllamaModel = "phi3:mini";

        [ObservableProperty]
        private ObservableCollection<string> _availableOllamaModels = new();

        [ObservableProperty]
        private string _ollamaStatusText = "Click 'Test Connection' to check Ollama";

        [ObservableProperty]
        private bool _isOllamaOnline;

        [ObservableProperty]
        private string _cloudProvider = "OpenAI"; // "OpenAI", "Groq", "Gemini"

        [ObservableProperty]
        private string _cloudApiKey = "";

        [ObservableProperty]
        private string _cloudModel = "gpt-4o-mini";

        // ─── Customization & Behavior ──────────────────────────────────
        [ObservableProperty]
        private string _personality = "Balanced"; // "Balanced", "Technical", "Concise", "Creative"

        [ObservableProperty]
        private string _customSystemPrompt = "";

        [ObservableProperty]
        private double _temperature = 0.7;

        [ObservableProperty]
        private int _maxFilesAnalyzed = 100;

        [ObservableProperty]
        private bool _allowFileOperations = false;

        [ObservableProperty]
        private double _paneWidth = 420.0;

        [ObservableProperty]
        private ObservableCollection<ChatSessionViewModel> _historySessions = new();

        public bool HasHistorySessions => HistorySessions.Count > 0;

        [ObservableProperty]
        private string _searchHistoryQuery = string.Empty;

        // Context state cached from active workspace
        private int _totalFiles;
        private int _totalFolders;
        private long _totalBytes;
        private List<string> _topExtensions = new();
        private List<string> _largestFiles = new();
        private string? _selectedFilePath;
        private string? _selectedFileName;
        private long _selectedFileSize;
        private string? _selectedFileSample;

        public AIAssistantViewModel(LocalIntelligenceEngine? engine = null, ISettingsService? settingsService = null)
        {
            _engine = engine ?? new LocalIntelligenceEngine();
            _settingsService = settingsService;

            LoadSettings();

            SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, () => !IsGenerating && !string.IsNullOrWhiteSpace(PromptInput));
            ClearChatCommand = new RelayCommand(ClearChat);
            QuickPromptCommand = new AsyncRelayCommand<string>(ExecuteQuickPromptAsync);
            ExecuteActionCommand = new RelayCommand<AIActionResult>(ExecuteAction);
            SwitchTabCommand = new RelayCommand<string>(SwitchTab);
            TestOllamaCommand = new AsyncRelayCommand(TestOllamaConnectionAsync);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            SetPaneWidthCommand = new RelayCommand<string>(SetPaneWidth);
            CopyCodeCommand = new RelayCommand<string>(CopyCode);
            SelectProviderCommand = new RelayCommand<string>(p => { if (!string.IsNullOrEmpty(p)) { SelectedProvider = p; SyncEngineSettings(); } });
            StartNewSessionCommand = new RelayCommand(StartNewSession);
            RestoreSessionCommand = new RelayCommand<ChatSessionViewModel>(RestoreSession);
            ClearHistoryCommand = new RelayCommand(ClearHistory);
            ExportChatCommand = new RelayCommand(ExportChat);
            RegenerateLastMessageCommand = new AsyncRelayCommand(RegenerateLastMessageAsync, () => !IsGenerating);
            CopyMessageCommand = new RelayCommand<ChatMessageViewModel>(CopyMessage);
            StopGeneratingCommand = new RelayCommand(StopGenerating, () => IsGenerating);

            // Default initial message
            ResetToWelcomeMessage();
        }

        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand QuickPromptCommand { get; }
        public ICommand ExecuteActionCommand { get; }
        public ICommand SwitchTabCommand { get; }
        public ICommand SelectProviderCommand { get; }
        public ICommand TestOllamaCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand SetPaneWidthCommand { get; }
        public ICommand CopyCodeCommand { get; }
        public ICommand StartNewSessionCommand { get; }
        public ICommand RestoreSessionCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand ExportChatCommand { get; }
        public ICommand RegenerateLastMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }
        public ICommand StopGeneratingCommand { get; }

        private void LoadSettings()
        {
            if (_settingsService?.Current == null) return;
            var s = _settingsService.Current;

            SelectedProvider = s.AIProvider ?? "BuiltIn";
            OllamaEndpoint = s.OllamaEndpoint ?? "http://localhost:11434";
            SelectedOllamaModel = s.OllamaModel ?? "phi3:mini";
            CloudProvider = s.CloudProvider ?? "OpenAI";
            CloudApiKey = s.CloudApiKey ?? "";
            CloudModel = s.CloudModel ?? "gpt-4o-mini";
            Personality = s.AIPersonality ?? "Balanced";
            CustomSystemPrompt = s.AICustomSystemPrompt ?? "";
            Temperature = s.AITemperature;
            MaxFilesAnalyzed = s.AIMaxFilesAnalyzed;
            AllowFileOperations = s.AIAllowFileOperations;
            PaneWidth = s.AIAssistantPaneWidth > 200 ? s.AIAssistantPaneWidth : 420.0;

            SyncEngineSettings();
        }

        public void SaveSettings()
        {
            SyncEngineSettings();

            if (_settingsService?.Current != null)
            {
                var s = _settingsService.Current;
                s.AIProvider = SelectedProvider;
                s.OllamaEndpoint = OllamaEndpoint;
                s.OllamaModel = SelectedOllamaModel;
                s.CloudProvider = CloudProvider;
                s.CloudApiKey = CloudApiKey;
                s.CloudModel = CloudModel;
                s.AIPersonality = Personality;
                s.AICustomSystemPrompt = CustomSystemPrompt;
                s.AITemperature = Temperature;
                s.AIMaxFilesAnalyzed = MaxFilesAnalyzed;
                s.AIAllowFileOperations = AllowFileOperations;
                s.AIAssistantPaneWidth = PaneWidth;

                _settingsService.Save();
            }

            UpdateStatusText();
            ActiveTab = "Chat";
        }

        private void SyncEngineSettings()
        {
            _engine.ActiveProvider = SelectedProvider;
            _engine.LocalLlmEndpoint = OllamaEndpoint.TrimEnd('/') + "/api/generate";
            _engine.LocalLlmModel = SelectedOllamaModel;
            _engine.CloudProvider = CloudProvider;
            _engine.CloudApiKey = CloudApiKey;
            _engine.CloudModel = CloudModel;
            _engine.Personality = Personality;
            _engine.CustomSystemPrompt = CustomSystemPrompt;
            _engine.Temperature = Temperature;
            _engine.MaxFilesAnalyzed = MaxFilesAnalyzed;
            _engine.AllowFileOperations = AllowFileOperations;

            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (SelectedProvider == "Ollama")
            {
                ActiveEngineStatus = $"Ollama · {SelectedOllamaModel}";
            }
            else if (SelectedProvider == "Cloud")
            {
                ActiveEngineStatus = $"{CloudProvider} · {CloudModel}";
            }
            else
            {
                ActiveEngineStatus = "Rift Local Core · 100% Offline";
            }
        }

        public void SwitchTab(string? tab)
        {
            if (!string.IsNullOrEmpty(tab))
            {
                ActiveTab = tab;
                if (tab == "Setup" && AvailableOllamaModels.Count == 0)
                {
                    _ = TestOllamaConnectionAsync();
                }
            }
        }

        public void SetPaneWidth(string? widthStr)
        {
            if (double.TryParse(widthStr, out double w))
            {
                PaneWidth = w;
                if (_settingsService?.Current != null)
                {
                    _settingsService.Current.AIAssistantPaneWidth = w;
                    _settingsService.Save();
                }
            }
        }

        public async Task TestOllamaConnectionAsync()
        {
            OllamaStatusText = "Connecting to Ollama on " + OllamaEndpoint + "...";
            var (isOnline, models, msg) = await _engine.CheckOllamaStatusAsync();
            IsOllamaOnline = isOnline;
            OllamaStatusText = msg;

            AvailableOllamaModels.Clear();
            foreach (var m in models)
            {
                AvailableOllamaModels.Add(m);
            }

            if (models.Count > 0 && !models.Contains(SelectedOllamaModel))
            {
                SelectedOllamaModel = models[0];
            }
        }

        private void ResetToWelcomeMessage()
        {
            Messages.Clear();
            var welcome = new ChatMessageViewModel
            {
                IsUser = false,
                Text = "### 👋 Welcome to Rift Vault AI Copilot\n\nI run directly on your computer to assist with file exploration, smart organization, command generation, and forensics.\n\nClick a quick action below or ask any question!",
                Provider = "Assistant",
                LatencyText = "Ready"
            };
            welcome.NextOptions.Add("📁 Analyze current folder");
            welcome.NextOptions.Add("🔍 Find duplicates");
            welcome.NextOptions.Add("🧹 Propose clean organization");
            welcome.NextOptions.Add("💻 Generate PowerShell script");
            Messages.Add(welcome);
        }

        public void UpdateContext(string currentPath, IEnumerable<FileItemViewModel> items, FileItemViewModel? selectedItem)
        {
            CurrentDirectory = currentPath;
            var itemList = items.ToList();

            _totalFiles = itemList.Count(i => !i.Model.IsDirectory);
            _totalFolders = itemList.Count(i => i.Model.IsDirectory);
            _totalBytes = itemList.Where(i => !i.Model.IsDirectory).Sum(i => i.Model.Size);

            _topExtensions = itemList
                .Where(i => !i.Model.IsDirectory && !string.IsNullOrEmpty(i.Model.Extension))
                .GroupBy(i => i.Model.Extension.ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToList();

            _largestFiles = itemList
                .Where(i => !i.Model.IsDirectory)
                .OrderByDescending(i => i.Model.Size)
                .Take(5)
                .Select(i => $"{i.Model.Name} ({i.DisplaySize})")
                .ToList();

            DirectoryContextText = $"{_totalFiles} files, {_totalFolders} folders · {FormatSize(_totalBytes)}";

            if (selectedItem != null)
            {
                _selectedFilePath = selectedItem.Model.Path;
                _selectedFileName = selectedItem.Model.Name;
                _selectedFileSize = selectedItem.Model.Size;
                SelectedFileText = $"{_selectedFileName} ({selectedItem.DisplaySize})";

                Task.Run(() =>
                {
                    try
                    {
                        if (File.Exists(_selectedFilePath) && selectedItem.Model.Size < 500_000)
                        {
                            _selectedFileSample = File.ReadAllText(_selectedFilePath);
                        }
                        else
                        {
                            _selectedFileSample = null;
                        }
                    }
                    catch
                    {
                        _selectedFileSample = null;
                    }
                });
            }
            else
            {
                _selectedFilePath = null;
                _selectedFileName = null;
                _selectedFileSize = 0;
                _selectedFileSample = null;
                SelectedFileText = "No file selected";
            }
        }

        public async Task SendMessageAsync()
        {
            string prompt = PromptInput.Trim();
            if (string.IsNullOrEmpty(prompt) || IsGenerating) return;

            PromptInput = string.Empty;
            await ProcessUserPromptAsync(prompt);
        }

        public async Task ExecuteQuickPromptAsync(string? prompt)
        {
            if (string.IsNullOrEmpty(prompt) || IsGenerating) return;
            ActiveTab = "Chat";
            await ProcessUserPromptAsync(prompt);
        }

        private async Task ProcessUserPromptAsync(string prompt, bool isRegeneration = false)
        {
            if (!isRegeneration)
            {
                Messages.Add(new ChatMessageViewModel
                {
                    IsUser = true,
                    Text = prompt
                });
            }

            IsGenerating = true;
            (SendMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (RegenerateLastMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (StopGeneratingCommand as RelayCommand)?.NotifyCanExecuteChanged();

            _generationCts = new CancellationTokenSource();

            var snapshot = new AIContextSnapshot
            {
                CurrentPath = CurrentDirectory,
                TotalFiles = _totalFiles,
                TotalFolders = _totalFolders,
                TotalBytes = _totalBytes,
                TopExtensions = _topExtensions,
                LargestFiles = _largestFiles,
                SelectedFilePath = _selectedFilePath,
                SelectedFileName = _selectedFileName,
                SelectedFileSize = _selectedFileSize,
                SelectedFileSample = _selectedFileSample
            };

            try
            {
                var response = await _engine.QueryAsync(prompt, snapshot, _generationCts.Token);

                var aiMsg = new ChatMessageViewModel
                {
                    IsUser = false,
                    Text = response.Text,
                    Provider = response.Provider,
                    LatencyText = $"⚡ {response.LatencyMs:N0} ms"
                };

                foreach (var action in response.Actions)
                {
                    aiMsg.Actions.Add(action);
                }

                foreach (var opt in response.NextOptions)
                {
                    aiMsg.NextOptions.Add(opt);
                }

                Messages.Add(aiMsg);
            }
            catch (OperationCanceledException)
            {
                Messages.Add(new ChatMessageViewModel
                {
                    IsUser = false,
                    Text = "*Generation stopped.*",
                    Provider = "Assistant"
                });
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessageViewModel
                {
                    IsUser = false,
                    Text = $"⚠️ Error: {ex.Message}",
                    Provider = "Assistant"
                });
            }
            finally
            {
                IsGenerating = false;
                (SendMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (RegenerateLastMessageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (StopGeneratingCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        public void CopyMessage(ChatMessageViewModel? msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.Text)) return;
            try
            {
                Clipboard.SetText(msg.Text);
                msg.IsCopied = true;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                timer.Tick += (s, e) =>
                {
                    msg.IsCopied = false;
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        }

        public void StopGenerating()
        {
            _generationCts?.Cancel();
        }

        public async Task RegenerateLastMessageAsync()
        {
            if (IsGenerating) return;

            var lastUserMsg = Messages.LastOrDefault(m => m.IsUser);
            if (lastUserMsg == null || string.IsNullOrWhiteSpace(lastUserMsg.Text)) return;

            while (Messages.Count > 0 && !Messages.Last().IsUser)
            {
                Messages.RemoveAt(Messages.Count - 1);
            }

            await ProcessUserPromptAsync(lastUserMsg.Text, isRegeneration: true);
        }

        private void ExecuteAction(AIActionResult? action)
        {
            if (action == null) return;

            if (action.ActionType == "CopyCommand")
            {
                try
                {
                    Clipboard.SetText(action.Parameter);
                    MessageBox.Show("Command copied to clipboard!", "Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
                return;
            }

            ActionRequested?.Invoke(action);
        }

        public void CopyCode(string? code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                try
                {
                    Clipboard.SetText(code);
                    MessageBox.Show("Code block copied to clipboard!", "Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            }
        }

        private void ClearChat()
        {
            SaveActiveSessionToHistory();
            ResetToWelcomeMessage();
        }

        public void StartNewSession()
        {
            SaveActiveSessionToHistory();
            ResetToWelcomeMessage();
            ActiveTab = "Chat";
        }

        private void SaveActiveSessionToHistory()
        {
            var userMsgs = Messages.Where(m => m.IsUser).ToList();
            if (userMsgs.Count == 0) return;

            string title = userMsgs.First().Text;
            if (title.Length > 35) title = title.Substring(0, 32) + "...";

            var session = new ChatSessionViewModel
            {
                Title = title,
                FolderPath = CurrentDirectory,
                Timestamp = DateTime.Now.ToString("MMM dd, HH:mm"),
                MessageCount = Messages.Count,
                Messages = new List<ChatMessageViewModel>(Messages)
            };

            HistorySessions.Insert(0, session);
            if (HistorySessions.Count > 30)
            {
                HistorySessions.RemoveAt(HistorySessions.Count - 1);
            }
            OnPropertyChanged(nameof(HasHistorySessions));
        }

        public void RestoreSession(ChatSessionViewModel? session)
        {
            if (session == null) return;
            Messages.Clear();
            foreach (var m in session.Messages)
            {
                Messages.Add(m);
            }
            ActiveTab = "Chat";
        }

        public void ClearHistory()
        {
            HistorySessions.Clear();
            OnPropertyChanged(nameof(HasHistorySessions));
        }

        public void ExportChat()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# Rift Vault AI Chat Export");
                sb.AppendLine($"**Date**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  ");
                sb.AppendLine($"**Folder**: `{CurrentDirectory}`\n");
                sb.AppendLine("---");

                foreach (var m in Messages)
                {
                    string speaker = m.IsUser ? "🧑 **You**" : $"🤖 **{m.Provider}**";
                    sb.AppendLine($"\n### {speaker} ({m.Timestamp})\n");
                    sb.AppendLine(m.Text);
                }

                string exportFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"RiftVault_ChatExport_{DateTime.Now:yyyyMMdd_HHmmss}.md"
                );

                File.WriteAllText(exportFile, sb.ToString());
                MessageBox.Show($"Chat conversation exported to:\n{exportFile}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export chat: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {suffixes[order]}";
        }
    }
}
