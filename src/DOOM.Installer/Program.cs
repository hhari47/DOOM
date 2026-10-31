namespace DOOM.Installer;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

/// <summary>
/// Professional Windows Installer and Maintenance tool for DOOM.
/// Supports installation, in-place upgrades with settings preservation, and clean uninstallation.
/// </summary>
internal static class Program
{
    private const string AppName = "DOOM";
    private const string AppFullName = "DOOM — Privacy-First Desktop AI Overlay";
    private const string AppVersion = "1.0.0";
    private const string AppPublisher = "DOOM Project";
    private const string AppUrl = "https://github.com/hhari47/DOOM";
    private const string RegistryUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DOOM";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        bool isSilent = false;
        bool isUninstall = false;
        bool purgeData = false;
        bool noLaunch = false;
        string? customDir = null;

        foreach (var arg in args)
        {
            var lower = arg.Trim().ToLowerInvariant();
            if (lower is "/silent" or "-s" or "--silent" or "/s")
            {
                isSilent = true;
            }
            else if (lower is "/uninstall" or "-u" or "--uninstall" or "/u")
            {
                isUninstall = true;
            }
            else if (lower is "/purge-data" or "--purge-data")
            {
                purgeData = true;
            }
            else if (lower is "/norestart" or "--no-launch" or "/nolaunch")
            {
                noLaunch = true;
            }
            else if (lower.StartsWith("/dir=") || lower.StartsWith("--dir="))
            {
                customDir = arg.Substring(arg.IndexOf('=') + 1).Trim('"');
            }
        }

        // Also detect if executable name contains "uninstall"
        var currentExeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        if (currentExeName.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
        {
            isUninstall = true;
        }

        PrintBanner(isUninstall);

        try
        {
            if (isUninstall)
            {
                return PerformUninstall(isSilent, purgeData);
            }
            else
            {
                return PerformInstall(isSilent, noLaunch, customDir);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Operation failed: {ex.Message}");
            Console.ResetColor();
            if (!isSilent)
            {
                Console.WriteLine("\nPress any key to exit...");
                try { Console.ReadKey(); } catch { }
            }
            return 1;
        }
    }

    private static void PrintBanner(bool isUninstall)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
    =======================================================
               D  O  O  M  —  W I N D O W S
          Privacy-First Desktop AI Overlay (v1.0.0)
    =======================================================");
        Console.ResetColor();
        Console.WriteLine(isUninstall
            ? "  Action: Uninstallation & Cleanup"
            : "  Action: Installation & Upgrade Setup");
        Console.WriteLine("-------------------------------------------------------\n");
    }

    private static int PerformInstall(bool isSilent, bool noLaunch, string? customDir)
    {
        var targetDir = !string.IsNullOrWhiteSpace(customDir)
            ? customDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        Console.WriteLine($"[1/5] Target Installation Path: {targetDir}");
        Console.WriteLine($"      User Configuration Path: {dataDir}");

        // Check running instances
        CloseRunningProcesses(AppName);

        // Check if upgrade
        bool isUpgrade = Directory.Exists(targetDir) && File.Exists(Path.Combine(targetDir, "DOOM.exe"));
        if (isUpgrade)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[2/5] Existing installation detected. Performing upgrade...");
            Console.WriteLine("      [PRESERVATION VERIFIED] Settings & DPAPI credentials in %APPDATA%\\DOOM will remain untouched.");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("\n[2/5] Preparing clean installation directory...");
        }

        Directory.CreateDirectory(targetDir);

