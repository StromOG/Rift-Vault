using System.Collections.Generic;

namespace RiftVault.Models
{
    public class AppSettings
    {
        // ─── 1. General & Startup ──────────────────────────────────────
        public string StartupBehavior { get; set; } = "Home"; // "Home", "LastSession", "ThisPC", "CustomFolder"
        public string CustomStartupPath { get; set; } = "";
        public bool IsFirstRunSetupComplete { get; set; } = false;
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool IntegrateWithExplorerContextMenu { get; set; } = true;
        public string InstallationDirectory { get; set; } = "";
        public bool IsPortableMode { get; set; } = false;
        public bool RestorePreviousTabsOnLaunch { get; set; } = true;
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool SingleClickOpen { get; set; } = false;
        public bool DoubleClickBlankToGoUp { get; set; } = true;
        public bool MiddleClickOpensInNewTab { get; set; } = true;
        public bool ShowStatusBar { get; set; } = true;
        public bool AutoRefreshFileSystemChanges { get; set; } = true;
        public bool MinimizeToSystemTray { get; set; } = false;
        public bool RememberWindowPositionAndSize { get; set; } = true;
        public string DefaultViewMode { get; set; } = "Details"; // "Details", "Grid", "List"

        // ─── 2. Appearance & Themes ────────────────────────────────────
        public string Theme { get; set; } = "SandstormPeach"; // "SandstormPeach", "ObsidianAurora", "SolarFusion", "CyberCrimson", "MidnightPink", "ArcticMist", "PureBlack"
        public string CustomAccentColor { get; set; } = "#E07A5F";
        public string WindowBackdrop { get; set; } = "Mica"; // "Mica", "Acrylic", "Solid"
        public string WindowCornerRadius { get; set; } = "16"; // "0", "8", "16", "20", "24", "28"
        public bool EnableAnimations { get; set; } = true;
        public string WindowEntranceAnimation { get; set; } = "FluentSpring"; // "FluentSpring", "SmoothScale", "SlideUp", "SoftZoom", "FadeOnly", "Instant"
        public double WindowAnimationDurationMs { get; set; } = 280.0; // 100ms to 800ms
        public double WindowCloseAnimationDurationMs { get; set; } = 180.0; // 80ms to 450ms
        public string AnimationEasingFunction { get; set; } = "Cubic"; // "Cubic", "Quintic", "Back", "Elastic", "Quadratic"
        public double AnimationSpringIntensity { get; set; } = 0.25; // 0.05 to 0.70
        public double UIHoverAnimationDurationMs { get; set; } = 150.0; // 50ms to 350ms
        public double GlassBlurIntensity { get; set; } = 1.0;
        public string AnimationSpeed { get; set; } = "Normal"; // "Instant", "Fast", "Normal", "Relaxed"
        public string UIDensity { get; set; } = "Comfortable"; // "Compact", "Comfortable", "Spacious"
        public string IconDensity { get; set; } = "Normal";
        public double SidebarWidth { get; set; } = 240.0;
        public bool ShowTabPreviewThumbnails { get; set; } = true;
        public bool ShowFullPathInTitleBar { get; set; } = false;
        public bool EnableSmoothScroll { get; set; } = true;

        // Custom Wallpaper & Glass Acrylic Canvas
        public string CustomBackgroundPath { get; set; } = "";
        public double BackgroundOpacity { get; set; } = 0.85; // 0.10 to 1.0 (default 0.85 for vibrant wallpaper)
        public double BackgroundBlur { get; set; } = 0.0; // 0 to 40px
        public double BackgroundDimmer { get; set; } = 0.30; // 0.0 to 0.90 dark/light contrast overlay
        public double FileAreaGlassOpacity { get; set; } = 0.25; // 0.05 to 0.80 file canvas translucency
        public string BackgroundStretch { get; set; } = "UniformToFill"; // "UniformToFill", "Uniform", "Center"

        // ─── 3. Files & Folders ────────────────────────────────────────
        public bool ShowHiddenFiles { get; set; } = false;
        public bool ShowSystemFiles { get; set; } = false;
        public bool ShowProtectedSystemFiles { get; set; } = false;
        public bool ShowFileExtensions { get; set; } = true;
        public string DateFormat { get; set; } = "Relative"; // "Relative", "Standard", "ISO"
        public bool CalculateFolderSizesInBackground { get; set; } = true;
        public bool ShowDriveCapacityPercent { get; set; } = true;
        public bool ShowChecksumInProperties { get; set; } = true;
        public bool GroupDirectoriesFirst { get; set; } = true;
        public bool CaseSensitiveSort { get; set; } = false;

