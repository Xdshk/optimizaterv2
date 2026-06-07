using GTA5Optimizer.Models.Enums;
using System.Collections.ObjectModel;

namespace GTA5Optimizer.Models.Optimization;

/// <summary>
/// Конфигурация профиля оптимизации
/// </summary>
public sealed class ProfileConfig
{
    public OptimizationProfile Profile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsCustom { get; set; }

    // Power settings
    public bool SetHighPerformancePowerPlan { get; set; }
    public bool DisableCoreParking { get; set; }
    public bool DisableCStates { get; set; }
    public int ProcessorPerformanceBoostMode { get; set; } = 2; // Aggressive
    public int ProcessorPerformanceCoreParkingOverride { get; set; } = 1;

    // Process priority
    public int GamePriorityClass { get; set; } = 128; // High
    public int LauncherPriorityClass { get; set; } = 128; // High
    public int RockstarProcessesPriorityClass { get; set; } = 128; // High
    public bool EnablePriorityBoost { get; set; } = true;
    public bool LockGameToPCCores { get; set; } = false;
    public int[]? PreferredCores { get; set; }

    // Background processes
    public bool AggressiveBackgroundCleanup { get; set; }
    public ObservableCollection<ProcessRule> ProcessRules { get; set; } = new();
    public bool CloseBrowsers { get; set; }
    public bool CloseDiscordOverlay { get; set; }
    public bool CloseXboxGameBar { get; set; }
    public bool CloseTeams { get; set; }
    public bool CloseOneDrive { get; set; }
    public bool CloseSteamWebHelper { get; set; }
    public bool CloseOverwolf { get; set; }
    public List<string> ProtectedProcesses { get; set; } = new() { "Spotify", "Discord" };

    // Memory
    public bool EnableMemoryCleanup { get; set; } = true;
    public double MemoryCleanupThresholdPercent { get; set; } = 80.0;
    public int MemoryCleanupIntervalSeconds { get; set; } = 30;
    public bool CleanStandbyMemory { get; set; } = true;
    public bool CleanWorkingSet { get; set; } = true;
    public bool CleanModifiedMemory { get; set; } = false;
    public long MaxStandbyMemoryMB { get; set; } = 2048;

    // Disk
    public bool EnableDiskOptimization { get; set; } = true;
    public bool EnableWriteCaching { get; set; } = true;
    public bool Disable8dot3Names { get; set; } = true;
    public bool DisableLastAccess { get; set; } = true;
    public bool EnablePrefetch { get; set; } = true;
    public bool EnableSuperfetch { get; set; } = false; // Off for SSD, On for HDD
    public int LargeSystemCache { get; set; } = 1;

    // Windows Services
    public bool DisableGameDVR { get; set; } = true;
    public bool DisableXboxServices { get; set; } = true;
    public bool DisableTelemetry { get; set; } = true;
    public bool DisableWindowsSearch { get; set; } = false;
    public bool DisableSysMain { get; set; } = false;
    public bool OptimizeTaskScheduler { get; set; } = true;
    public ObservableCollection<ServiceRule> ServiceRules { get; set; } = new();

    // Network
    public bool OptimizeNetworkStack { get; set; } = true;
    public bool DisableNagleAlgorithm { get; set; } = true;
    public bool EnableTCPNoDelay { get; set; } = true;
    public int TcpAckFrequency { get; set; } = 1;
    public bool PrioritizeGameTraffic { get; set; } = true;
    public bool DisableQoS { get; set; } = true;

    // Registry
    public bool CreateRestorePoint { get; set; } = true;
    public bool BackupRegistry { get; set; } = true;
    public string RestorePointPrefix { get; set; } = "GTA5Optimizer_";

    // Monitoring
    public bool EnableAutoOptimization { get; set; } = true;
    public int AutoOptimizationIntervalSeconds { get; set; } = 30;
    public bool MonitorTemperature { get; set; } = true;
    public double MaxCPUTemperature { get; set; } = 85.0;
    public double MaxGPUTemperature { get; set; } = 83.0;
    public bool ThrottleOnHighTemp { get; set; } = true;

    // Majestic RP specific
    public bool OptimizeMajesticCache { get; set; } = true;
    public bool ClearMajesticLogs { get; set; } = false;
    public long MaxMajesticCacheMB { get; set; } = 1024;

    public static ProfileConfig GetDefaultProfile(OptimizationProfile profile)
    {
        return profile switch
        {
            OptimizationProfile.Everyday => CreateEverydayProfile(),
            OptimizationProfile.RPMode => CreateRPModeProfile(),
            OptimizationProfile.MassiveOnline => CreateMassiveOnlineProfile(),
            OptimizationProfile.MaximumFPS => CreateMaximumFPSProfile(),
            _ => CreateRPModeProfile()
        };
    }

    private static ProfileConfig CreateEverydayProfile()
    {
        return new ProfileConfig
        {
            Profile = OptimizationProfile.Everyday,
            Name = "Everyday Mode",
            Description = "Минимальные изменения для повседневной работы. Максимальная совместимость.",
            IsDefault = true,
            SetHighPerformancePowerPlan = false,
            DisableCoreParking = false,
            GamePriorityClass = 64, // Normal
            LauncherPriorityClass = 64,
            RockstarProcessesPriorityClass = 64,
            AggressiveBackgroundCleanup = false,
            CloseBrowsers = false,
            CloseDiscordOverlay = false,
            CloseXboxGameBar = true,
            CloseTeams = false,
            CloseOneDrive = false,
            CloseSteamWebHelper = false,
            CloseOverwolf = false,
            EnableMemoryCleanup = true,
            MemoryCleanupThresholdPercent = 90,
            MemoryCleanupIntervalSeconds = 300,
            CleanStandbyMemory = true,
            CleanWorkingSet = false,
            EnableDiskOptimization = true,
            DisableGameDVR = true,
            DisableXboxServices = true,
            DisableTelemetry = true,
            OptimizeNetworkStack = false,
            EnableAutoOptimization = false,
            MonitorTemperature = true,
            MaxCPUTemperature = 90,
            MaxGPUTemperature = 88,
            ProtectedProcesses = new() { "Spotify", "Discord", "Chrome", "Edge", "Opera", "Teams", "OneDrive", "Steam" }
        };
    }

