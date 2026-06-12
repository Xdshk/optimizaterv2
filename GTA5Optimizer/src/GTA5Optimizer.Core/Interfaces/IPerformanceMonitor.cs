using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.Core.Interfaces;

public interface IPerformanceMonitor
{
    Task<PerformanceMetrics> GetCurrentMetricsAsync();
    Task<BottleneckAnalysis> AnalyzeBottlenecksAsync(PerformanceMetrics metrics);
    event Action<PerformanceMetrics>? OnMetricsUpdated;
    void StartMonitoring();
    void StopMonitoring();
    void ReportFrame(); // For FPS tracking from external sources
}
