namespace GTA5Optimizer.Models.Monitoring;

/// <summary>
/// Метрики производительности в реальном времени
/// </summary>
public sealed class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // FPS
    public double CurrentFPS { get; set; }
    public double AverageFPS { get; set; }
    public double MinFPS { get; set; }
    public double MaxFPS { get; set; }
    public double OnePercentLow { get; set; }
    public double PointOnePercentLow { get; set; }
    public int FrameTimeMs { get; set; }
    public int FrameTimeVariance { get; set; }
    public bool HasStutters { get; set; }
    public int StutterCount { get; set; }

    // CPU
    public double CPUUsage { get; set; }
    public double CPUUsageGame { get; set; }
    public double CPUTemperature { get; set; }
    public double CPUClock { get; set; }
    public int CPUThreadCount { get; set; }
    public double[] PerCoreUsage { get; set; } = Array.Empty<double>();

    // GPU
    public double GPUUsage { get; set; }
    public double GPUUsageGame { get; set; }
    public double GPUTemperature { get; set; }
    public long GPUMemoryUsed { get; set; }
    public long GPUMemoryTotal { get; set; }
    public double GPUMemoryUsagePercent => GPUMemoryTotal > 0 ? (double)GPUMemoryUsed / GPUMemoryTotal * 100 : 0;
    public int GPUEngineClock { get; set; }
    public int GPUMemoryClock { get; set; }
    public double GPUPowerDraw { get; set; }
    public double GPUPowerLimit { get; set; }

    // RAM
    public long TotalRAM { get; set; }
    public long UsedRAM { get; set; }
    public long AvailableRAM { get; set; }
    public long StandbyMemory { get; set; }
    public long ModifiedMemory { get; set; }
    public long GameWorkingSet { get; set; }
    public long GamePrivateBytes { get; set; }
    public double RAMUsagePercent => TotalRAM > 0 ? (double)UsedRAM / TotalRAM * 100 : 0;
    public double StandbyMemoryMB => StandbyMemory / 1024.0 / 1024.0;
    public double GameRAM_MB => GameWorkingSet / 1024.0 / 1024.0;

    // Disk
    public double DiskReadSpeedMBps { get; set; }
    public double DiskWriteSpeedMBps { get; set; }
    public double DiskReadLatencyMs { get; set; }
    public double DiskWriteLatencyMs { get; set; }
    public int DiskQueueLength { get; set; }
    public double DiskActiveTimePercent { get; set; }
    public double GameDiskReadMBps { get; set; }
    public bool IsTextureStreamingBottleneck { get; set; }

    // Network
    public double CurrentPing { get; set; }
    public double PacketLoss { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public int ActiveConnections { get; set; }

    // Optimization stats
    public long TotalMemoryFreed { get; set; }
    public int TotalProcessesClosed { get; set; }
    public int TotalProcessesOptimized { get; set; }
    public int AutoOptimizationRuns { get; set; }
    public DateTime LastOptimizationTime { get; set; }
    public List<string> RecentOptimizations { get; set; } = new();

    // Derived
    public string CPUTempStr => $"{CPUTemperature:F1}°C";
    public string GPUTempStr => $"{GPUTemperature:F1}°C";
    public string RAMUsageStr => $"{RAMUsagePercent:F1}%";
    public string GPUMemoryUsageStr => $"{GPUMemoryUsagePercent:F1}%";
    public string FPSStr => $"{CurrentFPS:F1} FPS";
    public string FrameTimeStr => $"{FrameTimeMs} ms";
    public string DiskReadStr => $"{DiskReadSpeedMBps:F1} MB/s";
    public string PingStr => $"{CurrentPing:F0} ms";
    public bool IsPerformanceGood => CurrentFPS >= 60 && !HasStutters && CPUTemperature < 85 && GPUTemperature < 83;
    public string PerformanceStatus => IsPerformanceGood ? "Хорошо" : "Требует внимания";
}

public sealed class BottleneckAnalysis
{
    public BottleneckType PrimaryBottleneck { get; set; }
    public List<BottleneckDetail> Details { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public double SeverityScore { get; set; } // 0-100
    public DateTime AnalysisTime { get; set; } = DateTime.Now;
}

public enum BottleneckType
{
    None = 0,
    CPU = 1,
    GPU = 2,
    RAM = 3,
    Disk = 4,
    Network = 5,
    Thermal = 6,
    TextureStreaming = 7,
    Driver = 8,
    GameEngine = 9
}

public sealed class BottleneckDetail
{
    public BottleneckType Type { get; set; }
    public string Component { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public double Severity { get; set; } // 0-100
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public bool IsCritical => Severity > 80;
}