    private static ProfileConfig CreateRPModeProfile()
    {
        return new ProfileConfig
        {
            Profile = OptimizationProfile.RPMode,
            Name = "RP Mode",
            Description = "Оптимизированный баланс для RP. Приоритет стабильности FPS и плавности.",
            IsDefault = false,
            SetHighPerformancePowerPlan = true,
            DisableCoreParking = true,
            DisableCStates = true,
            GamePriorityClass = 128, // High
            LauncherPriorityClass = 128,
            RockstarProcessesPriorityClass = 128,
            EnablePriorityBoost = true,
            AggressiveBackgroundCleanup = true,
            CloseBrowsers = true,
            CloseDiscordOverlay = true,
            CloseXboxGameBar = true,
            CloseTeams = true,
            CloseOneDrive = true,
            CloseSteamWebHelper = true,
            CloseOverwolf = true,
            EnableMemoryCleanup = true,
            MemoryCleanupThresholdPercent = 75,
            MemoryCleanupIntervalSeconds = 30,
            CleanStandbyMemory = true,
            CleanWorkingSet = true,
            MaxStandbyMemoryMB = 1536,
            EnableDiskOptimization = true,
            EnableSuperfetch = true, // HDD optimization
            DisableGameDVR = true,
            DisableXboxServices = true,
            DisableTelemetry = true,
            DisableSysMain = false, // Keep for HDD
            OptimizeTaskScheduler = true,
            OptimizeNetworkStack = true,
            DisableNagleAlgorithm = true,
            EnableTCPNoDelay = true,
            PrioritizeGameTraffic = true,
            DisableQoS = true,
            CreateRestorePoint = true,
            BackupRegistry = true,
            EnableAutoOptimization = true,
            AutoOptimizationIntervalSeconds = 30,
            MonitorTemperature = true,
            MaxCPUTemperature = 85,
            MaxGPUTemperature = 83,
            ThrottleOnHighTemp = true,
            OptimizeMajesticCache = true,
            ClearMajesticLogs = false,
            MaxMajesticCacheMB = 1024,
            ProtectedProcesses = new() { "Spotify", "Discord" }
        };
    }

    private static ProfileConfig CreateMassiveOnlineProfile()
    {
        var config = CreateRPModeProfile();
        config.Profile = OptimizationProfile.MassiveOnline;
        config.Name = "Massive Online Mode";
        config.Description = "Для массовых ивентов (100+ игроков). Агрессивная очистка памяти и приоритеты.";
        config.MemoryCleanupThresholdPercent = 70;
        config.MemoryCleanupIntervalSeconds = 15;
        config.MaxStandbyMemoryMB = 1024;
        config.CleanModifiedMemory = true;
        config.GamePriorityClass = 256; // Realtime (with caution)
        config.RockstarProcessesPriorityClass = 128;
        config.AggressiveBackgroundCleanup = true;
        config.DisableSysMain = true;
        config.EnableSuperfetch = false;
        config.MaxCPUTemperature = 80;
        config.MaxGPUTemperature = 80;
        config.ThrottleOnHighTemp = true;
        return config;
    }

    private static ProfileConfig CreateMaximumFPSProfile()
    {
        var config = CreateMassiveOnlineProfile();
        config.Profile = OptimizationProfile.MaximumFPS;
        config.Name = "Maximum FPS Mode";
        config.Description = "Максимальный FPS. Все ресурсы в игру, отключение всего лишнего.";
        config.DisableCoreParking = true;
        config.DisableCStates = true;
        config.ProcessorPerformanceBoostMode = 3; // Maximum
        config.GamePriorityClass = 256; // Realtime
        config.LockGameToPCCores = true;
        config.PreferredCores = new[] { 0, 1, 2, 3, 4, 5 }; // All P-cores for 12400F
        config.MemoryCleanupThresholdPercent = 65;
        config.MemoryCleanupIntervalSeconds = 10;
        config.MaxStandbyMemoryMB = 512;
        config.CleanWorkingSet = true;
        config.CleanModifiedMemory = true;
        config.EnableSuperfetch = false;
        config.DisableSysMain = true;
        config.DisableWindowsSearch = true;
        config.OptimizeNetworkStack = true;
        config.MaxCPUTemperature = 75;
        config.MaxGPUTemperature = 78;
        config.ProtectedProcesses = new() { "Discord" }; // Only Discord for voice
        return config;
    }
}

public sealed class ProcessRule
{
    public string ProcessName { get; set; } = string.Empty;
    public ProcessActionType Action { get; set; }
    public int PriorityClass { get; set; } = 64;
    public bool IsEnabled { get; set; } = true;
    public string? WindowTitleContains { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class ServiceRule
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Disable { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;
    public ServiceStartMode OriginalStartMode { get; set; }
}

public enum ServiceStartMode
{
    Boot = 0,
    System = 1,
    Auto = 2,
    Manual = 3,
    Disabled = 4
}