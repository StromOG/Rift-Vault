# Contributing to Rift Vault

Thank you for your interest in contributing to **Rift Vault**! We are committed to building an open, blazing-fast, and beautiful file manager for Windows, and we welcome contributions of all forms—bug reports, feature requests, documentation improvements, performance optimizations, and code pull requests.

---

## 📜 Code of Conduct

By participating in this project, you agree to treat everyone with respect, kindness, and professionalism. Constructive feedback, collaboration, and inclusive discussions are essential to our community.

---

## 🛠️ Development Setup

### Requirements
- **Windows 10** (Version 1903+, Build 18362+) or **Windows 11**
- **.NET 8.0 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Visual Studio 2022** (with *.NET desktop development* workload) or **Visual Studio Code** (with C# Dev Kit)

### Fork and Clone
1. Fork the repository on GitHub: [https://github.com/StromOG/Rift-Vault](https://github.com/StromOG/Rift-Vault)
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Rift-Vault.git
   cd Rift-Vault
   ```
3. Set up the upstream remote:
   ```bash
   git remote add upstream https://github.com/StromOG/Rift-Vault.git
   ```

### Building & Running
```bash
# Restore NuGet dependencies and build
dotnet build

# Launch the application
dotnet run --no-build
```

---

## 🌿 Git Workflow

1. Always branch off the latest `main` branch:
   ```bash
   git checkout main
   git pull upstream main
   git checkout -b feature/your-feature-name
   ```
2. Make your modifications following our code conventions.
3. Verify that the project compiles cleanly with 0 errors and 0 warnings:
   ```bash
   dotnet build -c Release
   ```
4. Commit your changes with a descriptive, concise commit message following conventional commits:
   - `feat: add collapsible date grouping in details view`
   - `fix: prevent vertical scroll offset in address bar`
   - `perf: optimize Win32 shell thumbnail caching`
   - `docs: update developer setup guide`
5. Push to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
6. Open a **Pull Request** targeting `main` on `StromOG/Rift-Vault`.

---

## 📐 Code Style & Architecture Guidelines

- **MVVM Pattern**: ViewModels should use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). Views should be minimal code-behind, focusing on layout and XAML bindings.
- **Async by Default**: All file I/O, thumbnail loading, and AI compute must execute asynchronously off the UI dispatcher thread.
- **Native Win32 Cleanliness**: Place all P/Invoke declarations and Win32 structs inside the `Win32/` directory with proper `DllImport` or `LibraryImport` signatures.
- **Theme Integrity**: Use dynamic resource brushes (`{DynamicResource TextPrimaryBrush}`, `{DynamicResource AccentBrush}`) rather than hardcoded hex colors, ensuring proper behavior across all themes.

---

## 🐛 Reporting Bugs

If you discover a bug, please check the [GitHub Issues](https://github.com/StromOG/Rift-Vault/issues) to ensure it hasn't already been reported. When filing a new issue, please include:
- Windows version and build number.
- Detailed steps to reproduce the behavior.
- Expected behavior vs. actual behavior.
- Any relevant logs or screenshots.

---

## 💡 Proposing Features

We love ideas! Before implementing major architectural changes, please open an issue with the `enhancement` tag to discuss your proposed solution with the maintainers.

Thank you for helping make Rift Vault the best file manager for Windows! 🚀
