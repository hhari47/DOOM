# Changelog

All notable changes to **DOOM (Windows Edition)** are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-10-31

### Added
- **Initial Windows 11 Release**: High-performance native Windows port of DOOM built on .NET 10 LTS and C# 14.
- **Floating Non-Activating Overlay**: Frameless dark-first WPF window configured with `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST` that renders streaming AI responses without stealing keyboard focus from active applications.
- **Windows UI Automation Engine**: Text selection capture via `AutomationElement` and `TextPattern`, supporting Notepad, VS Code, browsers, and Windows editors with a 1000ms watchdog timeout and fallback handling.
- **Dual-Layer Password Shield**: Automatic exclusion of password fields and credential boxes using both UI Automation `IsPassword` flags and Win32 `ES_PASSWORD` (0x0020) style masks.
- **Gemini Interactions API Client**: Real SSE streaming integration with Google Gemini (`gemini-3.5-flash`), supporting custom model IDs, system prompts, low-thinking parameters, and network error handling.
- **Incremental Markdown Viewer**: Custom high-throughput FlowDocument streaming renderer capable of processing over 33,000 lines/sec without UI stutter or full-document recalculation.
- **Privacy Mode (`WDA_EXCLUDEFROMCAPTURE`)**: Native Win32 `SetWindowDisplayAffinity` integration masking the overlay to pure black in screen recorders and screen-sharing applications (Teams, Discord, OBS, Snipping Tool).
- **Single-Instance IPC Activation**: Win32 Mutex combined with Named Pipe IPC (`ACTIVATE` signal) enabling subsequent launches from Desktop or Start Menu to bring the overlay to the foreground without focus stealing or modal dialogs.
- **Windows DPAPI Credential Store**: Gemini API keys encrypted at rest using the Windows Data Protection API with `CurrentUser` isolation and cryptographic entropy.
- **5-Card MVVM Settings Window**: Interactive settings interface with setup checklist, Gemini API key configuration (password masking/revealing, save, replace, remove, and live connection test), shortcut status, privacy review, and model configuration.
- **Global Shortcut Coordinator**: Win32 `RegisterHotKey` implementation for `Ctrl + Shift + Space` (capture & analyze), `Ctrl + Shift + H` (toggle overlay visibility), and `Escape` (cancel active generation).
- **Standalone Installer & Maintenance Tool**: Self-contained Windows setup executable with per-user installation into `%LOCALAPPDATA%\Programs\DOOM`, Start Menu and Desktop shortcuts, Windows Add/Remove Programs registration, and settings-preserving upgrade logic.
- **Comprehensive Automated Test Suite**: 128 automated unit, integration, and security tests covering single-instance IPC, DPAPI lifecycle, UI Automation acquisition, Gemini streaming, and display affinity verification.

