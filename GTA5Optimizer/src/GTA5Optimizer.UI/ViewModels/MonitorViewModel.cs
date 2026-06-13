using CommunityToolkit.Mvvm.ComponentModel;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки мониторинга.
/// Подписывается на PerformanceMonitor.OnMetricsUpdated и обновляет
/// сглаженные метрики для UI (предотвращает мерцание).
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private readonly IPerformanceMonitor _monitor;
    private readonly System.Windows.Threading.DispatcherTimer _analysisTimer;

    // Raw metrics from PerformanceMonitor (updated by event)
    [ObservableProperty]
    private PerformanceMetrics _metrics = new();

    // Smoothed values for UI bindings (prevents flickering)
    [ObservableProperty]
    private double _smoothedCpu;
    [ObservableProperty]
    private double _smoothedGpu;
    [ObservableProperty]
    private double _smoothedRam;
    [ObservableProperty]
    private double _smoothedFps;

    [ObservableProperty]
    private BottleneckAnalysis _bottleneck = new();

    // EMA smoothing factor (0 = no change, 1 = no smoothing)
    private const double Alpha = 0.35;

    public MonitorViewModel(IPerformanceMonitor monitor)
    {
        _monitor = monitor;

        // Subscribe to real-time metrics updates from PerformanceMonitor
        _monitor.OnMetricsUpdated += OnMetricsUpdated;

        // Bottleneck analysis runs less frequently (every 2s) — it's CPU-heavy
        _analysisTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _analysisTimer.Tick += async (_, _) => await UpdateBottleneckAsync();
        _analysisTimer.Start();
    }

    private void OnMetricsUpdated(PerformanceMetrics metrics)
    {
        // This is called from PerformanceMonitor's background thread — dispatch to UI
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Metrics = metrics;

            // Apply exponential moving average to prevent UI flickering
            SmoothedFps = Smooth(SmoothedFps, metrics.CurrentFPS);
            SmoothedCpu = Smooth(SmoothedCpu, metrics.CPUUsage);
            SmoothedGpu = Smooth(SmoothedGpu, metrics.GPUUsage);
            SmoothedRam = Smooth(SmoothedRam, metrics.RAMUsagePercent);
        });
    }

    private static double Smooth(double current, double newValue)
    {
        if (current == 0) return newValue;
        if (newValue == 0) return current * 0.9; // Decay to 0 gradually
        return current * (1 - Alpha) + newValue * Alpha;
    }

    private async Task UpdateBottleneckAsync()
    {
        try
        {
            var snapshot = Metrics;
            if (snapshot != null)
            {
                var analysis = await _monitor.AnalyzeBottlenecksAsync(snapshot);
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Bottleneck = analysis;
                });
            }
        }
        catch (Exception)
        {
            // Silently ignore — bottleneck analysis is non-critical
        }
    }

    public void Dispose()
    {
        _analysisTimer?.Stop();
        _monitor.OnMetricsUpdated -= OnMetricsUpdated;
    }
}
