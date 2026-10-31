# Contributing to DOOM

Thank you for your interest in contributing to **DOOM**! We welcome bug reports, feature discussions, and pull requests that adhere to our privacy, security, and performance standards.

---

## 1. Development Prerequisites

To develop and build DOOM on Windows, you will need:

- **Operating System**: Windows 11 (Version 22H2 or later recommended, Build 22621+)
- **SDK**: [.NET 10.0 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.403` or later)
- **IDE**: Visual Studio 2026, JetBrains Rider, or Visual Studio Code with the C# Dev Kit
- **Git**: Git for Windows with standard long path support enabled

---

## 2. Getting Started

1. **Clone the repository**:
   ```powershell
   git clone https://github.com/hhari47/DOOM.git
   cd DOOM
   ```

2. **Restore dependencies**:
   ```powershell
   dotnet restore DOOM.slnx
   ```

3. **Build the solution**:
   ```powershell
   dotnet build DOOM.slnx -c Release
   ```

4. **Run the automated test suite**:
   ```powershell
   dotnet test DOOM.slnx -c Release
   ```
   *All tests must pass before opening a pull request.*

---

## 3. Architecture & Code Quality Guidelines

DOOM adheres to **Clean Architecture** and **MVVM** principles across six focused projects:

- **`DOOM.Core`**: Domain models, contract interfaces, configuration abstractions, and security redactors. Has zero dependencies on UI or platform assemblies.
- **`DOOM.Services`**: Gemini SSE streaming client, settings service, and DPAPI credential management.
- **`DOOM.Platform.Windows`**: Native Win32 P/Invoke interop, UI Automation selection reader, global hotkeys, and display affinity.
- **`DOOM.Presentation`**: MVVM ViewModels, dark theme styling tokens, and the high-throughput `IncrementalMarkdownViewer`.
- **`DOOM.App`**: Composition root, single-instance mutex and Named Pipe IPC listener, system tray host, and WPF lifecycle.
- **`DOOM.Installer`**: Standalone self-contained Windows setup and uninstaller utility.

### Coding Standards
- **C# 14 & Nullable Reference Types**: Nullability annotations must be enabled and respected throughout.
- **Zero Warnings**: The solution enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. All builds must compile cleanly without warnings.
- **Privacy First**: Never log, persist, or transmit user text, prompts, or credentials. All logging must use `SensitiveDataRedactor` or structured templates that omit payload strings.
- **Non-Activating Windows**: Never call `Window.Activate()` or allow the response overlay to steal foreground focus from target applications.

---

## 4. Submitting a Pull Request

1. **Create a branch** for your feature or bug fix:
   ```powershell
   git checkout -b feature/my-improvement
   ```
2. **Write tests** covering new functionality or reproducing the bug being fixed.
3. **Verify build and test passes**:
   ```powershell
   dotnet test DOOM.slnx -c Release
   ```
4. **Commit with clear, descriptive messages**:
   ```text
   Fix: Resolve UI Automation text selection truncation on multiline controls
   ```
5. **Open a Pull Request** describing:
   - The motivation for the change.
   - Any manual testing performed on Windows 11.
   - Confirmation that all unit and integration tests passed.

