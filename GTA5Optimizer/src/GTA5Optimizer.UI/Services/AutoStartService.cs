using Microsoft.Win32;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Управление автозапуском с Windows
/// </summary>
public static class AutoStartService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "GTA5Optimizer";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }
    }

    public static void Enable()
    {
        try
        {
            var exePath = System.IO.Path.Combine(AppContext.BaseDirectory, "GTA5Optimizer.exe");
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch { }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch { }
    }

    public static void Toggle()
    {
        if (IsEnabled) Disable();
        else Enable();
    }
}
