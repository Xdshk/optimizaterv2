using GTA5Optimizer.Models.Enums;

namespace GTA5Optimizer.Models.System;

/// <summary>
/// Полная информация о системе
/// </summary>
public sealed class SystemInfo
{
    // OS
    public string OSVersion { get; set; } = string.Empty;
    public string OSBuild { get; set; } = string.Empty;
    public bool IsWindows11 { get; set; }
    public bool IsAdmin { get; set; }
    public string PowerPlan { get; set; } = string.Empty;
    public Guid ActivePowerPlanGuid { get; set; }

    // CPU
    public string CPUName { get; set; } = string.Empty;
    public int CPUPhysicalCores { get; set; }
    public int CPULogicalCores { get; set; }
    public double CPUBaseClock { get; set; }
    public double CPUMaxClock { get; set; }
    public double CurrentCPUUsage { get; set; }
    public double CurrentCPUTemperature { get; set; }
    public string CPUVendor { get; set; } = string.Empty;

    // GPU
    public string GPUName { get; set; } = string.Empty;
    public string GPUVendor { get; set; } = string.Empty;
    public long GPUMemoryTotal { get; set; }
    public long GPUMemoryUsed { get; set; }
    public double GPUUsage { get; set; }
    public double GPUTemperature { get; set; }
    public int GPUEngineClock { get; set; }
    public int GPUMemoryClock { get; set; }

    // RAM
    public long TotalRAM { get; set; }
    public long AvailableRAM { get; set; }
    public long UsedRAM { get; set; }
    public long StandbyMemory { get; set; }
    public long ModifiedMemory { get; set; }
    public double RAMUsagePercent => TotalRAM > 0 ? (double)UsedRAM / TotalRAM * 100 : 0;
    public double StandbyMemoryMB => StandbyMemory / 1024.0 / 1024.0;
    public int MemorySpeed { get; set; }
    public string MemoryType { get; set; } = string.Empty;

    // Disks
    public List<DriveInfo> Drives { get; set; } = new();

    // Network
    public string PrimaryNetworkAdapter { get; set; } = string.Empty;
    public long NetworkSpeed { get; set; }
    public double CurrentPing { get; set; }

    // Display
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int RefreshRate { get; set; }
    public double DpiScale { get; set; }

    // Derived properties for i5-12400F / RTX 3060 12GB / 16GB / 1440p
    public bool IsTargetHardware => CPUName.Contains("i5-12400F", StringComparison.OrdinalIgnoreCase) &&
                                     GPUName.Contains("3060", StringComparison.OrdinalIgnoreCase) &&
                                     TotalRAM >= 16L * 1024 * 1024 * 1024 &&
                                     ScreenWidth >= 2560;

    public string TotalRAM_GB => $"{(double)TotalRAM / 1024 / 1024 / 1024:F1} GB";
    public string GPUMemory_GB => $"{(double)GPUMemoryTotal / 1024 / 1024 / 1024:F1} GB";
    public string AvailableRAM_GB => $"{(double)AvailableRAM / 1024 / 1024 / 1024:F1} GB";
}

public sealed class DriveInfo
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Enums.DriveType Type { get; set; }
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public string FileSystem { get; set; } = string.Empty;
    public bool IsSystemDrive { get; set; }
    public double ReadSpeedMBps { get; set; }
    public double WriteSpeedMBps { get; set; }

    public double FreeSpaceGB => FreeSpace / 1024.0 / 1024.0 / 1024.0;
    public double TotalSpaceGB => TotalSize / 1024.0 / 1024.0 / 1024.0;
    public double UsedPercent => TotalSize > 0 ? (1.0 - (double)FreeSpace / TotalSize) * 100 : 0;
}