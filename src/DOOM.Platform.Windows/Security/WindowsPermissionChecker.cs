namespace DOOM.Platform.Windows.Security;

using System.Security.Principal;
using DOOM.Core.Contracts;

/// <summary>
/// Checks Windows process security context and accessibility subsystem availability.
/// </summary>
public sealed class WindowsPermissionChecker : IPermissionChecker
{
    public bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool IsUiAutomationAvailable()
    {
        // Windows UI Automation is a native component of Windows 11
        return OperatingSystem.IsWindows();
    }
}

