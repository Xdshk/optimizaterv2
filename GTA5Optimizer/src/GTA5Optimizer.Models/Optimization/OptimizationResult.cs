using GTA5Optimizer.Models.Enums;

namespace GTA5Optimizer.Models.Optimization;

/// <summary>
/// Результат операции оптимизации
/// </summary>
public sealed class OptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public OptimizationCategory Category { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }
    public bool RequiresReboot { get; set; }
    public bool RestorePointCreated { get; set; }
    public string? RestorePointName { get; set; }
    public List<RegistryBackup> RegistryBackups { get; set; } = new();
    public List<string> AffectedProcesses { get; set; } = new();
    public long MemoryFreedBytes { get; set; }
    public int ProcessesClosed { get; set; }
    public int ProcessesOptimized { get; set; }
    public Exception? Exception { get; set; }

    public string MemoryFreedMB => $"{MemoryFreedBytes / 1024.0 / 1024.0:F1} MB";
    public string DurationMs => $"{Duration.TotalMilliseconds:F0} ms";
    public bool NeedsSSDWarning { get; set; }
}

public enum OptimizationCategory
{
    PowerPlan = 0,
    ProcessPriority = 1,
    BackgroundProcesses = 2,
    MemoryCleanup = 3,
    DiskOptimization = 4,
    WindowsServices = 5,
    RegistryTweaks = 6,
    NetworkOptimization = 7,
    GameSpecific = 8,
    ThermalManagement = 9,
    FullProfile = 10,
    Rollback = 11
}

public sealed class RegistryBackup
{
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public object? OriginalValue { get; set; }
    public Microsoft.Win32.RegistryValueKind ValueKind { get; set; }
    public DateTime BackupTime { get; set; } = DateTime.Now;
}