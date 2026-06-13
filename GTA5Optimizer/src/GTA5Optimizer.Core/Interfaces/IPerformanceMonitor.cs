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

/// <summary>
/// Считает FPS экрана через DXGI Desktop Duplication API.
/// Независим от конкретной игры — считает все кадры дисплея.
/// </summary>
public interface IScreenFpsCounter : IDisposable
{
    /// <summary>Текущий FPS экрана</summary>
    double CurrentFPS { get; }
    /// <summary>Запускает захват кадров в фоновом потоке</summary>
    void StartCapture();
    /// <summary>Останавливает захват</summary>
    void StopCapture();
}
