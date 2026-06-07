using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Game;
using GTA5Optimizer.Models.Logging;
using GTA5Optimizer.Models.Monitoring;
using GTA5Optimizer.Models.Optimization;
using GTA5Optimizer.Models.Settings;
using GTA5Optimizer.Models.System;

namespace GTA5Optimizer.Core.Interfaces;

/// <summary>
/// Главный интерфейс для оптимизации системы
/// </summary>
public interface ISystemOptimizer
{
    Task<bool> ApplyOptimizationsAsync(OptimizationProfile profile);
    Task<bool> RestoreDefaultsAsync();
    Task<bool> EnableHighPerformanceModeAsync();
    Task<bool> DisableHighPerformanceModeAsync();
}

/// <summary>
/// Интерфейс для работы с реестром
/// </summary>
public interface IRegistryManager
{
    Task<bool> CreateRestorePointAsync(string description);
    Task<bool> BackupRegistryKeyAsync(string keyPath);
    Task<bool> RestoreRegistryKeyAsync(string keyPath);
    Task<T?> ReadRegistryValueAsync<T>(string keyPath, string valueName);
    Task<bool> WriteRegistryValueAsync(string keyPath, string valueName, object value);
}

/// <summary>
/// Интерфейс для управления процессами
/// </summary>
public interface IProcessManager
{
    Task<bool> SetProcessPriorityAsync(int processId, ProcessPriority priority);
    Task<bool> SetProcessAffinityAsync(int processId, int affinityMask);
    Task<bool> SuspendProcessAsync(int processId);
    Task<bool> ResumeProcessAsync(int processId);
    Task<bool> KillProcessAsync(int processId);
    Task<List<RunningProcess>> GetRunningProcessesAsync();
    Task<List<RunningProcess>> GetProcessesByNameAsync(string processName);
    Task<bool> IsProcessRunningAsync(string processName);
}

/// <summary>
/// Интерфейс для управления памятью
/// </summary>
public interface IMemoryManager
{
    Task<MemoryOptimizationResult> OptimizeMemoryAsync();
    Task<long> GetAvailableMemoryAsync();
    Task<long> GetStandbyMemoryAsync();
    Task<bool> ClearStandbyMemoryAsync();
    Task<bool> TrimWorkingSetAsync(int processId);
}

/// <summary>
/// Интерфейс для оптимизации диска
/// </summary>
public interface IDiskOptimizer
{
    Task<DiskOptimizationResult> AnalyzeDiskAsync();
    Task<bool> ClearDiskCacheAsync();
    Task<bool> OptimizeForGamingAsync();
}

/// <summary>
/// Интерфейс для мониторинга производительности
/// </summary>
public interface IPerformanceMonitor
{
    Task<PerformanceMetrics> GetCurrentMetricsAsync();
    Task<BottleneckAnalysis> AnalyzeBottlenecksAsync(PerformanceMetrics metrics);
    event Action<PerformanceMetrics>? OnMetricsUpdated;
    void StartMonitoring();
    void StopMonitoring();
}

/// <summary>
/// Интерфейс обнаружения игры
/// </summary>
public interface IGameDetector
{
    Task<GameInfo> DetectGameAsync();
    Task<MajesticInfo> DetectMajesticRPAsync();
    Task<bool> IsGameRunningAsync();
}

/// <summary>
/// Интерфейс для работы с профилями оптимизации
/// </summary>
public interface IProfileManager
{
    Task<ProfileConfig> GetActiveProfileAsync();
    Task<bool> ApplyProfileAsync(OptimizationProfile profile);
    Task<List<ProfileConfig>> GetAvailableProfilesAsync();
}

/// <summary>
/// Интерфейс логирования
/// </summary>
public interface ILoggerService
{
    Task LogAsync(LogEntry entry);
    Task<List<LogEntry>> GetRecentLogsAsync(int count = 100);
    Task ClearLogsAsync();
}
