# DOOM — Installation & Deployment Guide

This guide details how to install, upgrade, configure, and uninstall **DOOM (Windows Edition)** on Windows 11.

---

## 1. System Requirements

| Specification | Requirement |
|---|---|
| **Operating System** | Windows 11 (Version 22H2 or later recommended, Build 22621+) |
| **Architecture** | x64 (64-bit Intel / AMD) |
| **Runtime** | **None required** — DOOM is published as a self-contained binary with the .NET 10 LTS runtime bundled |
| **Privileges** | Standard user account (Per-user installation into `%LOCALAPPDATA%`; **no Administrator / UAC prompt required**) |
| **Disk Space** | ~120 MB free disk space |
| **Memory** | ~35 MB RAM during operation |
| **Network** | HTTPS (Port 443) connection to `https://generativelanguage.googleapis.com` |

---

## 2. Standard Installation (Recommended)

1. Download the latest installer executable: **`DOOM-Setup-v1.0.0.exe`** from the official repository releases.
2. Double-click **`DOOM-Setup-v1.0.0.exe`** to launch setup.
3. The installer will automatically:
   - Extract the application files into:
     ```
     %LOCALAPPDATA%\Programs\DOOM\
     ```
   - Create Start Menu shortcut:
     ```
     %APPDATA%\Microsoft\Windows\Start Menu\Programs\DOOM.lnk
     ```
   - Create Desktop shortcut:
     ```
     %USERPROFILE%\Desktop\DOOM.lnk
     ```
   - Register DOOM in Windows **Installed Apps (Add or Remove Programs)** with publisher info, version metadata, and uninstaller.
   - Launch DOOM in the background notification area (System Tray).

---

## 3. Unattended & Enterprise Silent Deployment

DOOM supports silent, headless installation via command line parameters, suitable for script automation, Intune, or SCCM:

### Silent Installation
```powershell
DOOM-Setup-v1.0.0.exe /SILENT
```
*Installs silently using default locations and launches DOOM in the system tray.*

### Silent Installation without Auto-Launch
```powershell
DOOM-Setup-v1.0.0.exe /SILENT /NORESTART
```
*Installs DOOM without launching the process upon completion.*

### Custom Installation Directory
```powershell
DOOM-Setup-v1.0.0.exe /SILENT /DIR="D:\CustomApps\DOOM"
```

---

## 4. In-Place Upgrades & Data Preservation Guarantee

DOOM strictly separates program binaries from user data:

| Asset | Location | Upgrade Behavior |
|---|---|---|
| **Program Binaries** | `%LOCALAPPDATA%\Programs\DOOM\` | Replaced atomically with new version binaries. |
| **User Settings** | `%APPDATA%\DOOM\settings.json` | **Preserved 100% untouched.** |
| **DPAPI Credentials** | `%APPDATA%\DOOM\credentials.dat` | **Preserved 100% untouched.** |

### Upgrade Procedure
1. Download the newer version installer (e.g. `DOOM-Setup-v1.1.0.exe`).
2. Run the installer (interactively or via `/SILENT`).
3. The installer automatically detects the existing installation, signals running instances of `DOOM.exe` to close gracefully, updates the binary files, updates the Windows Registry version key, and restarts DOOM.
4. Your API keys, model selections, and custom privacy settings are instantly active without needing re-entry.

---

## 5. Uninstallation & Data Removal

DOOM can be uninstalled through standard Windows settings or directly via command line:

### Method A: Windows Settings (GUI)
1. Open Windows **Settings** (`Win + I`) $\rightarrow$ **Apps** $\rightarrow$ **Installed apps**.
2. Locate **DOOM — Privacy-First Desktop AI Overlay**.
3. Click the three dots `...` and select **Uninstall**.
4. Follow the prompt to complete removal.

### Method B: Direct Command Line Uninstallation
Run the uninstaller located in the installation folder:
```powershell
"%LOCALAPPDATA%\Programs\DOOM\Uninstall-DOOM.exe" /UNINSTALL
```

### Method C: Silent Uninstallation
```powershell
"%LOCALAPPDATA%\Programs\DOOM\Uninstall-DOOM.exe" /UNINSTALL /SILENT
```

### Preserving vs. Purging User Configuration
- **Default Uninstall:** By default, uninstalling DOOM preserves your `%APPDATA%\DOOM` directory (containing `settings.json` and encrypted `credentials.dat`), so reinstalling later does not require re-configuring your API key.
- **Complete Purge:** To remove all binary files AND erase all local configuration and encrypted credentials from disk, pass the `/PURGE-DATA` switch:
  ```powershell
  "%LOCALAPPDATA%\Programs\DOOM\Uninstall-DOOM.exe" /UNINSTALL /SILENT /PURGE-DATA
  ```

---

## 6. Portable (.ZIP) Deployment

For users who prefer not to use an installer:
1. Download **`DOOM-v1.0.0-win-x64.zip`**.
2. Extract the archive into any preferred directory (e.g. `C:\Tools\DOOM`).
3. Double-click `DOOM.exe` to run.
4. To create a startup shortcut manually, press `Win + R`, enter `shell:startup`, and place a shortcut to `DOOM.exe` inside the opened folder.

---

## 7. Building & Packaging from Source

To compile and produce the installer from source code:

### 1. Prerequisites
- Windows 11 (x64)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.403` or later)
- Git for Windows

### 2. Build Pipeline Commands
```powershell
# 1. Restore and verify solution
dotnet restore DOOM.slnx

# 2. Run unit and integration tests (108 tests)
dotnet test -c Release DOOM.slnx

# 3. Publish self-contained single-file application
dotnet publish src/DOOM.App/DOOM.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist/app

# 4. Package portable archive
Compress-Archive -Path "dist/app/*" -DestinationPath "dist/DOOM-v1.0.0-win-x64.zip" -Force

# 5. Compile and publish setup installer
dotnet publish src/DOOM.Installer/DOOM.Installer.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist/installer
```

The compiled installer will be generated at:
```
dist/installer/DOOM-Setup-v1.0.0.exe
```

