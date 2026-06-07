using GTA5Optimizer.Models.Enums;
using System.Diagnostics;

namespace GTA5Optimizer.Models.Game;

/// <summary>
/// Информация о Majestic RP Launcher
/// </summary>
public sealed class MajesticInfo
{
    public string InstallPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Enums.DriveType DriveType { get; set; }
    public bool IsRunning { get; set; }
    public Process? LauncherProcess { get; set; }
    public int ProcessId { get; set; }
    public List<Process> RelatedProcesses { get; set; } = new();
    public string? CachePath { get; set; }
    public string? LogsPath { get; set; }
    public long CacheSize { get; set; }
    public DateTime? LastLaunchTime { get; set; }

    public double CacheSizeMB => CacheSize / 1024.0 / 1024.0;
    public bool IsOnSSD => DriveType == Enums.DriveType.SSD || DriveType == Enums.DriveType.NVMe;
}
