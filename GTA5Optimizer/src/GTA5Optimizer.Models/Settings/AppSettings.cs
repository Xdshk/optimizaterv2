using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;
using System.Collections.ObjectModel;

namespace GTA5Optimizer.Models.Settings;

/// <summary>
/// Настройки приложения (сериализуются в JSON)
/// </summary>
public sealed class AppSettings
{
    // General
    public string Language { get; set; } = "ru-RU";
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool RunAsAdmin { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public bool AutoDetectGames { get; set; } = true;

    // Profile
    public OptimizationProfile ActiveProfile { get; set; } = OptimizationProfile.RPMode;
    public ProfileConfig? CustomProfile { get; set; }
    public ObservableCollection<ProfileConfig> SavedProfiles { get; set; } = new();

    // Game paths (auto-detected or manual)
    public string? GTA5Path { get; set; }
    public string? MajesticPath { get; set; }
    public bool GTA5PathVerified { get; set; }
    public bool MajesticPathVerified { get; set; }

    // Monitoring
    public bool EnableMonitoring { get; set; } = true;
    public int MonitoringUpdateIntervalMs { get; set; } = 500;
    public bool ShowFPSOverlay { get; set; } = false;
    public int OverlayPosition { get; set; } = 0; // 0=TopLeft, 1=TopRight, 2=BottomLeft, 3=BottomRight
    public bool LogPerformanceData { get; set; } = true;
    public int MaxLogEntries { get; set; } = 10000;
    public int MaxPerformanceHistoryPoints { get; set; } = 300; // 5 minutes at 1s interval

    // Auto-optimization
    public bool EnableAutoOptimization { get; set; } = true;
    public int AutoOptimizationIntervalSeconds { get; set; } = 30;
    public bool AutoOptimizeOnlyInGame { get; set; } = true;
    public bool NotifyOnOptimization { get; set; } = false;
    public bool ShowToastNotifications { get; set; } = true;

    // Safety
    public bool CreateRestorePoints { get; set; } = true;
    public bool BackupRegistry { get; set; } = true;
    public int MaxRestorePoints { get; set; } = 10;
    public bool ConfirmDangerousActions { get; set; } = true;
    public bool EnableRollbackOnError { get; set; } = true;

    // Hardware specific (i5-12400F / RTX 3060 12GB / 16GB / 1440p)
    public HardwareProfile HardwareProfile { get; set; } = new();
    public bool UseHardwareOptimizations { get; set; } = true;

    // Advanced
    public bool EnableExperimentalFeatures { get; set; } = false;
    public bool EnableDetailedLogging { get; set; } = false;
    public string LogDirectory { get; set; } = string.Empty; // Empty = default
    public int LogRetentionDays { get; set; } = 30;
    public bool ExportTelemetry { get; set; } = false;

    // Window state
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; } = -1; // -1 = center
    public double WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; } = false;
    public int SelectedTabIndex { get; set; } = 0;

    // Protected processes (never close)
    public ObservableCollection<string> ProtectedProcesses { get; set; } = new()
    {
        "Spotify", "Discord", "explorer", "dwm", "csrss", "winlogon", "services", "lsass"
    };

    // Custom process rules
    public ObservableCollection<GTA5Optimizer.Models.Optimization.ProcessRule> CustomProcessRules { get; set; } = new();

    // Theme
    public string Theme { get; set; } = "Dark"; // Dark, System
    public string AccentColor { get; set; } = "#8B6F6F";
}

public sealed class HardwareProfile
{
    public string CPUName { get; set; } = "Intel Core i5-12400F";
    public string GPUName { get; set; } = "NVIDIA GeForce RTX 3060 12GB";
    public long TotalRAMBytes { get; set; } = 16L * 1024 * 1024 * 1024;
    public int ScreenWidth { get; set; } = 2560;
    public int ScreenHeight { get; set; } = 1440;
    public int RefreshRate { get; set; } = 144;
    public bool GTA5OnHDD { get; set; } = true;
    public bool MajesticOnSSD { get; set; } = true;
    public string GTA5DriveLetter { get; set; } = "D:";
    public string MajesticDriveLetter { get; set; } = "C:";

    // Optimized values for this hardware
    public int OptimalGamePriority { get; set; } = 128; // High
    public int OptimalCPUAffinityMask { get; set; } = 0x3F; // Cores 0-5 (all 6 cores)
    public double OptimalMemoryCleanupThreshold { get; set; } = 75.0;
    public int OptimalMemoryCleanupInterval { get; set; } = 30;
    public long OptimalMaxStandbyMemory { get; set; } = 1536 * 1024 * 1024; // 1.5 GB
    public bool PreferSuperfetchForHDD { get; set; } = true;
    public double MaxSafeCPUTemp { get; set; } = 85.0;
    public double MaxSafeGPUTemp { get; set; } = 83.0;
    public int TargetFPS { get; set; } = 144;
}

