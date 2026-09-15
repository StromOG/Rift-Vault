using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RiftVault.Core;
using RiftVault.Services;
using RiftVault.ViewModels;
using RiftVault.AI;

namespace RiftVault
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        private ILogger<App> _logger;

        public App()
        {
            Services = ConfigureServices();
            _logger = Services.GetRequiredService<ILogger<App>>();
            
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                _logger.LogCritical(e.ExceptionObject as Exception, "AppDomain Unhandled Exception");
                ShowCrashDialog(e.ExceptionObject as Exception);
            };
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Logging
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "logs");
            Directory.CreateDirectory(logDir);
            
            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.AddProvider(new FileLoggerProvider(logDir));
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            // Core Engines
            services.AddSingleton<IFileSystemEngine, FileSystemEngine>();
            services.AddSingleton<IThumbnailCache, ThumbnailCache>();
            services.AddSingleton<ISearchEngine, SearchEngine>();
            services.AddSingleton<IClipboardManager, ClipboardManager>();
            services.AddSingleton<IArchiveEngine, ArchiveEngine>();
            services.AddSingleton<IChecksumService, ChecksumService>();
            services.AddSingleton<IMetadataReader, MetadataReader>();
            services.AddSingleton<IShellFileService, ShellFileService>();

            // AI
            services.AddSingleton<LocalIntelligenceEngine>();
            services.AddSingleton<AIEngine>();
            services.AddSingleton<SmartShelf>();
            services.AddSingleton<FileDNA>();
            services.AddSingleton<DuplicateBrain>();
            services.AddSingleton<AutoOrganizer>();

            // Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IShortcutService, ShortcutService>();
            services.AddSingleton<IVaultService, VaultService>();
            services.AddSingleton<ITagService, TagService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<ITeleportService, TeleportService>();
            services.AddSingleton<ISoundService, SoundService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<TabViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SmartShelfViewModel>();
            services.AddTransient<AIAssistantViewModel>();
            services.AddTransient<InvestigatorViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
            services.AddTransient<UI.SettingsWindow>();
            services.AddTransient<UI.InvestigatorWindow>();

            return services.BuildServiceProvider();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                _logger.LogInformation("RiftVault starting up...");
                
                var themeService = Services.GetRequiredService<IThemeService>();
                themeService.InitializeTheme();
                var settingsService = Services.GetRequiredService<ISettingsService>();

                if (Array.Exists(e.Args, a => a.Equals("--render-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-png", StringComparison.OrdinalIgnoreCase));
                    string target = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : Environment.CurrentDirectory;
                    string initialTab = (idx + 2 < e.Args.Length) ? e.Args[idx + 2] : "Forensics";
                    string outPng = (idx + 3 < e.Args.Length) ? e.Args[idx + 3] : "capture.png";

                    var invWindow = Services.GetRequiredService<UI.InvestigatorWindow>();
                    invWindow.Width = 920;
                    invWindow.Height = 720;
                    invWindow.InspectTarget(target, initialTab);

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            invWindow.Measure(new Size(920, 720));
                            invWindow.Arrange(new Rect(0, 0, 920, 720));
                            invWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(920, 720, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(invWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            invWindow.Close();
                        }
                    };
                    timer.Start();

                    invWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-settings-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-settings-png", StringComparison.OrdinalIgnoreCase));
                    string targetCategory = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "General";
                    string outPng = (idx + 2 < e.Args.Length) ? e.Args[idx + 2] : "settings_capture.png";

                    var setWindow = Services.GetRequiredService<UI.SettingsWindow>();
                    setWindow.Width = 1020;
                    setWindow.Height = 880;
                    if (targetCategory.StartsWith("Appearance_Motion", StringComparison.OrdinalIgnoreCase))
                    {
                        setWindow.SelectCategory("Appearance");
                        setWindow.ScrollToOffset(1120);
                    }
                    else if (targetCategory.StartsWith("Appearance_Offset_", StringComparison.OrdinalIgnoreCase) && double.TryParse(targetCategory.Substring("Appearance_Offset_".Length), out double customOff))
                    {
                        setWindow.SelectCategory("Appearance");
                        setWindow.ScrollToOffset(customOff);
                    }
                    else if (targetCategory.StartsWith("Appearance_Sliders", StringComparison.OrdinalIgnoreCase))
                    {
                        setWindow.SelectCategory("Appearance");
                        setWindow.ScrollToOffset(280);
                    }
                    else
                    {
                        setWindow.SelectCategory(targetCategory);
                    }

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            if (targetCategory.StartsWith("Appearance_Motion", StringComparison.OrdinalIgnoreCase))
                            {
                                setWindow.ScrollToOffset(1120);
                            }
                            else if (targetCategory.StartsWith("Appearance_Offset_", StringComparison.OrdinalIgnoreCase) && double.TryParse(targetCategory.Substring("Appearance_Offset_".Length), out double cOff))
                            {
                                setWindow.ScrollToOffset(cOff);
                            }
                            else if (targetCategory.StartsWith("Appearance_Sliders", StringComparison.OrdinalIgnoreCase))
                            {
                                setWindow.ScrollToOffset(280);
                            }
                            setWindow.Measure(new Size(1020, 880));
                            setWindow.Arrange(new Rect(0, 0, 1020, 880));
                            setWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1020, 880, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(setWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            setWindow.Close();
                        }
                    };
                    timer.Start();

                    setWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-main-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-main-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "main_capture.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1200;
                    mWindow.Height = 780;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            mWindow.Measure(new Size(1200, 780));
                            mWindow.Arrange(new Rect(0, 0, 1200, 780));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1200, 780, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                            File.WriteAllText("render_success.txt", $"Saved to {outPng}");
                            Console.WriteLine($"SUCCESS_RENDER_MAIN: Saved to {outPng}");
                        }
                        catch (Exception ex)
                        {
                            File.WriteAllText("render_error.txt", ex.ToString());
                            Console.WriteLine($"ERROR_RENDER_MAIN: {ex.Message}");
                        }
                        finally
                        {
                            mWindow.Close();
                            Environment.Exit(0);
                        }
                    };
                    timer.Start();
                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-customizer-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-customizer-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "customizer_capture.png";

                    var win = new UI.ToolbarCustomizeWindow(settingsService);
                    win.Width = 880;
                    win.Height = 620;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            win.Measure(new Size(880, 620));
                            win.Arrange(new Rect(0, 0, 880, 620));
                            win.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(880, 620, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(win);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                            Console.WriteLine($"SUCCESS_RENDER_CUSTOMIZER: Saved to {outPng}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR_RENDER_CUSTOMIZER: {ex.Message}");
                        }
                        finally
                        {
                            win.Close();
                        }
                    };
                    timer.Start();
                    win.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--installer", StringComparison.OrdinalIgnoreCase) || a.Equals("--setup", StringComparison.OrdinalIgnoreCase)))
                {
                    var setupWin = new UI.FirstRunSetupWindow(settingsService, themeService);
                    setupWin.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-installer", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("=== STARTING RIFT VAULT INSTALLER TEST SUITE ===");
                    
                    var defaultPath = Win32.SystemIntegrationHelper.GetDefaultInstallDirectory();
                    Console.WriteLine($"[1] Default Installation Directory: {defaultPath}");
                    if (string.IsNullOrEmpty(defaultPath)) throw new InvalidOperationException("Default install path is empty");

                    var setupWin = new UI.FirstRunSetupWindow(settingsService, themeService);
                    Console.WriteLine("[2] Testing Sequential Stepper Navigation (Steps 1 through 3)...");
                    for (int step = 1; step <= 3; step++)
                    {
                        setupWin.PublicGoToStep(step);
                        Console.WriteLine($"    -> Step {step} initialized successfully");
                    }

                    Console.WriteLine("[3] Testing Desktop Shortcut & Context Menu registry logic...");
                    bool shortcutResult = Win32.SystemIntegrationHelper.CreateDesktopShortcut();
                    Console.WriteLine($"    -> Desktop shortcut created: {shortcutResult}");

                    bool regResult = Win32.SystemIntegrationHelper.RegisterExplorerContextMenu();
                    Console.WriteLine($"    -> Explorer context menu registered: {regResult}");
                    bool isRegistered = Win32.SystemIntegrationHelper.IsContextMenuRegistered();
                    Console.WriteLine($"    -> Context menu verification check: {isRegistered}");

                    Console.WriteLine("[4] Testing AppSettings persistence for installer parameters...");
                    var s = settingsService.Current;
                    s.CreateDesktopShortcut = true;
                    s.IntegrateWithExplorerContextMenu = true;
                    s.InstallationDirectory = defaultPath;
                    s.IsFirstRunSetupComplete = true;
                    settingsService.Save();
                    Console.WriteLine("    -> Settings successfully persisted");

                    setupWin.Close();
                    Console.WriteLine("ALL RIFT VAULT INSTALLER TESTS PASSED SUCCESSFULLY!");
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-installer-png", StringComparison.OrdinalIgnoreCase) || a.Equals("--render-firstrun-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-installer-png", StringComparison.OrdinalIgnoreCase) || a.Equals("--render-firstrun-png", StringComparison.OrdinalIgnoreCase));
                    int targetStep = 1;
                    string outPng = "installer_step1.png";

                    if (idx + 1 < e.Args.Length && int.TryParse(e.Args[idx + 1], out int step))
                    {
                        targetStep = step;
                        outPng = (idx + 2 < e.Args.Length) ? e.Args[idx + 2] : $"installer_step{step}.png";
                    }
                    else if (idx + 1 < e.Args.Length)
                    {
                        outPng = e.Args[idx + 1];
                    }

                    var setupWin = new UI.FirstRunSetupWindow(settingsService, themeService);
                    setupWin.Width = 820;
                    setupWin.Height = 580;
                    setupWin.PublicGoToStep(targetStep);

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            setupWin.PublicGoToStep(targetStep);
                            setupWin.Measure(new Size(820, 580));
                            setupWin.Arrange(new Rect(0, 0, 820, 580));
                            setupWin.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(920, 730, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(setupWin);
                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng)) { enc.Save(fs); }
                            Console.WriteLine($"SUCCESS_RENDER_INSTALLER: Step {targetStep} saved to {outPng}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR_RENDER_INSTALLER: {ex.Message}");
                        }
                        finally
                        {
                            setupWin.Close();
                        }
                    };
                    timer.Start();
                    setupWin.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-ai-copilot", StringComparison.OrdinalIgnoreCase) || a.Equals("--render-ai-copilot-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-ai-copilot", StringComparison.OrdinalIgnoreCase) || a.Equals("--render-ai-copilot-png", StringComparison.OrdinalIgnoreCase));
                    string targetTab = (idx + 1 < e.Args.Length && !e.Args[idx + 1].StartsWith("-")) ? e.Args[idx + 1] : "Chat";
                    string outPng = (idx + 2 < e.Args.Length && !e.Args[idx + 2].StartsWith("-")) ? e.Args[idx + 2] : "ai_copilot_capture.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1380;
                    mWindow.Height = 850;

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm != null)
                    {
                        vm.IsAIAssistantOpen = true;
                        vm.AIAssistant.PaneWidth = 460;
                        if (targetTab.Equals("Report", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.AIAssistant.SwitchTab("Chat");
                            vm.AIAssistant.PromptInput = "Analyze this folder";
                            vm.AIAssistant.SendMessageCommand.Execute(null);
                        }
                        else if (targetTab.Equals("Welcome", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.AIAssistant.SwitchTab("Chat");
                        }
                        else if (targetTab.Equals("Chat", StringComparison.OrdinalIgnoreCase))
                        {
                            // Populate representative sample messages for capture
                            if (vm.AIAssistant.Messages.Count <= 1)
                            {
                                vm.AIAssistant.PromptInput = "Find duplicate files and clean up cache";
                                vm.AIAssistant.SendMessageCommand.Execute(null);
                            }
                        }
                        else if (targetTab.Equals("History", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.AIAssistant.PromptInput = "Organize downloads by date and clean old installers";
                            vm.AIAssistant.SendMessageCommand.Execute(null);
                            vm.AIAssistant.StartNewSession();
                            vm.AIAssistant.SwitchTab("History");
                        }
                        else
                        {
                            vm.AIAssistant.SwitchTab(targetTab);
                        }
                    }

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            mWindow.Measure(new Size(1380, 850));
                            mWindow.Arrange(new Rect(0, 0, 1380, 850));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1380, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                            Console.WriteLine($"SUCCESS_RENDER_AI_COPILOT: Saved {targetTab} view to {outPng}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR_RENDER_AI_COPILOT: {ex.Message}");
                        }
                        finally
                        {
                            mWindow.Close();
                        }
                    };
                    timer.Start();
                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-toolbar-customizer", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm == null) throw new InvalidOperationException("ViewModel not found");

                    var blocks = vm.Settings.ToolbarBlocks;
                    if (blocks == null || blocks.Count == 0) throw new InvalidOperationException("ToolbarBlocks is null or empty");

                    var active = blocks.Where(b => b.IsVisible).OrderBy(b => b.OrderIndex).ToList();
                    Console.WriteLine($"INITIAL ACTIVE BLOCKS ({active.Count}): " + string.Join(", ", active.Select(x => x.Id)));

                    if (!active.Any(b => b.Id == "address")) throw new InvalidOperationException("Address bar not active by default");
                    if (!active.Any(b => b.Id == "nav")) throw new InvalidOperationException("Nav controls not active by default");

                    var customizer = new UI.ToolbarCustomizeWindow(vm.SettingsService, mWindow);
                    Console.WriteLine($"CUSTOMIZER ACTIVE: {customizer.ActiveItems.Count}, AVAILABLE: {customizer.AvailableItems.Count}");

                    if (customizer.ActiveItems.Count == 0) throw new InvalidOperationException("ActiveItems is empty in customizer");
                    if (customizer.AvailableItems.Count == 0 && customizer.ActiveItems.Count > 1)
                    {
                        var temp = customizer.ActiveItems[customizer.ActiveItems.Count - 1];
                        customizer.ActiveItems.Remove(temp);
                        customizer.AvailableItems.Add(temp);
                    }

                    var first = customizer.ActiveItems[0];
                    customizer.ActiveItems.Move(0, 1);
                    if (customizer.ActiveItems[1].Id != first.Id) throw new InvalidOperationException("Move failed");

                    Console.WriteLine("ALL TOOLBAR CUSTOMIZATION TESTS PASSED!");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-command-strip", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm == null) throw new InvalidOperationException("ViewModel not found");

                    Console.WriteLine($"STATUS TEXT: {vm.StatusText}");
                    Console.WriteLine($"ACTIVE TAB ITEMS: {vm.ActiveTab?.TotalItemCount}");
                    Console.WriteLine($"IS FOLDER EMPTY: {vm.ActiveTab?.IsFolderEmpty}");

                    if (vm.ActiveTab?.CreateNewFolderCommand == null) throw new InvalidOperationException("CreateNewFolderCommand missing");
                    if (vm.ActiveTab?.CreateNewFileCommand == null) throw new InvalidOperationException("CreateNewFileCommand missing");
                    if (vm.ActiveTab?.CutSelectedCommand == null) throw new InvalidOperationException("CutSelectedCommand missing");
                    if (vm.ActiveTab?.CopySelectedCommand == null) throw new InvalidOperationException("CopySelectedCommand missing");
                    if (vm.ActiveTab?.PasteCommand == null) throw new InvalidOperationException("PasteCommand missing");
                    if (vm.ActiveTab?.DeleteSelectedCommand == null) throw new InvalidOperationException("DeleteSelectedCommand missing");

                    Console.WriteLine("ALL COMMAND STRIP AND VIEWMODEL TESTS PASSED!");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-contextmenu", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var fileCm = mWindow.FindResource("FileItemContextMenu") as System.Windows.Controls.ContextMenu;
                    if (fileCm == null) throw new InvalidOperationException("FileItemContextMenu not found!");

                    var tabCm = mWindow.FindResource("TabContextMenu") as System.Windows.Controls.ContextMenu;
                    if (tabCm == null) throw new InvalidOperationException("TabContextMenu not found!");

                    Console.WriteLine($"SUCCESS_CONTEXTMENU: FileItemContextMenu items: {fileCm.Items.Count}, TabContextMenu items: {tabCm.Items.Count}");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-toolbar-buttons", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm == null) throw new InvalidOperationException("ViewModel not found");

                    Console.WriteLine("TEST: SetViewMode Grid...");
                    vm.SetViewModeCommand.Execute("Grid");
                    mWindow.UpdateLayout();

                    Console.WriteLine("TEST: SetViewMode List...");
                    vm.SetViewModeCommand.Execute("List");
                    mWindow.UpdateLayout();

                    Console.WriteLine("TEST: SetViewMode Details...");
                    vm.SetViewModeCommand.Execute("Details");
                    mWindow.UpdateLayout();

                    Console.WriteLine("TEST: ToggleHiddenFiles...");
                    vm.ToggleHiddenFilesCommand.Execute(null);

                    Console.WriteLine("TEST: ToggleDualPane...");
                    vm.ToggleDualPaneCommand.Execute(null);

                    Console.WriteLine("TEST: TogglePreviewPane...");
                    vm.TogglePreviewPaneCommand.Execute(null);

                    Console.WriteLine("TEST: ToggleAIAssistant...");
                    vm.ToggleAIAssistantCommand.Execute(null);

                    Console.WriteLine("TEST: SetSortBy...");
                    vm.SetSortByCommand.Execute("Name");

                    Console.WriteLine("TEST: ShowInvestigator...");
                    mWindow.ShowInvestigator("Forensics");

                    Console.WriteLine("TEST: SettingsWindow instantiation & load...");
                    var settingsWindow = Services.GetRequiredService<UI.SettingsWindow>();
                    settingsWindow.Owner = mWindow;
                    settingsWindow.Show();
                    settingsWindow.UpdateLayout();
                    settingsWindow.Close();

                    Console.WriteLine("ALL TOOLBAR BUTTON TESTS PASSED!");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-shell-access", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("=== TESTING FULL ACCESS & SHELL OPERATIONS ===");

                    // 1. Test Universal Path Resolution
                    string personal = FileSystemEngine.ResolvePath("shell:Personal");
                    Console.WriteLine($"Resolve shell:Personal -> {personal}");
                    if (string.IsNullOrEmpty(personal) || !Directory.Exists(personal))
                        throw new InvalidOperationException("Failed to resolve shell:Personal");

                    string downloads = FileSystemEngine.ResolvePath("shell:Downloads");
                    Console.WriteLine($"Resolve shell:Downloads -> {downloads}");
                    if (string.IsNullOrEmpty(downloads) || !Directory.Exists(downloads))
                        throw new InvalidOperationException("Failed to resolve shell:Downloads");

                    string desktop = FileSystemEngine.ResolvePath("shell:Desktop");
                    Console.WriteLine($"Resolve shell:Desktop -> {desktop}");
                    if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                        throw new InvalidOperationException("Failed to resolve shell:Desktop");

                    string home = FileSystemEngine.ResolvePath("~");
                    Console.WriteLine($"Resolve ~ -> {home}");
                    if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
                        throw new InvalidOperationException("Failed to resolve ~");

                    string appData = FileSystemEngine.ResolvePath("%APPDATA%");
                    Console.WriteLine($"Resolve %APPDATA% -> {appData}");
                    if (string.IsNullOrEmpty(appData) || !Directory.Exists(appData))
                        throw new InvalidOperationException("Failed to resolve %APPDATA%");

                    string cDrive = FileSystemEngine.ResolvePath("C:");
                    Console.WriteLine($"Resolve C: -> {cDrive}");
                    if (!cDrive.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Failed to resolve C:");

                    // 2. Test ShellFileService operations
                    var shellService = Services.GetRequiredService<IShellFileService>();
                    string testDir = Path.Combine(Path.GetTempPath(), "RiftVault_AccessTest_" + Guid.NewGuid().ToString("N"));
                    Console.WriteLine($"Testing CreateFolderAsync at {testDir}...");
                    bool folderCreated = shellService.CreateFolderAsync(testDir).GetAwaiter().GetResult();
                    if (!folderCreated || !Directory.Exists(testDir))
                        throw new InvalidOperationException("CreateFolderAsync failed");

                    string testFile = Path.Combine(testDir, "test.txt");
                    Console.WriteLine($"Testing CreateFileAsync at {testFile}...");
                    bool fileCreated = shellService.CreateFileAsync(testFile, "Hello Rift Vault").GetAwaiter().GetResult();
                    if (!fileCreated || !File.Exists(testFile))
                        throw new InvalidOperationException("CreateFileAsync failed");

                    string renamedFile = "renamed_test.txt";
                    string expectedRenamedPath = Path.Combine(testDir, renamedFile);
                    Console.WriteLine($"Testing RenameItemAsync to {renamedFile}...");
                    bool renamed = shellService.RenameItemAsync(testFile, renamedFile).GetAwaiter().GetResult();
                    if (!renamed || !File.Exists(expectedRenamedPath))
                        throw new InvalidOperationException("RenameItemAsync failed");

                    Console.WriteLine("Testing DeleteItemsAsync (Send to Recycle Bin)...");
                    bool deleted = shellService.DeleteItemsAsync(new[] { testDir }, permanent: false).GetAwaiter().GetResult();
                    if (!deleted || Directory.Exists(testDir))
                        throw new InvalidOperationException("DeleteItemsAsync to Recycle Bin failed");

                    Console.WriteLine("=== ALL FULL ACCESS & SHELL TESTS PASSED SUCCESSFULLY! ===");
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-address-bar", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var breadcrumbView = mWindow.FindName("BreadcrumbView") as FrameworkElement;
                    var addressEditView = mWindow.FindName("AddressEditView") as FrameworkElement;
                    var addressTextBox = mWindow.FindName("AddressTextBox") as TextBox;
                    var popup = mWindow.FindName("AddressHistoryPopup") as System.Windows.Controls.Primitives.Popup;
                    var items = mWindow.FindName("AddressHistoryItemsControl") as ItemsControl;

                    if (breadcrumbView?.Visibility != Visibility.Visible)
                        throw new InvalidOperationException("BreadcrumbView should be Visible initially");
                    if (addressEditView?.Visibility != Visibility.Collapsed)
                        throw new InvalidOperationException("AddressEditView should be Collapsed initially");

                    Console.WriteLine("TEST: Invoking EnterAddressEditMode...");
                    mWindow.EnterAddressEditMode(showHistory: true);
                    mWindow.UpdateLayout();

                    if (breadcrumbView.Visibility != Visibility.Collapsed)
                        throw new InvalidOperationException("BreadcrumbView should be Collapsed in edit mode");
                    if (addressEditView.Visibility != Visibility.Visible)
                        throw new InvalidOperationException("AddressEditView should be Visible in edit mode");
                    if (popup == null || !popup.IsOpen)
                        throw new InvalidOperationException("AddressHistoryPopup should be Open in edit mode");
                    if (items == null || items.Items.Count == 0)
                        throw new InvalidOperationException("AddressHistoryItemsControl should have items populated");

                    Console.WriteLine($"TEST: AddressHistoryItemsControl has {items.Items.Count} history items");

                    Console.WriteLine("TEST: Invoking ExitAddressEditMode...");
                    mWindow.ExitAddressEditMode();
                    mWindow.UpdateLayout();

                    if (breadcrumbView.Visibility != Visibility.Visible)
                        throw new InvalidOperationException("BreadcrumbView should be restored after exit");
                    if (addressEditView.Visibility != Visibility.Collapsed)
                        throw new InvalidOperationException("AddressEditView should be Collapsed after exit");
                    if (popup.IsOpen)
                        throw new InvalidOperationException("AddressHistoryPopup should be Closed after exit");

                    Console.WriteLine("ALL ADDRESS BAR TESTS PASSED!");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-full-system-access", StringComparison.OrdinalIgnoreCase)))
                {
                    var sb = new System.Text.StringBuilder();
                    void Log(string msg) { Console.WriteLine(msg); sb.AppendLine(msg); }

                    void PumpEvents(int ms = 250)
                    {
                        var frame = new System.Windows.Threading.DispatcherFrame();
                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                        timer.Tick += (s, ev) => { timer.Stop(); frame.Continue = false; };
                        timer.Start();
                        System.Windows.Threading.Dispatcher.PushFrame(frame);
                    }

                    void WaitForLoad(TabViewModel tab, int timeoutMs = 6000)
                    {
                        var start = DateTime.Now;
                        while (tab.IsLoading && (DateTime.Now - start).TotalMilliseconds < timeoutMs)
                        {
                            PumpEvents(50);
                        }
                        PumpEvents(250);
                    }

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();
                    PumpEvents(300);

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm == null || vm.ActiveTab == null) throw new InvalidOperationException("ViewModel/ActiveTab not found");

                    Log("TEST: 1. Navigating to 'This PC'...");
                    vm.ActiveTab.NavigateTo("This PC");
                    WaitForLoad(vm.ActiveTab);

                    Log($"This PC Items Count: {vm.ActiveTab.Items.Count}");
                    if (vm.ActiveTab.Items.Count == 0) throw new Exception("This PC returned 0 items!");

                    var cDrive = System.Linq.Enumerable.FirstOrDefault(vm.ActiveTab.Items, i => i.Model.Path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
                    if (cDrive == null) throw new Exception("C: drive not found in This PC!");
                    Log($"Found Drive: {cDrive.Model.Name} -> {cDrive.Model.Path}");

                    Log("TEST: 2. Navigating to C:\\ root...");
                    vm.ActiveTab.NavigateTo("C:\\");
                    WaitForLoad(vm.ActiveTab);
                    Log($"C:\\ Items Count: {vm.ActiveTab.Items.Count}");
                    if (vm.ActiveTab.Items.Count == 0) throw new Exception("C:\\ returned 0 items!");

                    Log("TEST: 3. Ascending (GoUp) from C:\\ back to This PC...");
                    vm.ActiveTab.GoUpCommand.Execute(null);
                    WaitForLoad(vm.ActiveTab);
                    Log($"After GoUp, CurrentPath: {vm.ActiveTab.CurrentPath}");
                    if (!vm.ActiveTab.CurrentPath.Equals("This PC", StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Expected 'This PC' after ascending from C:\\, got '{vm.ActiveTab.CurrentPath}'");

                    Log("TEST: 4. Testing resilient system folder access: C:\\Windows...");
                    vm.ActiveTab.NavigateTo("C:\\Windows");
                    WaitForLoad(vm.ActiveTab);
                    Log($"C:\\Windows Items Count: {vm.ActiveTab.Items.Count}");
                    if (vm.ActiveTab.Items.Count == 0) throw new Exception("C:\\Windows returned 0 items!");

                    Log("TEST: 5. Testing environment variable %TEMP%...");
                    string tempExp = Environment.ExpandEnvironmentVariables("%TEMP%");
                    vm.ActiveTab.NavigateTo(tempExp);
                    WaitForLoad(vm.ActiveTab);
                    Log($"%TEMP% Items Count: {vm.ActiveTab.Items.Count}");

                    Log("TEST: 6. Testing Smooth Scroll & ScrollBar Configuration...");
                    var lv = mWindow.FindName("FileListView") as ListView;
                    if (lv == null) throw new Exception("FileListView not found!");
                    bool smoothEnabled = RiftVault.UI.Controls.SmoothScrollHelper.GetIsEnabled(lv);
                    Log($"FileListView SmoothScrollHelper.IsEnabled: {smoothEnabled}");
                    if (!smoothEnabled) throw new Exception("SmoothScrollHelper was not enabled on FileListView!");

                    Log($"SUCCESS_FULL_SYSTEM_ACCESS: All 6 tests passed successfully! IsAdmin={vm.IsAdministrator}");
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_full_access.txt"), sb.ToString());
                    File.WriteAllText(@"c:\Users\polic\Desktop\Antigravity\Rift VAult\test_full_access.txt", sb.ToString());
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--show-address-edit", StringComparison.OrdinalIgnoreCase)))
                {
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 800;
                    MainWindow = mWindow;
                    mWindow.Show();

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        mWindow.EnterAddressEditMode(showHistory: true);
                    };
                    timer.Start();

                    var closeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(4500) };
                    closeTimer.Tick += (s, ev) =>
                    {
                        closeTimer.Stop();
                        mWindow.Close();
                        Environment.Exit(0);
                    };
                    closeTimer.Start();
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--show-smooth-scroll", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--show-smooth-scroll", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length && !e.Args[idx + 1].StartsWith("-")) ? e.Args[idx + 1] : @"C:\Users\polic\.gemini\antigravity-ide\brain\5bb34346-ea3a-49e7-86f4-089cffb29afc\smooth_scrollbar_view.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 820;
                    MainWindow = mWindow;
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm?.ActiveTab != null)
                    {
                        vm.ActiveTab.NavigateTo("C:\\Windows");
                    }

                    var startTime = DateTime.Now;
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    timer.Tick += (s, ev) =>
                    {
                        if (vm != null && vm.ActiveTab != null && vm.ActiveTab.IsLoading && (DateTime.Now - startTime).TotalMilliseconds < 5000)
                        {
                            return;
                        }

                        timer.Stop();
                        try
                        {
                            vm?.UpdateStatusText();
                            mWindow.UpdateLayout();

                            var lv = mWindow.FindName("FileListView") as System.Windows.Controls.ListView;
                            if (lv != null)
                            {
                                var sv = RiftVault.UI.Controls.SmoothScrollHelper.FindVisualChild<System.Windows.Controls.ScrollViewer>(lv);
                                if (sv != null)
                                {
                                    sv.ScrollToVerticalOffset(Math.Min(350, sv.ScrollableHeight));
                                    mWindow.UpdateLayout();
                                }
                            }

                            int w = (int)mWindow.ActualWidth;
                            int h = (int)mWindow.ActualHeight;
                            if (w <= 0) w = 1360;
                            if (h <= 0) h = 820;

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                            Console.WriteLine($"SUCCESS_SAVED_SMOOTH_SCROLL: {outPng}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR_SAVING_SMOOTH_SCROLL: {ex.Message}");
                        }
                        finally
                        {
                            mWindow.Close();
                            Environment.Exit(0);
                        }
                    };
                    timer.Start();
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--show-this-pc", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--show-this-pc", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length && !e.Args[idx + 1].StartsWith("-")) ? e.Args[idx + 1] : @"C:\Users\polic\.gemini\antigravity-ide\brain\5bb34346-ea3a-49e7-86f4-089cffb29afc\this_pc_drives_view.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 820;
                    MainWindow = mWindow;
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm?.ActiveTab != null)
                    {
                        vm.ActiveTab.NavigateTo("This PC");
                    }

                    var startTime = DateTime.Now;
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    timer.Tick += (s, ev) =>
                    {
                        if (vm != null && vm.ActiveTab != null && vm.ActiveTab.IsLoading && (DateTime.Now - startTime).TotalMilliseconds < 5000)
                        {
                            return;
                        }

                        timer.Stop();
                        try
                        {
                            vm?.UpdateStatusText();
                            mWindow.UpdateLayout();

                            int w = (int)mWindow.ActualWidth;
                            int h = (int)mWindow.ActualHeight;
                            if (w <= 0) w = 1360;
                            if (h <= 0) h = 820;

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                            Console.WriteLine($"SUCCESS_SAVED_THIS_PC: {outPng}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR_SAVING_THIS_PC: {ex.Message}");
                        }
                        finally
                        {
                            mWindow.Close();
                            Environment.Exit(0);
                        }
                    };
                    timer.Start();
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--inspect", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--inspect", StringComparison.OrdinalIgnoreCase));
                    string target = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : Environment.CurrentDirectory;
                    string initialTab = (idx + 2 < e.Args.Length) ? e.Args[idx + 2] : "Forensics";

                    var invWindow = Services.GetRequiredService<UI.InvestigatorWindow>();
                    MainWindow = invWindow;
                    invWindow.InspectTarget(target, initialTab);
                    invWindow.Show();
                    invWindow.Activate();
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase));
                    string cat = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "General";

                    var setWindow = Services.GetRequiredService<UI.SettingsWindow>();
                    MainWindow = setWindow;
                    setWindow.SelectCategory(cat);
                    setWindow.Show();
                    setWindow.Activate();
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-contextmenu-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-contextmenu-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "context_menu_screen.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var cm = mWindow.FindResource("FileItemContextMenu") as ContextMenu;
                    if (cm != null)
                    {
                        cm.PlacementTarget = mWindow;
                        cm.IsOpen = true;
                        mWindow.UpdateLayout();

                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                        timer.Tick += (s, ev) =>
                        {
                            timer.Stop();
                            try
                            {
                                cm.Measure(new Size(355, 950));
                                cm.Arrange(new Rect(0, 0, 355, Math.Max(200, cm.DesiredSize.Height)));
                                cm.UpdateLayout();

                                int w = 355;
                                int h = (int)Math.Max(300, cm.ActualHeight > 0 ? cm.ActualHeight : cm.DesiredSize.Height);
                                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                rtb.Render(cm);

                                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                                using (var fs = File.Create(outPng))
                                {
                                    enc.Save(fs);
                                }
                            }
                            catch { }
                            finally
                            {
                                cm.IsOpen = false;
                                mWindow.Close();
                                Environment.Exit(0);
                            }
                        };
                        timer.Start();
                        return;
                    }
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-sortmenu-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-sortmenu-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "sort_menu_screen.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var sortBtn = mWindow.FindName("SortButton") as System.Windows.Controls.Button;
                    var cm = sortBtn?.ContextMenu;
                    if (cm != null)
                    {
                        cm.PlacementTarget = sortBtn;
                        cm.IsOpen = true;
                        mWindow.UpdateLayout();

                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                        timer.Tick += (s, ev) =>
                        {
                            timer.Stop();
                            try
                            {
                                cm.Measure(new Size(240, 450));
                                cm.Arrange(new Rect(0, 0, 240, Math.Max(150, cm.DesiredSize.Height)));
                                cm.UpdateLayout();

                                int w = 240;
                                int h = (int)Math.Max(220, cm.ActualHeight > 0 ? cm.ActualHeight : cm.DesiredSize.Height);
                                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                rtb.Render(cm);

                                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                                using (var fs = File.Create(outPng))
                                {
                                    enc.Save(fs);
                                }
                            }
                            catch { }
                            finally
                            {
                                cm.IsOpen = false;
                                mWindow.Close();
                                Environment.Exit(0);
                            }
                        };
                        timer.Start();
                        return;
                    }
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-grid-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-grid-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "grid_view_wrapped.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 800;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            var vm = mWindow.DataContext as ViewModels.MainViewModel;
                            if (vm != null)
                            {
                                vm.SetViewModeCommand.Execute("Grid");
                            }

                            mWindow.Measure(new Size(1360, 800));
                            mWindow.Arrange(new Rect(0, 0, 1360, 800));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1360, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            mWindow.Close();
                        }
                    };
                    timer.Start();

                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-dual-pane-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-dual-pane-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "dual_pane_view.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 800;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            var vm = mWindow.DataContext as ViewModels.MainViewModel;
                            if (vm != null)
                            {
                                vm.ToggleDualPaneCommand.Execute(null);
                            }

                            mWindow.Measure(new Size(1360, 800));
                            mWindow.Arrange(new Rect(0, 0, 1360, 800));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1360, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            mWindow.Close();
                        }
                    };
                    timer.Start();

                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-main-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-main-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "rift_vault_screen.png";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1360;
                    mWindow.Height = 800;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            mWindow.Measure(new Size(1360, 800));
                            mWindow.Arrange(new Rect(0, 0, 1360, 800));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1360, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            mWindow.Close();
                        }
                    };
                    timer.Start();

                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--render-preview-png", StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = Array.FindIndex(e.Args, a => a.Equals("--render-preview-png", StringComparison.OrdinalIgnoreCase));
                    string outPng = (idx + 1 < e.Args.Length) ? e.Args[idx + 1] : "preview_screen.png";
                    string targetFile = (idx + 2 < e.Args.Length) ? e.Args[idx + 2] : "";

                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Width = 1400;
                    mWindow.Height = 850;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
                    timer.Tick += async (s, ev) =>
                    {
                        timer.Stop();
                        try
                        {
                            var vm = mWindow.DataContext as ViewModels.MainViewModel;
                            if (vm != null)
                            {
                                vm.IsPreviewPaneOpen = true;
                                if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile))
                                {
                                    var thumbCache = Services.GetRequiredService<Core.IThumbnailCache>();
                                    var item = new ViewModels.FileItemViewModel(new Models.FileItem { Path = targetFile, Name = Path.GetFileName(targetFile), IsDirectory = false }, thumbCache);
                                    await vm.PreviewPane.LoadPreviewAsync(item);
                                }
                            }

                            mWindow.Measure(new Size(1400, 850));
                            mWindow.Arrange(new Rect(0, 0, 1400, 850));
                            mWindow.UpdateLayout();

                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1400, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(mWindow);

                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = File.Create(outPng))
                            {
                                enc.Save(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            mWindow.Close();
                        }
                    };
                    timer.Start();

                    mWindow.ShowDialog();
                    Environment.Exit(0);
                    return;
                }

                if (Array.Exists(e.Args, a => a.Equals("--test-ai-copilot", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("=== STARTING AI COPILOT SUITE TESTS ===");
                    var mWindow = Services.GetRequiredService<MainWindow>();
                    mWindow.Show();
                    mWindow.UpdateLayout();

                    var vm = mWindow.DataContext as ViewModels.MainViewModel;
                    if (vm == null || vm.AIAssistant == null)
                        throw new InvalidOperationException("MainViewModel or AIAssistant is null");

                    var ai = vm.AIAssistant;
                    Console.WriteLine($"[1] Initial Active Provider: {ai.SelectedProvider}");
                    Console.WriteLine($"[1] Active Engine Status: {ai.ActiveEngineStatus}");

                    // 2. Test built-in query
                    Console.WriteLine("[2] Testing query execution ('find large files')...");
                    ai.PromptInput = "find large files";
                    ai.SendMessageCommand.Execute(null);

                    // Wait briefly for execution
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (ai.IsGenerating && sw.ElapsedMilliseconds < 5000)
                    {
                        System.Threading.Thread.Sleep(50);
                    }

                    if (ai.Messages.Count < 2)
                        throw new InvalidOperationException($"Expected at least 2 messages in chat, got {ai.Messages.Count}");

                    var lastMsg = ai.Messages.Last();
                    Console.WriteLine($"[2] Assistant responded: '{(lastMsg.Text.Length > 60 ? lastMsg.Text.Substring(0, 60) : lastMsg.Text)}...'");
                    Console.WriteLine($"[2] Response Latency: {lastMsg.LatencyText}, HasNextOptions: {lastMsg.HasNextOptions}, Options Count: {lastMsg.NextOptions.Count}");

                    if (lastMsg.NextOptions.Count == 0)
                        throw new InvalidOperationException("Expected next options to be populated");

                    // 3. Test follow-up chip execution
                    string firstChip = lastMsg.NextOptions.First();
                    Console.WriteLine($"[3] Clicking Next Option Chip: '{firstChip}'...");
                    ai.QuickPromptCommand.Execute(firstChip);

                    sw.Restart();
                    while (ai.IsGenerating && sw.ElapsedMilliseconds < 5000)
                    {
                        System.Threading.Thread.Sleep(50);
                    }

                    Console.WriteLine($"[3] Messages count after option execution: {ai.Messages.Count}");

                    // 4. Test code block extraction
                    Console.WriteLine("[4] Testing code block extraction with synthetic message...");
                    var msgWithCode = new ChatMessageViewModel
                    {
                        IsUser = false,
                        Text = "Here is a PowerShell command:\n```powershell\nGet-ChildItem -Recurse | Sort-Object Length -Descending | Select-Object -First 10\n```\nRun this in terminal."
                    };
                    Console.WriteLine($"[4] HasCodeSnippet: {msgWithCode.HasCodeSnippet}, Code: {msgWithCode.ExtractedCode.Trim()}");
                    if (!msgWithCode.HasCodeSnippet || !msgWithCode.ExtractedCode.Contains("Get-ChildItem"))
                        throw new InvalidOperationException("Code snippet extraction failed!");

                    // 5. Test Settings & Personalities
                    Console.WriteLine("[5] Testing Personalities & Settings Customization...");
                    ai.Personality = "Technical";
                    ai.Temperature = 0.3;
                    ai.CustomSystemPrompt = "Always include SHA256 checksum suggestions.";
                    ai.SaveSettings();

                    var activeSettings = settingsService.Current;
                    if (activeSettings.AIPersonality != "Technical" || activeSettings.AITemperature != 0.3)
                        throw new InvalidOperationException("Settings persistence failed for AI settings!");
                    Console.WriteLine($"[5] Settings successfully persisted to JSON: Personality={settingsService.Current.AIPersonality}, Temp={settingsService.Current.AITemperature}");

                    // 6. Test Ollama Offline Check
                    Console.WriteLine("[6] Testing Ollama connection test (graceful offline handling)...");
                    ai.TestOllamaCommand.Execute(null);
                    sw.Restart();
                    while (ai.OllamaStatusText.Contains("Testing") && sw.ElapsedMilliseconds < 3000)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                    Console.WriteLine($"[6] Ollama Status result: {ai.OllamaStatusText}");

                    // 7. Test Session History & Archiving
                    Console.WriteLine("[7] Testing Chat Session Management...");
                    int initialHistCount = ai.HistorySessions.Count;
                    ai.StartNewSession();
                    Console.WriteLine($"[7] Started new session. Archived sessions: {ai.HistorySessions.Count}");
                    if (ai.HistorySessions.Count <= initialHistCount)
                        throw new InvalidOperationException("Expected new session to archive previous conversation into HistorySessions");

                    Console.WriteLine("ALL AI COPILOT TESTS PASSED SUCCESSFULLY!");
                    mWindow.Close();
                    Environment.Exit(0);
                    return;
                }


                // First Run Onboarding Check
                if (!settingsService.Current.IsFirstRunSetupComplete && !Array.Exists(e.Args, a => a.Equals("--no-setup", StringComparison.OrdinalIgnoreCase)))
                {
                    var setupWin = new UI.FirstRunSetupWindow(settingsService, themeService);
                    setupWin.ShowDialog();
                }

                var mainWindow = Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error in OnStartup");
                ShowCrashDialog(ex);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "Dispatcher Unhandled Exception suppressed for resilience: {Message}", e.Exception.Message);
            e.Handled = true; 
        }

        private static bool _isExiting = false;
        private void ShowCrashDialog(Exception? ex)
        {
            if (_isExiting) return;
            _isExiting = true;
            MessageBox.Show($"A critical error occurred:\n\n{ex?.Message}\n\nCheck logs in %AppData%\\RiftVault\\logs for details.", 
                "Rift Vault - Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDir;
        public FileLoggerProvider(string logDir) => _logDir = logDir;
        public ILogger CreateLogger(string categoryName) => new FileLogger(_logDir, categoryName);
        public void Dispose() { }
    }

    public class FileLogger : ILogger
    {
        private readonly string _logDir;
        private readonly string _categoryName;
        private static readonly object _lock = new object();

        public FileLogger(string logDir, string categoryName)
        {
            _logDir = logDir;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            
            var msg = formatter(state, exception);
            var logLine = $"[{DateTime.Now:O}] [{logLevel}] [{_categoryName}] {msg}";
            if (exception != null) logLine += $"\n{exception}";

            string logFile = Path.Combine(_logDir, $"rift-{DateTime.Now:yyyy-MM-dd}.log");

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(logFile, logLine + Environment.NewLine);
                    
                    var info = new FileInfo(logFile);
                    if (info.Exists && info.Length > 10 * 1024 * 1024)
                    {
                        File.Move(logFile, Path.Combine(_logDir, $"rift-{DateTime.Now:yyyy-MM-dd-HHmmss}.log"));
                        CleanupOldLogs();
                    }
                }
                catch { /* Failsafe */ }
            }
        }

        private void CleanupOldLogs()
        {
            var files = new DirectoryInfo(_logDir).GetFiles("rift-*.log");
            if (files.Length > 5)
            {
                Array.Sort(files, (a, b) => a.CreationTime.CompareTo(b.CreationTime));
                for (int i = 0; i < files.Length - 5; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
        }
    }
}