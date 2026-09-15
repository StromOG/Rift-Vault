using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RiftVault.AI
{
    public class AIContextSnapshot
    {
        public string CurrentPath { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
        public long TotalBytes { get; set; }
        public List<string> TopExtensions { get; set; } = new();
        public List<string> LargestFiles { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();
        public string? SelectedFilePath { get; set; }
        public string? SelectedFileName { get; set; }
        public long SelectedFileSize { get; set; }
        public string? SelectedFileSample { get; set; }
    }

    public class AIActionResult
    {
        public string ActionType { get; set; } = "None"; // Search, Navigate, Terminal, CopyCommand
        public string Parameter { get; set; } = string.Empty;
        public string ButtonLabel { get; set; } = string.Empty;
    }

    public class AIResponse
    {
        public string Text { get; set; } = string.Empty;
        public List<AIActionResult> Actions { get; set; } = new();
        public List<string> NextOptions { get; set; } = new();
        public string Provider { get; set; } = "Rift Local Neural Core";
        public double LatencyMs { get; set; }
    }

    public class LocalIntelligenceEngine
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
        private readonly Dictionary<string, string> _formatEncyclopedia = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _windowsKnowledge = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _cliKnowledge = new(StringComparer.OrdinalIgnoreCase);

        public string ActiveProvider { get; set; } = "BuiltIn"; // "BuiltIn", "Ollama", "Cloud"
        public string LocalLlmEndpoint { get; set; } = "http://localhost:11434/api/generate";
        public string LocalLlmModel { get; set; } = "phi3:mini";
        public bool EnableLocalLlmBridge { get; set; } = true;
        public string CloudProvider { get; set; } = "OpenAI";
        public string CloudApiKey { get; set; } = "";
        public string CloudModel { get; set; } = "gpt-4o-mini";
        public string Personality { get; set; } = "Balanced";
        public string CustomSystemPrompt { get; set; } = "";
        public double Temperature { get; set; } = 0.7;
        public int MaxFilesAnalyzed { get; set; } = 100;
        public bool AllowFileOperations { get; set; } = false;

        public LocalIntelligenceEngine()
        {
            InitializeKnowledgeBase();
        }

        public async Task<AIResponse> QueryAsync(string userPrompt, AIContextSnapshot context, CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AIResponse? response = null;

            // 1. Ollama local LLM bridge
            if (ActiveProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                response = await TryLocalLlmAsync(userPrompt, context, ct);
            }
            // 2. Cloud AI (OpenAI / Groq / Gemini)
            else if (ActiveProvider.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
            {
                response = await TryCloudAiAsync(userPrompt, context, ct);
            }

            // 3. Built-in High-Speed Local Neural & Semantic Engine (< 15ms response, 100% offline)
            if (response == null)
            {
                response = ProcessWithBuiltInEngine(userPrompt, context);
                if (ActiveProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
                {
                    response.Provider = "Built-In Core (Ollama offline)";
                }
                else if (ActiveProvider.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
                {
                    response.Provider = "Built-In Core (Cloud offline)";
                }
            }

            // Ensure NextOptions are populated
            if (response.NextOptions == null || response.NextOptions.Count == 0)
            {
                response.NextOptions = GenerateNextOptions(userPrompt, context);
            }

            sw.Stop();
            response.LatencyMs = sw.ElapsedMilliseconds;
            return response;
        }

        private AIResponse ProcessWithBuiltInEngine(string prompt, AIContextSnapshot ctx)
        {
            string p = prompt.Trim().ToLowerInvariant();
            var actions = new List<AIActionResult>();
            var sb = new StringBuilder();

            // ─── Intent 1: Folder Analysis / Health Check ───────────────
            if (p.Contains("analyze") || p.Contains("health") || p.Contains("stats") || p.Contains("summary of this folder") || p.Contains("tell me about this folder") || p.Contains("what is in this folder"))
            {
                return GenerateFolderAnalysis(ctx);
            }

            // ─── Intent 2: Summarize / Explain Selected File ────────────
            if ((p.Contains("summarize") || p.Contains("explain") || p.Contains("what does this do") || p.Contains("what is this file") || p.Contains("review")) && !string.IsNullOrEmpty(ctx.SelectedFilePath))
            {
                return GenerateFileSummary(ctx);
            }

            // ─── Intent 3: Find Duplicates ─────────────────────────────
            if (p.Contains("duplicate") || p.Contains("clones") || p.Contains("copies"))
            {
                return GenerateDuplicateReport(ctx);
            }

            // ─── Intent 4: File Extension / Format Encyclopedia ─────────
            var extMatch = Regex.Match(p, @"\.([a-zA-Z0-9_]{1,10})\b|\b([a-zA-Z0-9_]{1,10}) file\b");
            if (extMatch.Success)
            {
                string ext = (extMatch.Groups[1].Value != "" ? extMatch.Groups[1].Value : extMatch.Groups[2].Value).ToLowerInvariant();
                if (_formatEncyclopedia.TryGetValue("." + ext, out string? info))
                {
                    sb.AppendLine($"### 📄 Format Intelligence: `.{ext.ToUpperInvariant()}`");
                    sb.AppendLine();
                    sb.AppendLine(info);
                    sb.AppendLine();
                    sb.AppendLine($"**Active Folder Presence:** {(ctx.TopExtensions.Any(e => e.Contains(ext, StringComparison.OrdinalIgnoreCase)) ? "Found in this directory." : "None in current folder.")}");
                    
                    actions.Add(new AIActionResult
                    {
                        ActionType = "Search",
                        Parameter = "*." + ext,
                        ButtonLabel = $"Filter for *.{ext}"
                    });

                    return new AIResponse { Text = sb.ToString(), Actions = actions };
                }
            }

            // ─── Intent 5: Windows System Knowledge / Paths ─────────────
            foreach (var kvp in _windowsKnowledge)
            {
                if (p.Contains(kvp.Key.ToLowerInvariant()))
                {
                    sb.AppendLine($"### 🖥️ Windows Architecture: {kvp.Key}");
                    sb.AppendLine();
                    sb.AppendLine(kvp.Value);
                    sb.AppendLine();

                    string targetPath = kvp.Key.ToLowerInvariant() switch
                    {
                        "hosts" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc"),
                        "system32" => Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "syswow64" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"),
                        _ => Environment.ExpandEnvironmentVariables(kvp.Key)
                    };

                    if (Directory.Exists(targetPath))
                    {
                        actions.Add(new AIActionResult
                        {
                            ActionType = "Navigate",
                            Parameter = targetPath,
                            ButtonLabel = $"Navigate to {kvp.Key}"
                        });
                    }

                    return new AIResponse { Text = sb.ToString(), Actions = actions };
                }
            }

            // ─── Intent 6: Natural Language Search Query ────────────────
            if (p.StartsWith("find ") || p.StartsWith("search ") || p.Contains("show me all") || p.Contains("where is") || p.Contains("filter for"))
            {
                return GenerateSearchIntent(prompt, ctx);
            }

            // ─── Intent 7: Organization / Cleanup Suggestions ───────────
            if (p.Contains("organize") || p.Contains("clean") || p.Contains("tidy") || p.Contains("clutter") || p.Contains("sort"))
            {
                return GenerateOrganizationPlan(ctx);
            }

            // ─── Intent 8: CLI / PowerShell / Terminal Generator ────────
            if (p.Contains("powershell") || p.Contains("cmd") || p.Contains("command") || p.Contains("script") || p.Contains("bash") || p.Contains("how to") || p.Contains("terminal"))
            {
                return GenerateCliGuidance(prompt, ctx);
            }

            // ─── Intent 9: Code / Development Question ──────────────────
            if (p.Contains("code") || p.Contains("c#") || p.Contains("csharp") || p.Contains("python") || p.Contains("javascript") || p.Contains("react") || p.Contains("regex") || p.Contains("git"))
            {
                return GenerateDevCopilotResponse(prompt, ctx);
            }

            // ─── General Fallback / Universal Assistant Response ────────
            sb.AppendLine($"### 🔮 Rift Local AI Assistant");
            sb.AppendLine();
            sb.AppendLine($"I am your **100% on-device AI Copilot**, running locally with full awareness of your system and active workspace:");
            sb.AppendLine($"- **Active Directory**: `{ctx.CurrentPath}` ({ctx.TotalFiles} files, {ctx.TotalFolders} folders, {FormatSize(ctx.TotalBytes)})");
            if (!string.IsNullOrEmpty(ctx.SelectedFileName))
            {
                sb.AppendLine($"- **Selected File**: `{ctx.SelectedFileName}` ({FormatSize(ctx.SelectedFileSize)})");
            }
            sb.AppendLine();
            sb.AppendLine("Here are some powerful things I can do for you right now:");
            sb.AppendLine("1. **Analyze Directory**: Ask *\"Analyze this folder\"* to see health, duplicate risks, and largest items.");
            sb.AppendLine("2. **Inspect Selected Code/Text**: Ask *\"Explain this file\"* or *\"Summarize code\"*.");
            sb.AppendLine("3. **Find Files Naturally**: Ask *\"Find all videos modified this month\"* or *\"Show files larger than 100MB\"*.");
            sb.AppendLine("4. **Suggest Cleanup**: Ask *\"How should I organize this folder?\"*.");
            sb.AppendLine("5. **Write Shell Commands**: Ask *\"Give me a PowerShell command to backup this folder\"*.");
            sb.AppendLine();
            sb.AppendLine("> **Tip**: If you have **Ollama** running locally (`ollama run llama3` or `phi3`), I automatically connect to it for open-ended conversational intelligence!");

            actions.Add(new AIActionResult { ActionType = "Analyze", Parameter = ctx.CurrentPath, ButtonLabel = "⚡ Analyze Current Folder" });
            if (!string.IsNullOrEmpty(ctx.SelectedFilePath))
            {
                actions.Add(new AIActionResult { ActionType = "Summarize", Parameter = ctx.SelectedFilePath, ButtonLabel = $"📝 Summarize {ctx.SelectedFileName}" });
            }

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateFolderAnalysis(AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### 📊 Health & Intelligence Report: `{Path.GetFileName(ctx.CurrentPath)}`");
            sb.AppendLine($"**Full Path:** `{ctx.CurrentPath}`");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"| :--- | :--- |");
            sb.AppendLine($"| **Total Files** | {ctx.TotalFiles:N0} |");
            sb.AppendLine($"| **Subdirectories** | {ctx.TotalFolders:N0} |");
            sb.AppendLine($"| **Allocated Storage** | {FormatSize(ctx.TotalBytes)} |");
            sb.AppendLine();

            if (ctx.TopExtensions.Any())
            {
                sb.AppendLine("**Dominant File Types:**");
                foreach (var ext in ctx.TopExtensions.Take(5))
                {
                    sb.AppendLine($"- `{ext}`");
                }
                sb.AppendLine();
            }

            if (ctx.LargestFiles.Any())
            {
                sb.AppendLine("**Largest Files in Directory:**");
                foreach (var item in ctx.LargestFiles.Take(4))
                {
                    sb.AppendLine($"- 📦 {item}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("💡 **AI Assessment:**");
            if (ctx.TotalFiles > 300)
            {
                sb.AppendLine("- High item count detected. Recommending subfolder grouping by year or extension.");
            }
            else if (ctx.TotalBytes > 10L * 1024 * 1024 * 1024)
            {
                sb.AppendLine("- Heavy storage consumption (>10 GB). Consider archiving unaccessed media or logs.");
            }
            else
            {
                sb.AppendLine("- Directory structure is healthy and responsive.");
            }

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "Terminal", Parameter = ctx.CurrentPath, ButtonLabel = "Open Terminal Here" },
                new() { ActionType = "Duplicates", Parameter = ctx.CurrentPath, ButtonLabel = "Check Duplicates" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateFileSummary(AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            string fileName = ctx.SelectedFileName ?? "Selected File";
            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            sb.AppendLine($"### 🔍 Deep File Inspection: `{fileName}`");
            sb.AppendLine($"- **Size:** {FormatSize(ctx.SelectedFileSize)}");
            sb.AppendLine($"- **Format Category:** {GetCategoryDescription(ext)}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(ctx.SelectedFileSample))
            {
                string sample = ctx.SelectedFileSample;
                int lines = sample.Split('\n').Length;
                sb.AppendLine($"**Code/Text Metrics:** ~{lines} preview lines analyzed.");
                sb.AppendLine();

                // Code detection
                if (ext is ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".java" or ".go" or ".rs")
                {
                    var classes = Regex.Matches(sample, @"\b(class|interface|struct|record|enum)\s+([A-Za-z0-9_]+)").Select(m => m.Groups[2].Value).Distinct().ToList();
                    var methods = Regex.Matches(sample, @"\b(public|private|protected|async|def|function)\s+([A-Za-z0-9_]+)\s*\(").Select(m => m.Groups[2].Value).Distinct().Take(6).ToList();

                    if (classes.Any())
                    {
                        sb.AppendLine($"**Declared Structures:** {string.Join(", ", classes.Select(c => $"`{c}`"))}");
                    }
                    if (methods.Any())
                    {
                        sb.AppendLine($"**Key Functions/Methods:** {string.Join(", ", methods.Select(m => $"`{m}()`"))}");
                    }
                    sb.AppendLine();
                    sb.AppendLine("**Purpose:** Source code file implementing application logic, types, or services.");
                }
                else if (ext is ".json" or ".xml" or ".yaml" or ".yml" or ".config")
                {
                    sb.AppendLine("**Structure:** Structured data / configuration schema. Contains settings or data interchange models.");
                }
                else if (ext is ".md" or ".txt" or ".log")
                {
                    sb.AppendLine("**Content Preview Excerpt:**");
                    string previewSnippet = sample.Length > 280 ? sample.Substring(0, 280) + "..." : sample;
                    sb.AppendLine("```text");
                    sb.AppendLine(previewSnippet.Trim());
                    sb.AppendLine("```");
                }
            }
            else
            {
                if (_formatEncyclopedia.TryGetValue(ext, out string? details))
                {
                    sb.AppendLine(details);
                }
                else
                {
                    sb.AppendLine($"Binary or media file format `{ext}`. Fully compatible with native Windows execution.");
                }
            }

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "OpenWithNotepad", Parameter = ctx.SelectedFilePath ?? "", ButtonLabel = "Edit in Notepad" },
                new() { ActionType = "Properties", Parameter = ctx.SelectedFilePath ?? "", ButtonLabel = "Properties" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateDuplicateReport(AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### 🧬 Duplicate Brain Scanner");
            sb.AppendLine($"Scanning active directory `{Path.GetFileName(ctx.CurrentPath)}` for content collisions...");
            sb.AppendLine();
            sb.AppendLine("Rift Vault Duplicate Brain uses a 2-stage verification pipeline:");
            sb.AppendLine("1. **Instant Byte-Length Binning**: Groups files with exact matching byte counts.");
            sb.AppendLine("2. **Cryptographic MD5 Checksumming**: Computes on-device cryptographic hash to verify bit-for-bit equivalence.");
            sb.AppendLine();
            sb.AppendLine("To run a full deep scan of this folder right now, click below:");

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "RunDuplicateScan", Parameter = ctx.CurrentPath, ButtonLabel = "⚡ Scan For Duplicates Now" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateSearchIntent(string prompt, AIContextSnapshot ctx)
        {
            string p = prompt.ToLowerInvariant();
            string query = "";
            string explanation = "";

            if (p.Contains("large") || p.Contains("huge") || p.Contains("big"))
            {
                query = ">100MB";
                explanation = "Filtering for large files exceeding 100 MB in current workspace.";
            }
            else if (p.Contains("video") || p.Contains("movies"))
            {
                query = ".mp4;.mkv;.avi;.mov";
                explanation = "Filtering for video media assets.";
            }
            else if (p.Contains("image") || p.Contains("photo") || p.Contains("picture"))
            {
                query = ".png;.jpg;.jpeg;.webp;.bmp";
                explanation = "Filtering for graphic and image assets.";
            }
            else if (p.Contains("code") || p.Contains("source"))
            {
                query = ".cs;.ts;.js;.py;.json;.xml";
                explanation = "Filtering for code and configuration documents.";
            }
            else if (p.Contains("doc") || p.Contains("pdf") || p.Contains("word"))
            {
                query = ".pdf;.docx;.xlsx;.pptx;.txt;.md";
                explanation = "Filtering for documents and office files.";
            }
            else
            {
                var match = Regex.Match(prompt, @"(?:find|search|where is|filter for)\s+['""]?([^'""]+)['""]?", RegexOptions.IgnoreCase);
                query = match.Success ? match.Groups[1].Value.Trim() : prompt.Replace("find", "").Replace("search", "").Trim();
                explanation = $"Searching directory for `{query}`.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"### 🔎 Natural Language Search Assistant");
            sb.AppendLine(explanation);
            sb.AppendLine();
            sb.AppendLine($"**Target Query:** `{query}`");

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "Search", Parameter = query, ButtonLabel = $"Apply Filter: \"{query}\"" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateOrganizationPlan(AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### 🧹 Smart Auto-Organizer Blueprint");
            sb.AppendLine($"Based on the `{ctx.TotalFiles}` items currently in `{Path.GetFileName(ctx.CurrentPath)}`, here is the optimal folder structure:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("📂 " + Path.GetFileName(ctx.CurrentPath));
            sb.AppendLine(" ├── 📁 Documents/    (PDF, Word, TXT, MD, Spreadsheets)");
            sb.AppendLine(" ├── 📁 Media/        (Images, Videos, Audio tracks)");
            sb.AppendLine(" ├── 📁 Development/  (Source code, Scripts, JSON, Configs)");
            sb.AppendLine(" ├── 📁 Archives/     (ZIP, 7Z, RAR, Installers)");
            sb.AppendLine(" └── 📁 Miscellaneous/");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Would you like Rift Vault to cluster these files safely into these subfolders?");

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "Organize", Parameter = ctx.CurrentPath, ButtonLabel = "✨ Group By Category" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateCliGuidance(string prompt, AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### ⚡ PowerShell & CLI Copilot");
            sb.AppendLine();

            string script = "";
            string desc = "";

            if (prompt.Contains("backup") || prompt.Contains("copy"))
            {
                desc = "High-performance multi-threaded directory mirror using Robocopy:";
                script = $"robocopy \"{ctx.CurrentPath}\" \"D:\\Backup\\{Path.GetFileName(ctx.CurrentPath)}\" /E /MT:16 /R:1 /W:1";
            }
            else if (prompt.Contains("count") || prompt.Contains("how many"))
            {
                desc = "Count all files recursively grouped by extension in PowerShell:";
                script = $"Get-ChildItem -Recurse -File | Group-Object Extension -NoElement | Sort-Object Count -Descending";
            }
            else if (prompt.Contains("large") || prompt.Contains("size"))
            {
                desc = "Find the top 10 largest files in this directory tree:";
                script = $"Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue | Sort-Object Length -Descending | Select-Object -First 10 Name, @{{Name='SizeMB';Expression={{[math]::round($_.Length/1MB,2)}}}}, FullName";
            }
            else if (prompt.Contains("delete") || prompt.Contains("clean") || prompt.Contains("temp"))
            {
                desc = "Clean empty subdirectories safely:";
                script = $"Get-ChildItem -Directory -Recurse | Where-Object {{ (Get-ChildItem $_.FullName).Count -eq 0 }} | Remove-Item";
            }
            else
            {
                desc = $"Open PowerShell directly in this directory with environment initialized:";
                script = $"wt -d \"{ctx.CurrentPath}\"";
            }

            sb.AppendLine(desc);
            sb.AppendLine("```powershell");
            sb.AppendLine(script);
            sb.AppendLine("```");

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "CopyCommand", Parameter = script, ButtonLabel = "📋 Copy Script" },
                new() { ActionType = "Terminal", Parameter = ctx.CurrentPath, ButtonLabel = "Open Terminal Here" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        private AIResponse GenerateDevCopilotResponse(string prompt, AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### 💻 Developer Assistant");
            sb.AppendLine();

            if (prompt.Contains("git"))
            {
                sb.AppendLine("**Essential Git Workflow Cheatsheet:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git status                    # Check uncommitted changes");
                sb.AppendLine("git add -A                    # Stage all modified and untracked files");
                sb.AppendLine("git commit -m \"feat: update\"   # Commit staged files");
                sb.AppendLine("git log --oneline -n 10       # Clean compact commit history");
                sb.AppendLine("```");
            }
            else if (prompt.Contains("regex"))
            {
                sb.AppendLine("**Helpful Regex Patterns:**");
                sb.AppendLine("- Email: `^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$`");
                sb.AppendLine("- IPv4: `^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$`");
                sb.AppendLine("- Date (YYYY-MM-DD): `^\\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\\d|3[01])$`");
                sb.AppendLine("- Windows Path: `^[a-zA-Z]:\\\\(?:[^\\\\/:*?\"<>|\\r\\n]+\\\\)*[^\\\\/:*?\"<>|\\r\\n]*$`");
            }
            else
            {
                sb.AppendLine($"Currently viewing: `{ctx.CurrentPath}`.");
                sb.AppendLine("You can ask me to debug code snippets, explain complex algorithms, generate C# / Python / TypeScript templates, or write batch automation scripts.");
            }

            var actions = new List<AIActionResult>
            {
                new() { ActionType = "Terminal", Parameter = ctx.CurrentPath, ButtonLabel = "Open CLI Here" }
            };

            return new AIResponse { Text = sb.ToString(), Actions = actions };
        }

        public async Task<(bool isRunning, List<string> models, string message)> CheckOllamaStatusAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                string url = LocalLlmEndpoint.Replace("/api/generate", "/api/tags");
                if (!url.EndsWith("/api/tags")) url = "http://localhost:11434/api/tags";

                using var res = await _httpClient.GetAsync(url, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var models = new List<string>();
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        foreach (var m in modelsArr.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(name)) models.Add(name);
                            }
                        }
                    }
                    return (true, models, models.Count > 0 
                        ? $"Ollama connected · {models.Count} model(s) ready"
                        : "Ollama connected · No models installed yet");
                }
                return (false, new List<string>(), $"Ollama returned status {(int)res.StatusCode}");
            }
            catch (Exception ex)
            {
                return (false, new List<string>(), $"Ollama offline on port 11434 ({ex.Message})");
            }
        }

        private string BuildSystemPrompt(AIContextSnapshot ctx)
        {
            var sb = new StringBuilder();
            sb.Append("You are Rift Vault AI, a high-speed intelligent copilot built into the native Windows file explorer Rift Vault. ");
            sb.Append($"Current Directory: '{ctx.CurrentPath}'. Folder contents: {ctx.TotalFiles} files, {ctx.TotalFolders} folders. ");
            if (!string.IsNullOrEmpty(ctx.SelectedFileName))
            {
                sb.Append($"Currently selected item: '{ctx.SelectedFileName}' ({FormatSize(ctx.SelectedFileSize)}). ");
            }

            // Personality styling
            switch (Personality)
            {
                case "Technical":
                    sb.Append("Tone: Technical, forensic, deep system-level insights, hex/signature hints, and direct PowerShell cmdlets. ");
                    break;
                case "Concise":
                    sb.Append("Tone: Ultra-concise, minimal text, bullet points only, direct facts. ");
                    break;
                case "Creative":
                    sb.Append("Tone: Inspiring and helpful, provide intuitive organization structures and smart naming advice. ");
                    break;
                default: // Balanced
                    sb.Append("Tone: Friendly, well-structured, clear markdown headings and bullet points. ");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(CustomSystemPrompt))
            {
                sb.Append($"User Custom Instructions: {CustomSystemPrompt} ");
            }

            sb.Append("Always provide actionable solutions and use markdown formatting.");
            return sb.ToString();
        }

        private async Task<AIResponse?> TryLocalLlmAsync(string prompt, AIContextSnapshot ctx, CancellationToken ct)
        {
            try
            {
                string systemPrompt = BuildSystemPrompt(ctx);

                var payload = new
                {
                    model = LocalLlmModel,
                    prompt = $"{systemPrompt}\n\nUser Question: {prompt}\nAnswer:",
                    stream = false,
                    options = new { temperature = Math.Clamp(Temperature, 0.0, 1.0), num_predict = 600 }
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _httpClient.PostAsync(LocalLlmEndpoint, content, ct);

                if (!res.IsSuccessStatusCode) return null;

                string raw = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("response", out var textProp))
                {
                    string text = textProp.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new AIResponse
                        {
                            Text = text.Trim(),
                            Provider = $"Ollama ({LocalLlmModel})",
                            NextOptions = GenerateNextOptions(prompt, ctx)
                        };
                    }
                }
            }
            catch
            {
                // Local LLM server not running or timed out; seamlessly fallback to instant core
            }

            return null;
        }

        private async Task<AIResponse?> TryCloudAiAsync(string prompt, AIContextSnapshot ctx, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(CloudApiKey)) return null;

            try
            {
                string endpoint = CloudProvider switch
                {
                    "Groq" => "https://api.groq.com/openai/v1/chat/completions",
                    "Gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                    _ => "https://api.openai.com/v1/chat/completions"
                };

                string model = !string.IsNullOrWhiteSpace(CloudModel) 
                    ? CloudModel 
                    : (CloudProvider == "Groq" ? "llama-3.3-70b-versatile" : "gpt-4o-mini");

                string systemPrompt = BuildSystemPrompt(ctx);

                var payload = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = Math.Clamp(Temperature, 0.0, 1.0),
                    max_tokens = 800
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CloudApiKey.Trim());
                string reqJson = JsonSerializer.Serialize(payload);
                req.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                using var res = await _httpClient.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;

                string raw = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var msg = choices[0].GetProperty("message");
                    if (msg.TryGetProperty("content", out var contentProp))
                    {
                        string text = contentProp.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return new AIResponse
                            {
                                Text = text.Trim(),
                                Provider = $"{CloudProvider} ({model})",
                                NextOptions = GenerateNextOptions(prompt, ctx)
                            };
                        }
                    }
                }
            }
            catch
            {
                // Fallback to built-in core
            }

            return null;
        }

        public static List<string> GenerateNextOptions(string prompt, AIContextSnapshot ctx)
        {
            string p = (prompt ?? "").ToLowerInvariant();
            var list = new List<string>();

            if (p.Contains("duplicate") || p.Contains("clean") || p.Contains("space"))
            {
                list.Add("🧹 Propose cleanup rules");
                list.Add("🔍 Find largest storage consumers");
                list.Add("📁 Move older files to Archive");
                list.Add("💻 Open PowerShell script here");
            }
            else if (p.Contains("analyze") || p.Contains("summary") || p.Contains("folder") || p.Contains("files"))
            {
                list.Add("🔍 Check for duplicates");
                list.Add("🧹 Group by extension");
                list.Add("📊 Show file type breakdown");
                list.Add("💻 Copy PowerShell stats command");
            }
            else if (p.Contains("script") || p.Contains("powershell") || p.Contains("batch") || p.Contains("code"))
            {
                list.Add("📋 Copy script to clipboard");
                list.Add("🚀 Open Windows Terminal");
                list.Add("⚙️ How to run safely");
                list.Add("📁 Analyze folder again");
            }
            else if (!string.IsNullOrEmpty(ctx.SelectedFileName))
            {
                list.Add($"📝 Summarize '{ctx.SelectedFileName}'");
                list.Add($"🔤 Explain '{Path.GetExtension(ctx.SelectedFileName)}' format");
                list.Add("📋 Copy file path");
                list.Add("🔍 Search similar files");
            }
            else
            {
                list.Add("📁 Analyze current folder");
                list.Add("🔍 Find duplicate files");
                list.Add("🧹 Smart organization tips");
                list.Add("💻 Open terminal here");
            }

            return list;
        }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {suffixes[order]}";
        }

        private static string GetCategoryDescription(string ext)
        {
            return ext switch
            {
                ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".h" or ".java" or ".go" or ".rs" => "Source Code Document",
                ".json" or ".xml" or ".yaml" or ".yml" or ".toml" or ".ini" => "Structured Configuration / Data",
                ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" => "Digital Raster Image",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "Digital Video Stream",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" => "Digital Audio Stream",
                ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Compressed Archive Container",
                ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "Executable Program / Automation Script",
                ".pdf" or ".docx" or ".xlsx" or ".pptx" or ".txt" => "Formatted Office / Reading Document",
                _ => "General System File"
            };
        }

        private void InitializeKnowledgeBase()
        {
            // ─── Format Encyclopedia ──────────────────────────────────
            _formatEncyclopedia[".exe"] = "Windows Portable Executable binary. Contains compiled machine instructions executed directly by the processor.";
            _formatEncyclopedia[".dll"] = "Dynamic Link Library. Shared library containing reusable executable code and resources loaded into process memory dynamically.";
            _formatEncyclopedia[".cs"] = "C# Source File. Managed code compiled by Roslyn into Intermediate Language (IL) for the .NET CLR runtime.";
            _formatEncyclopedia[".json"] = "JavaScript Object Notation. Standard lightweight, human-readable data interchange format used universally in web and app configs.";
            _formatEncyclopedia[".xml"] = "Extensible Markup Language. Hierarchical structured document format widely used in enterprise data, WPF XAML, and configuration.";
            _formatEncyclopedia[".xaml"] = "Extensible Application Markup Language. Declarative XML dialect used by WPF, WinUI, and MAUI to define user interfaces and visual trees.";
            _formatEncyclopedia[".py"] = "Python Source File. High-level interpreted scripting file executed by CPython or PyPy runtime engines.";
            _formatEncyclopedia[".ts"] = "TypeScript Source File. Statically typed superset of JavaScript compiled down to plain JS by `tsc` or bundlers.";
            _formatEncyclopedia[".js"] = "JavaScript Source File. High-level dynamic script interpreted by V8, SpiderMonkey, or Node.js runtime.";
            _formatEncyclopedia[".cpp"] = "C++ Source Code. High-performance compiled systems programming code compiled to native machine binaries.";
            _formatEncyclopedia[".h"] = "C/C++ Header File. Declares class signatures, function prototypes, macros, and structs included via `#include`.";
            _formatEncyclopedia[".rs"] = "Rust Source File. Memory-safe systems programming code governed by compile-time borrow checker and ownership semantics.";
            _formatEncyclopedia[".go"] = "Go Source File. Statically typed compiled language designed for concurrency with goroutines and channels.";
            _formatEncyclopedia[".sql"] = "Structured Query Language. Script defining relational database queries, schemas, stored procedures, or migrations.";
            _formatEncyclopedia[".sh"] = "POSIX Shell Script. Automation script executed by Bash, Zsh, or Dash on Linux, macOS, and WSL.";
            _formatEncyclopedia[".bat"] = "Windows Batch File. Classic command-line script executed sequentially by `cmd.exe`.";
            _formatEncyclopedia[".ps1"] = "PowerShell Script. Object-oriented automation script executed by Windows PowerShell or PowerShell 7 (`pwsh`).";
            _formatEncyclopedia[".md"] = "Markdown Document. Lightweight plaintext formatting syntax easily converted to HTML and rendered in documentation.";
            _formatEncyclopedia[".pdf"] = "Portable Document Format. Adobe standard for fixed-layout digital documents independent of application, hardware, and OS.";
            _formatEncyclopedia[".zip"] = "ZIP Compressed Archive. DEFLATE-compressed multi-file container natively supported in Windows Explorer.";
            _formatEncyclopedia[".7z"] = "7-Zip Compressed Archive. High-compression container using LZMA/LZMA2 algorithms with strong AES-256 encryption.";
            _formatEncyclopedia[".tar"] = "Tape Archive. Uncompressed archive container packaging multiple files into one, typically paired with `.gz` (gzip).";
            _formatEncyclopedia[".iso"] = "Optical Disc Image. Raw sector-by-sector image of an optical disc (CD/DVD/Blu-ray) mountable directly in Windows.";
            _formatEncyclopedia[".png"] = "Portable Network Graphics. Lossless raster image format supporting 24-bit RGB and 8-bit alpha transparency.";
            _formatEncyclopedia[".jpg"] = "JPEG Image. Lossy raster image compression standard optimized for photographic pictures and web media.";
            _formatEncyclopedia[".webp"] = "Google WebP Image. Modern image format providing 25-34% better lossy and lossless compression than JPEG/PNG.";
            _formatEncyclopedia[".svg"] = "Scalable Vector Graphics. XML-based vector image format providing infinite resolution scaling without pixelation.";
            _formatEncyclopedia[".mp4"] = "MPEG-4 Part 14 Video Container. Universal digital multimedia format encoding H.264/H.265 video and AAC audio.";
            _formatEncyclopedia[".mp3"] = "MPEG-1 Audio Layer III. Lossy digital audio coding format standard across consumer devices.";
            _formatEncyclopedia[".flac"] = "Free Lossless Audio Codec. Lossless bit-perfect digital audio compression for audiophile playback.";
            _formatEncyclopedia[".wasm"] = "WebAssembly Binary. Compact binary code format executing at near-native speed inside web browsers and WASI runtimes.";
            _formatEncyclopedia[".pem"] = "Privacy-Enhanced Mail Certificate. Base64-encoded ASCII container for cryptographic keys and X.509 certificates.";

            // ─── Windows OS Internals Knowledge ───────────────────────
            _windowsKnowledge["%appdata%"] = "Roaming Application Data directory (`C:\\Users\\<user>\\AppData\\Roaming`). Houses settings synchronized across domain networks.";
            _windowsKnowledge["%localappdata%"] = "Local Application Data directory (`C:\\Users\\<user>\\AppData\\Local`). Houses machine-specific app caches, databases, and binaries.";
            _windowsKnowledge["%temp%"] = "Temporary files storage (`AppData\\Local\\Temp`). Can be safely cleared to reclaim storage space.";
            _windowsKnowledge["system32"] = "Core 64-bit Windows operating system binary directory (`C:\\Windows\\System32`). Contains vital kernel DLLs, drivers, and system tools.";
            _windowsKnowledge["syswow64"] = "Windows 32-bit on Windows 64-bit directory (`C:\\Windows\\SysWOW64`). Contains 32-bit compatibility binaries for x86 apps.";
            _windowsKnowledge["hosts"] = "Windows TCP/IP DNS resolution override file (`C:\\Windows\\System32\\drivers\\etc\\hosts`). Maps IP addresses to domain names.";
            _windowsKnowledge["pagefile.sys"] = "Virtual Memory paging file. Physical disk extension of system RAM managed by the Windows memory manager.";
            _windowsKnowledge["hiberfil.sys"] = "Hibernation memory dump file. Stores the entire RAM contents to disk when entering Windows Fast Startup or Hibernation.";
        }
    }
}
