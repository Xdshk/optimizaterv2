using GTA5Optimizer.Models.Settings;
using Microsoft.Extensions.Logging;
using System.Management;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Detects actual hardware configuration at runtime.
/// Replaces all hardcoded values in HardwareProfile.
/// </summary>
public sealed class SystemInfoDetector
{
    private readonly ILogger<SystemInfoDetector> _logger;

    [DllImport("kernel32.dll")]
    private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryKB);

    public SystemInfoDetector(ILogger<SystemInfoDetector> logger)
    {
        _logger = logger;
    }

    public HardwareProfile DetectHardwareProfile()
    {
        var profile = new HardwareProfile
        {
            CPUName = DetectCPU(),
            GPUName = DetectGPU(),
            TotalRAMBytes = DetectTotalRAM(),
            ScreenWidth = DetectScreenWidth(),
            ScreenHeight = DetectScreenHeight(),
            RefreshRate = DetectRefreshRate(),
            TargetFPS = DetectRefreshRate(), // Default target = monitor refresh
        };

        _logger.LogInformation("Hardware detected: CPU={CPU}, GPU={GPU}, RAM={RAM}GB, Screen={Wx}H@{Hz}",
            profile.CPUName, profile.GPUName, profile.TotalRAMBytes / (1024L * 1024 * 1024),
            profile.ScreenWidth, profile.ScreenHeight, profile.RefreshRate);

        return profile;
    }

    private static string DetectCPU()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject? mo in searcher.Get())
            {
                var name = mo["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CPU detection failed: {ex.Message}");
        }
        return "Unknown CPU";
    }

    private static string DetectGPU()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            string? bestGPU = null;
            long bestRam = 0;
            foreach (ManagementObject? mo in searcher.Get())
            {
                var name = mo["Name"]?.ToString()?.Trim();
                var ram = Convert.ToInt64(mo["AdapterRAM"] ?? 0);
                if (!string.IsNullOrEmpty(name) && ram > bestRam)
                {
                    bestGPU = name;
                    bestRam = ram;
                }
            }
            if (!string.IsNullOrEmpty(bestGPU))
                return bestGPU;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPU detection failed: {ex.Message}");
        }
        return "Unknown GPU";
    }

    private static long DetectTotalRAM()
    {
        try
        {
            GetPhysicallyInstalledSystemMemory(out long totalKB);
            if (totalKB > 0)
                return totalKB * 1024;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RAM detection via kernel32 failed: {ex.Message}");
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            long total = 0;
            foreach (ManagementObject? mo in searcher.Get())
            {
                total += Convert.ToInt64(mo["Capacity"] ?? 0);
            }
            if (total > 0)
                return total;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RAM detection via WMI failed: {ex.Message}");
        }

        return 0;
    }

    private static int DetectScreenWidth()
    {
        try
        {
            return System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
        }
        catch
        {
            return 1920;
        }
    }

    private static int DetectScreenHeight()
    {
        try
        {
            return System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;
        }
        catch
        {
            return 1080;
        }
    }

    private static int DetectRefreshRate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject? mo in searcher.Get())
            {
                var rate = Convert.ToInt32(mo["CurrentRefreshRate"] ?? 60);
                if (rate > 0)
                    return rate;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh rate detection failed: {ex.Message}");
        }
        return 60;
    }
}
