namespace DOOM.Platform.Windows.SystemTray;

using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using DOOM.Core.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages the native Windows notification area (system tray) icon and context menu.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SystemTrayCoordinator : ISystemTrayCoordinator
{
    private readonly ILogger<SystemTrayCoordinator> _logger;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _privacyMenuItem;
    private ToolStripMenuItem? _statusMenuItem;
    private bool _isDisposed;

    public SystemTrayCoordinator(ILogger<SystemTrayCoordinator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Initialize(TrayMenuActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var contextMenu = new ContextMenuStrip();

        // 1. Status Item (Disabled header)
        _statusMenuItem = new ToolStripMenuItem("DOOM: Ready") { Enabled = false };
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        // 2. Open DOOM
        var openItem = new ToolStripMenuItem("Open DOOM", null, (_, _) => actions.OnOpenDoom?.Invoke())
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };
        contextMenu.Items.Add(openItem);

        // 3. Settings
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => actions.OnOpenSettings?.Invoke());
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // 4. Toggle Response Panel
        var togglePanelItem = new ToolStripMenuItem("Toggle Response Panel", null, (_, _) => actions.OnTogglePanel?.Invoke());
        contextMenu.Items.Add(togglePanelItem);

        // 5. Privacy Mode Toggle
        _privacyMenuItem = new ToolStripMenuItem("Privacy Mode: ON", null, (_, _) => actions.OnTogglePrivacy?.Invoke())
        {
            Checked = true
        };
        contextMenu.Items.Add(_privacyMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // 6. Quit
        var quitItem = new ToolStripMenuItem("Quit DOOM", null, (_, _) => actions.OnQuit?.Invoke());
        contextMenu.Items.Add(quitItem);

        // Initialize NotifyIcon
        Icon? appIcon = null;
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
        {
            try
            {
                appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
            catch
            {
                // Fall back to default
            }
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = "DOOM — Privacy AI Overlay",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => actions.OnOpenDoom?.Invoke();

        _logger.LogInformation("System tray coordinator initialized successfully.");
    }

    public void UpdatePrivacyStatus(bool enabled)
    {
        if (_privacyMenuItem != null)
        {
            _privacyMenuItem.Text = enabled ? "Privacy Mode: ON" : "Privacy Mode: OFF";
            _privacyMenuItem.Checked = enabled;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.Text = enabled
                ? "DOOM — Privacy Mode Active"
                : "DOOM — Privacy AI Overlay";
        }
    }

    public void UpdateStatusText(string status)
    {
        if (_statusMenuItem != null)
        {
            _statusMenuItem.Text = $"DOOM: {status}";
        }
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _logger.LogInformation("System tray icon disposed.");
    }
}