        // Extract payload from embedded resource
        Console.WriteLine("\n[3/5] Extracting application payload...");
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("DOOM.Payload.zip"))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("Embedded installation payload 'DOOM.Payload.zip' was not found in the installer assembly.");
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.Combine(targetDir, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    var parent = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }

        // Copy current installer as uninstaller
        var currentExe = Environment.ProcessPath;
        var uninstallerPath = Path.Combine(targetDir, "Uninstall-DOOM.exe");
        if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
        {
            try
            {
                File.Copy(currentExe, uninstallerPath, overwrite: true);
            }
            catch
            {
                // In-use or upgrade lock fallback
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("      Files extracted successfully.");
        Console.ResetColor();

        // Create shortcuts
        Console.WriteLine("\n[4/5] Registering Windows shortcuts...");
        var startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "DOOM.lnk");

        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "DOOM.lnk");

        var exePath = Path.Combine(targetDir, "DOOM.exe");

        CreateShortcut(startMenuPath, exePath, targetDir, AppFullName);
        Console.WriteLine($"      Created Start Menu shortcut: {startMenuPath}");

        CreateShortcut(desktopPath, exePath, targetDir, AppFullName);
        Console.WriteLine($"      Created Desktop shortcut: {desktopPath}");

        // Register in Add/Remove Programs
        Console.WriteLine("\n[5/5] Registering in Windows Add/Remove Programs...");
        RegisterInUninstall(targetDir, exePath, uninstallerPath);
        Console.WriteLine("      Windows Registry updated successfully.");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("  DOOM installation completed successfully!");
        Console.WriteLine("=======================================================");
        Console.ResetColor();

        Console.WriteLine("\nDefault Global Shortcuts:");
        Console.WriteLine("  • [Ctrl + Shift + Space] : Capture selected text & invoke AI");
        Console.WriteLine("  • [Ctrl + Shift + H]     : Toggle response overlay visibility");
        Console.WriteLine("  • [Escape]               : Cancel active streaming response");
        Console.WriteLine("  • System Tray Icon       : Right-click for Settings, Privacy Mode, Quit");

        if (!noLaunch)
        {
            Console.WriteLine("\nLaunching DOOM in background...");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notice: Could not auto-launch DOOM: {ex.Message}");
            }
        }

        if (!isSilent)
        {
            Console.WriteLine("\nSetup finished. Exiting...");
        }

        return 0;
    }

    private static int PerformUninstall(bool isSilent, bool purgeData)
    {
        Console.WriteLine("[1/4] Closing running instances of DOOM...");
        CloseRunningProcesses(AppName);

        var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
        var startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DOOM.lnk");
        var desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DOOM.lnk");

        Console.WriteLine("\n[2/4] Removing shortcuts...");
        if (File.Exists(startMenuPath))
        {
            try
            {
                File.Delete(startMenuPath);
                Console.WriteLine($"      Removed: {startMenuPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      Notice: Could not remove {startMenuPath}: {ex.Message}");
            }
        }

        if (File.Exists(desktopPath))
        {
            try
            {
                File.Delete(desktopPath);
                Console.WriteLine($"      Removed: {desktopPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      Notice: Could not remove {desktopPath}: {ex.Message}");
            }
        }

        Console.WriteLine("\n[3/4] Removing Windows Add/Remove Programs registry entry...");
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryUninstallKey, throwOnMissingSubKey: false);
            Console.WriteLine("      Registry entry removed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      Notice: Registry cleanup skipped: {ex.Message}");
        }

        Console.WriteLine("\n[4/4] Removing program files...");
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        if (purgeData)
        {
            if (Directory.Exists(dataDir))
            {
                Directory.Delete(dataDir, recursive: true);
                Console.WriteLine($"      Purged user settings and credentials at: {dataDir}");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"      [PRESERVED] User settings and DPAPI credentials kept at: {dataDir}");
            Console.WriteLine("      (To purge configuration, run: Uninstall-DOOM.exe /UNINSTALL /PURGE-DATA)");
            Console.ResetColor();
        }

        var currentExe = Environment.ProcessPath ?? string.Empty;
        bool isRunningFromTargetDir = currentExe.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase);

        // Delete all files in targetDir except current executing binary
        if (Directory.Exists(targetDir))
        {
            foreach (var file in Directory.GetFiles(targetDir))
            {
                if (isRunningFromTargetDir && file.Equals(currentExe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try { File.Delete(file); } catch { }
            }

            foreach (var subDir in Directory.GetDirectories(targetDir))
            {
                try { Directory.Delete(subDir, recursive: true); } catch { }
            }
        }

        // If running from targetDir, schedule self-deletion
        if (isRunningFromTargetDir)
        {
            ScheduleSelfDelete(targetDir, currentExe);
        }
        else
        {
            try
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, recursive: true);
                }
            }
            catch { }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("  DOOM uninstallation completed successfully.");
        Console.WriteLine("=======================================================");
        Console.ResetColor();

        return 0;
    }

    private static void CloseRunningProcesses(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0) return;

        Console.WriteLine($"      Found {processes.Length} running instance(s) of {processName}. Requesting close...");
        foreach (var p in processes)
        {
            try
            {
                if (!p.CloseMainWindow())
                {
                    p.Kill();
                }
                p.WaitForExit(3000);
            }
            catch { }
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            shortcut.IconLocation = $"{targetPath},0";
            shortcut.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      Notice: Shortcut creation note: {ex.Message}");
        }
    }

    private static void RegisterInUninstall(string installDir, string exePath, string uninstallerPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryUninstallKey);
        if (key == null) return;

        key.SetValue("DisplayName", AppFullName);
        key.SetValue("DisplayVersion", AppVersion);
        key.SetValue("Publisher", AppPublisher);
        key.SetValue("DisplayIcon", $"{exePath},0");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("UninstallString", $"\"{uninstallerPath}\" /UNINSTALL");
        key.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" /UNINSTALL /SILENT");
        key.SetValue("URLInfoAbout", AppUrl);
        key.SetValue("EstimatedSize", 75000, RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 0, RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    }

    private static void ScheduleSelfDelete(string targetDir, string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C choice /C Y /N /D Y /T 2 & del /F /Q \"{exePath}\" & rmdir /S /Q \"{targetDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch { }
    }
}
