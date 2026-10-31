# DOOM — Windows Edition

> **Privacy-First Desktop AI Overlay for Windows 11**  
> *Native Windows 11 desktop assistant powered by the Google Gemini Interactions API.*

[![Target OS](https://img.shields.io/badge/Platform-Windows%2011%20(x64)-0078D4?logo=windows11&logoColor=white)](https://microsoft.com/windows)
[![Runtime](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Language](https://img.shields.io/badge/C%23-14.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![AI Provider](https://img.shields.io/badge/Google-Gemini%20Interactions%20API-4285F4?logo=google&logoColor=white)](https://ai.google.dev)
[![Test Suite](https://img.shields.io/badge/Tests-128%2F128%20Passing-success?logo=checkmarx&logoColor=white)](tests/)

---

## 1. Overview & What DOOM Does

**DOOM (Windows Edition)** is a native, privacy-first desktop utility that brings ambient intelligence to your workflow. By capturing selected text directly through the **Windows UI Automation** accessibility tree and streaming responses from **Google Gemini**, DOOM answers questions, clarifies context, summarizes excerpts, and assists workflows without disrupting your active window or stealing focus.

### Core Principles
- **Zero Data Persistence**: Prompt text and model outputs live exclusively in volatile RAM. They are never written to disk, telemetry, or structured logs.
- **Hardware-Backed Credential Security**: Your Gemini API key is encrypted locally using the **Windows Data Protection API (DPAPI)** with `CurrentUser` isolation and cryptographic entropy.
- **DWM Display Privacy**: Uses native Win32 `SetWindowDisplayAffinity` (`WDA_EXCLUDEFROMCAPTURE`) to mask the response overlay to pure black in screen recorders, meeting tools (Teams, Discord, Zoom), and screenshot utilities.
- **Non-Activating Overlay**: Never steals keyboard focus from the application you are working in.

---

## 2. Key Features

- **Global Activation (`Ctrl + Shift + Space`)**: Instantly captures selected text from any foreground editor (Notepad, VS Code, Visual Studio, Word, Chrome, Edge) and triggers Gemini AI analysis.
- **Floating Non-Activating Overlay**: Frameless dark-first WPF window configured with `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`.
- **Incremental Markdown Renderer**: High-performance token streamer rendering bold, italics, inline code, and lists in real-time (over 33,000 lines/sec throughput) without full-document recalculation stutter.
- **Dual-Layer Password Shield**: UI Automation `IsPassword == true` and native Win32 `ES_PASSWORD` (0x0020) checks guarantee password fields and credential boxes are unconditionally rejected.
- **Privacy Mode (`WDA_EXCLUDEFROMCAPTURE`)**: Native Win32 `SetWindowDisplayAffinity` masks the response window from capture tools.
- **Single-Instance IPC Activation**: Seamless Named Pipe IPC brings the overlay to the foreground when launched again from the Desktop or Start Menu, with zero duplicate processes and no annoying modal alerts.
- **Escape to Cancel (`Escape`)**: Immediate cancellation of active streaming network requests without losing window state.
- **Decoupled Visibility (`Ctrl + Shift + H`)**: Hide or restore the overlay at any time without terminating background AI generation.
- **Interactive Settings Window**: 5-card MVVM layout with Gemini key setup (masked input, show/hide, save, replace, remove, and live connection test), shortcut status, privacy review, and model override.

---

## 3. Screenshots & Visual Interface

> *Note: Place visual assets in `docs/screenshots/` when available.*

| Response Overlay (Floating) | Settings Window (5-Card Layout) |
|---|---|
| ![DOOM Overlay Placeholder](docs/screenshots/overlay.png)<br>*(Floating, topmost, non-activating AI streamer)* | ![DOOM Settings Placeholder](docs/screenshots/settings.png)<br>*(Setup checklist, Gemini API key, privacy mode)* |

---

## 4. Architecture & Technology Stack

DOOM Windows Edition follows **Clean Architecture** and **MVVM** principles across six dedicated projects:

```text
DOOM/
├── src/
│   ├── DOOM.Core/               # Domain models, contracts, options, redaction rules
│   ├── DOOM.Services/           # Gemini Interactions API, SSE parser, atomic settings, DPAPI
│   ├── DOOM.Platform.Windows/   # Win32 P/Invoke, Display Affinity, Hotkeys, Tray, UI Automation
│   ├── DOOM.Presentation/       # MVVM ViewModels, Incremental Markdown, WPF Views & Controls
│   ├── DOOM.App/                # Composition root, DI host, single-instance mutex & IPC, WPF App
│   └── DOOM.Installer/          # Self-contained Windows setup & uninstaller executable
├── tests/
│   └── DOOM.UnitTests/          # 128 automated unit, integration, and security tests
├── scripts/                     # Benchmark, test, and icon generation scripts
└── docs/                        # In-depth architectural and mapping documentation
```

### Technology Highlights
- **Framework**: .NET 10.0 LTS (`net10.0-windows`)
- **Language**: C# 14.0 (Nullable Reference Types, Records, Pattern Matching)
- **UI Engine**: Windows Presentation Foundation (WPF) with dark-first styling tokens
- **Platform Interop**: Win32 User32 / DWM / Kernel32 P/Invoke, COM Windows UI Automation (`UIAutomationClient`)
- **AI Backend**: Google Gemini Interactions API (`gemini-3.5-flash` default)

---

## 5. System Requirements

| Specification | Requirement |
|---|---|
| **Operating System** | Windows 11 (Version 22H2 or later recommended, Build 22621+) |
| **Architecture** | x64 (64-bit Intel / AMD) |
| **Runtime** | **None required** for installed app (self-contained .NET 10 runtime bundled) |
| **SDK (for developers)** | .NET 10.0.403 SDK or later |
| **Privileges** | Standard user account (per-user installation into `%LOCALAPPDATA%`, no UAC required) |
| **Network** | HTTPS connection to `https://generativelanguage.googleapis.com` |

---

## 6. Installation & First-Time Setup

### Step 1: Download & Install
Download **`DOOM-Setup-v1.0.0.exe`** from the [GitHub Releases](https://github.com/hhari47/DOOM/releases) page and run the installer.
* The installer extracts DOOM to `%LOCALAPPDATA%\Programs\DOOM\`.
* Start Menu and Desktop shortcuts are created automatically.
* Settings and DPAPI credentials are kept safe across upgrades.

### Step 2: Configure Gemini API Key
> **Important:** DOOM requires your own Google Gemini API key. Keys are free for standard personal use via [Google AI Studio](https://aistudio.google.com/).

1. Launch DOOM from your Desktop or Start Menu.
2. Open Settings:
   - Click the **⚙** gear icon in the response overlay header, OR
   - Right-click the DOOM shield icon in the system tray and select **Settings...**.
3. In the **Gemini Configuration** card:
   - Paste your API key (starts with `AIzaSy...`, 20–256 characters).
   - Use the **Show** / **Hide** button to inspect your input.
   - Click **Save Key**. The key is encrypted immediately into `%APPDATA%\DOOM\credentials.dat` via Windows DPAPI.
4. Click **Test Connection** to execute an end-to-end verification ping.

### Step 3: Usage
1. Open any Windows application (Notepad, VS Code, Browser, Word).
2. Select text you want to analyze or question.
3. Press **`Ctrl + Shift + Space`**.
4. DOOM will stream the answer into the top-right overlay.

---

## 7. Keyboard Shortcuts

| Shortcut | Action | Description |
|---|---|---|
| **`Ctrl + Shift + Space`** | **Capture & Analyze** | Captures selected text via UI Automation and streams Gemini AI response. |
| **`Ctrl + Shift + H`** | **Toggle Visibility** | Hides or reveals the floating response overlay without interrupting generation. |
| **`Escape`** | **Cancel Generation** | Immediately cancels active HTTP streaming request and transitions to Cancelled state. |
| **System Tray** | **App Menu** | Right-click tray icon to access Settings, toggle Privacy Mode, or Quit DOOM. |

---

## 8. Building & Testing for Developers

### Prerequisites
- Windows 11 (x64)
- [.NET 10.0 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.403` or later)
- Git for Windows

### Build the Solution
```powershell
# Clone the repository
git clone https://github.com/hhari47/DOOM.git
cd DOOM

# Restore dependencies
dotnet restore DOOM.slnx

# Build Release solution
dotnet build DOOM.slnx -c Release
```

### Run Automated Tests
```powershell
# Run the complete test suite (128 tests)
dotnet test DOOM.slnx -c Release
```

### Publish Self-Contained Executable
```powershell
dotnet publish src/DOOM.App/DOOM.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/app
```

---

## 9. Security & Privacy Notes

- **Zero Logging of Prompts**: Your captured selections and AI responses are never recorded in logs or disk caches.
- **Hardware DPAPI Encryption**: Secrets are encrypted at rest with Windows DPAPI under your user account with unique cryptographic entropy.
- **HTTPS Only**: All API traffic goes over TLS 1.3 to Google's official Gemini endpoint. Auto-redirects and cookie sharing are disabled.
- **Security Policy**: For vulnerability reporting, see [SECURITY.md](SECURITY.md).

---

## 10. Known Limitations

1. **DirectX / Vulkan Exclusive Fullscreen**: DOOM overlays standard desktop and borderless windows. In exclusive fullscreen games, the game takes direct control of the display hardware and obscures DWM overlays.
2. **Elevated Administrator Targets**: If an application is running as Administrator (elevated), a standard non-elevated DOOM process cannot query its UI Automation tree due to Windows UIPI (User Interface Privilege Isolation). Run DOOM as Administrator if you need to capture text from elevated processes.
3. **Legacy Screen Recorders**: While modern tools (Teams, Zoom, Discord, OBS) respect DWM `WDA_EXCLUDEFROMCAPTURE`, legacy capture software using direct unmanaged framebuffer hooks may capture desktop buffers directly.

---

## 11. Documentation Links

- **[Installation Guide](INSTALLATION.md)** — Installation, upgrade, silent deployment, and uninstall.
- **[User Guide](USER_GUIDE.md)** — Feature walkthrough, settings cards, and tips.
- **[Privacy Architecture](PRIVACY.md)** — Technical details on DWM affinity, password shielding, and data flow.
- **[Security Policy](SECURITY.md)** — Credential handling and vulnerability reporting.
- **[Contributing Guidelines](CONTRIBUTING.md)** — Setup, architecture, and code quality expectations.
- **[Changelog](CHANGELOG.md)** — Full release history for v1.0.0.
- **[Release Notes](RELEASE_NOTES.md)** — Performance metrics and technical verification summary.

---

## 12. Copyright & Proprietary Notice

Copyright © 2026 DOOM Contributors. All Rights Reserved.

This software, source code, and related documentation are proprietary. No open-source license is granted, either expressly or implicitly. Unauthorized copying, modification, distribution, sublicensing, or publication of this code, via any medium, is strictly prohibited.