        // Preview Pane & Built-in Mini-Editor
        public bool IsPreviewPaneOpen { get; set; } = false;
        public double PreviewPaneWidth { get; set; } = 350.0;
        public bool AutoPlayVideoPreviews { get; set; } = false;
        public bool WrapCodeText { get; set; } = true;
        public bool ShowCodeLineNumbers { get; set; } = true;
        public string PreferredEditorApp { get; set; } = "BuiltIn"; // "BuiltIn", "Notepad", "VSCode", "NotepadPlusPlus", "Custom"
        public string CustomEditorPath { get; set; } = "";

        // ─── 3b. Terminal & Compression Tools ──────────────────────────
        public string PreferredTerminal { get; set; } = "WindowsTerminal"; // "WindowsTerminal", "PowerShell", "CommandPrompt", "GitBash", "Custom"
        public string CustomTerminalPath { get; set; } = "";
        public string DefaultArchiveFormat { get; set; } = "Zip"; // "Zip", "7z", "TarGz"
        public string CompressionLevel { get; set; } = "Optimal"; // "Fastest", "Optimal", "SmallestSize"

        // ─── 4. Local AI & Neural Engine ───────────────────────────────
        public string AIProvider { get; set; } = "BuiltIn"; // "BuiltIn", "Ollama", "Cloud"
        public string OllamaEndpoint { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "phi3:mini";
        public string CloudProvider { get; set; } = "OpenAI"; // "OpenAI", "Groq", "Gemini"
        public string CloudApiKey { get; set; } = "";
        public string CloudModel { get; set; } = "gpt-4o-mini";
        public string AIPersonality { get; set; } = "Balanced"; // "Balanced", "Technical", "Concise", "Creative"
        public string AICustomSystemPrompt { get; set; } = "";
        public bool AIAllowFileOperations { get; set; } = false;
        public int AIMaxFilesAnalyzed { get; set; } = 100;
        public double AITemperature { get; set; } = 0.7;
        public double AIAssistantPaneWidth { get; set; } = 420.0;
        public bool EnableSmartShelf { get; set; } = true;
        public string SmartShelfSensitivity { get; set; } = "Balanced"; // "Conservative", "Balanced", "Aggressive"
        public bool EnableFileDNA { get; set; } = true;
        public bool EnableDuplicateBrain { get; set; } = true;
        public int DuplicateSimilarityThreshold { get; set; } = 95; // 80 - 100%
        public bool EnableAutoOrganizer { get; set; } = true;
        public bool AutoTagFilesWithAI { get; set; } = true;
        public bool EnableFileAgeGlow { get; set; } = true;
        public int FileAgeThresholdDays { get; set; } = 180;
        public bool ShowAITagsInGrid { get; set; } = true;
        public string AIComputeDevice { get; set; } = "DirectML GPU"; // "Auto", "CPU Only", "DirectML GPU"
        public string AIProcessingPriority { get; set; } = "IdleOnly"; // "IdleOnly", "LowBackground", "Normal"
        public bool AllowNeuralModelAutoUpdates { get; set; } = false;

        // ─── 5. Performance & Caching ──────────────────────────────────
        public int ThumbnailCacheSizeMB { get; set; } = 512;
        public int MaxConcurrentIOThreads { get; set; } = 8;
        public int MaxSearchIndexDepth { get; set; } = 10;
        public bool EnableMemoryTrimmingOnIdle { get; set; } = true;
        public bool PreloadAdjacentFolders { get; set; } = true;
        public bool FastDirectoryEnumeration { get; set; } = true;

        // ─── 6. Privacy & Security (Zero-Cloud) ────────────────────────
        public bool EnforceZeroCloudGuarantee { get; set; } = true;
        public bool ClearSearchHistoryOnExit { get; set; } = false;
        public int SecureShreddingPasses { get; set; } = 3; // 1, 3, 7 (DoD standard)
        public bool AutoLockVaultOnSleep { get; set; } = true;
        public int VaultTimeoutMinutes { get; set; } = 15;

        // ─── 7. Audio & Feedback ───────────────────────────────────────
        public bool EnableUISounds { get; set; } = true;
        public bool PlaySoundOnNavigation { get; set; } = true;
        public bool PlaySoundOnOperationComplete { get; set; } = true;
        public bool PlaySoundOnError { get; set; } = true;
        public double SoundVolume { get; set; } = 50.0;

        // ─── 8. Hotkeys & Shortcuts ────────────────────────────────────
        public Dictionary<string, string> Hotkeys { get; set; } = new()
        {
            { "NewTab", "Ctrl+T" },
            { "CloseTab", "Ctrl+W" },
            { "NextTab", "Ctrl+Tab" },
            { "PreviousTab", "Ctrl+Shift+Tab" },
            { "Search", "Ctrl+F" },
            { "Settings", "Ctrl+," },
            { "ToggleSidebar", "Ctrl+B" },
            { "Refresh", "F5" }
        };

        // ─── 9. Modular Toolbar Customization ──────────────────────────
        public List<ToolbarBlockSetting> ToolbarBlocks { get; set; } = GetDefaultToolbarBlocks();

        public static List<ToolbarBlockSetting> GetDefaultToolbarBlocks()
        {
            return new List<ToolbarBlockSetting>
            {
                // Default Active Blocks (Familiar, spacious address bar matching Windows 11 & Files App)
                new() { Id = "nav", Title = "Navigation Buttons", IconGlyph = "\uE72B", Description = "Back, forward, up, and refresh controls", IsVisible = true, OrderIndex = 0 },
                new() { Id = "address", Title = "Address Bar", IconGlyph = "\uE80F", Description = "Interactive breadcrumbs, direct path editor & history dropdown (flexible width)", IsVisible = true, OrderIndex = 1 },
                new() { Id = "search", Title = "Search Omnibar", IconGlyph = "\uE721", Description = "Quick search box with clear button", IsVisible = true, OrderIndex = 2 },

                // Available Tool Blocks (Ready to be added / customized onto Row 1 right off the box)
                new() { Id = "viewmode", Title = "View Modes", IconGlyph = "\uE762", Description = "Details, Grid, and List layout switcher", IsVisible = false, OrderIndex = 3 },
                new() { Id = "sort", Title = "Sort Options", IconGlyph = "\uE8CB", Description = "Sort files by name, date modified, size, and type", IsVisible = false, OrderIndex = 4 },
                new() { Id = "customize", Title = "Customize Toolbar", IconGlyph = "\uE70F", Description = "Open customizer dialog to reorder, add, or remove toolbar tools", IsVisible = false, OrderIndex = 5 },

                // Available Tool Blocks (Ready to be added / toggled)
                new() { Id = "actionstrip", Title = "Command Action Strip", IconGlyph = "\uE8F4", Description = "Inline buttons for New folder, New file, Cut, Copy, Paste, Rename, Delete", IsVisible = false, OrderIndex = 6 },
                new() { Id = "features", Title = "Quick Features Pill", IconGlyph = "\uE890", Description = "Toggles for Preview pane, Dual-pane, Hidden files, and AI assistant", IsVisible = false, OrderIndex = 7 },
                new() { Id = "appmodes", Title = "Workflow Modes Pill", IconGlyph = "\uE8B7", Description = "Switch between Explorer, Dual Commander, Media Gallery, Dev, and Zen", IsVisible = false, OrderIndex = 8 },
                new() { Id = "inspector", Title = "File Inspector", IconGlyph = "\uE9F9", Description = "Inspect file details, safety, and deep forensics", IsVisible = false, OrderIndex = 9 },
                new() { Id = "terminal", Title = "Terminal Console", IconGlyph = "\uE756", Description = "Launch preferred command terminal in current folder", IsVisible = false, OrderIndex = 10 },
                new() { Id = "selection", Title = "Selection Tools", IconGlyph = "\uE762", Description = "Select all, invert selection, and deselect all", IsVisible = false, OrderIndex = 11 },
                new() { Id = "settings", Title = "Settings Launcher", IconGlyph = "\uE713", Description = "Quick access button for Rift Vault Settings", IsVisible = false, OrderIndex = 12 }
            };
        }
    }

    public class ToolbarBlockSetting
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE700";
        public string Description { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public int OrderIndex { get; set; }
    }
}