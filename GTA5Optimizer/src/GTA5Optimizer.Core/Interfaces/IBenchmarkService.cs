using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.Core.Interfaces;

public interface IBenchmarkService
{
    /// <summary>
    /// Runs a benchmark for the specified duration and returns results.
    /// </summary>
    Task<BenchmarkResult> RunBenchmarkAsync(TimeSpan duration, CancellationToken ct = default);

    /// <summary>
    /// Captures a snapshot of current metrics for before/after comparison.
    /// </summary>
    Task<BenchmarkSnapshot> CaptureSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Compares two snapshots and returns a detailed comparison.
    /// </summary>
    BenchmarkComparison CompareSnapshots(BenchmarkSnapshot before, BenchmarkSnapshot after);
}

public sealed class BenchmarkResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public double AverageFPS { get; set; }
    public double MinFPS { get; set; }
    public double MaxFPS { get; set; }
    public double OnePercentLowFPS { get; set; }
    public double PointOnePercentLowFPS { get; set; }
    public double AverageFrameTimeMs { get; set; }
    public double MaxFrameTimeMs { get; set; }
    public double AverageCPUUsage { get; set; }
    public double AverageGPUUsage { get; set; }
    public double AverageRAMUsagePercent { get; set; }
    public double PeakCPUTemperature { get; set; }
    public double PeakGPUTemperature { get; set; }
    public int StutterCount { get; set; }
    public double StutterSeverity { get; set; }
    public List<PerformanceMetrics> RawData { get; set; } = new();
}

public sealed class BenchmarkSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public PerformanceMetrics Metrics { get; set; } = new();
    public double CurrentFPS => Metrics.CurrentFPS;
    public double CPUUsage => Metrics.CPUUsage;
    public double GPUUsage => Metrics.GPUUsage;
    public double RAMUsagePercent => Metrics.RAMUsagePercent;
    public double CPUTemperature => Metrics.CPUTemperature;
    public double GPUTemperature => Metrics.GPUTemperature;
    public double FrameTimeMs => Metrics.FrameTimeMs;
}

public sealed class BenchmarkComparison
{
    public BenchmarkSnapshot Before { get; set; } = new();
    public BenchmarkSnapshot After { get; set; } = new();
    public double FPS_Gain => After.CurrentFPS - Before.CurrentFPS;
    public double FPS_GainPercent => Before.CurrentFPS > 0 ? (FPS_Gain / Before.CurrentFPS) * 100 : 0;
    public double FrameTime_Reduction => Before.FrameTimeMs - After.FrameTimeMs;
    public double CPUUsage_Change => After.CPUUsage - Before.CPUUsage;
    public double GPUUsage_Change => After.GPUUsage - Before.GPUUsage;
    public double RAMUsage_Change => After.RAMUsagePercent - Before.RAMUsagePercent;
    public double CPUTemp_Change => After.CPUTemperature - Before.CPUTemperature;
    public double GPUTemp_Change => After.GPUTemperature - Before.GPUTemperature;
    public bool IsImproved => FPS_Gain > 0;
    public string Summary => GenerateSummary();

    private string GenerateSummary()
    {
        if (FPS_GainPercent > 10)
            return $"Значительный прирост FPS: +{FPS_GainPercent:F1}% (+{FPS_Gain:F0} FPS)";
        if (FPS_GainPercent > 0)
            return $"Небольшой прирост FPS: +{FPS_GainPercent:F1}% (+{FPS_Gain:F0} FPS)";
        if (FPS_GainPercent == 0)
            return "FPS не изменился";
        return $"FPS снизился: {FPS_GainPercent:F1}% ({FPS_Gain:F0} FPS)";
    }
}
