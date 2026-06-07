using CommunityToolkit.Mvvm.ComponentModel;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки мониторинга
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private readonly IPerformanceMonitor _monitor;
    private readonly Timer _updateTimer;

    [ObservableProperty]
    private PerformanceMetrics _metrics = new();

    [ObservableProperty]
    private BottleneckAnalysis _bottleneck = new();

    public MonitorViewModel(IPerformanceMonitor monitor)
    {
        _monitor = monitor;
        _monitor.OnMetricsUpdated += OnMetricsUpdated;

        _updateTimer = new Timer(async _ => await UpdateMetricsAsync(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    private async Task UpdateMetricsAsync()
    {
        var metrics = await _monitor.GetCurrentMetricsAsync();
        Metrics = metrics;

        var analysis = await _monitor.AnalyzeBottlenecksAsync(metrics);
        Bottleneck = analysis;
    }

    private void OnMetricsUpdated(PerformanceMetrics metrics)
    {
        Metrics = metrics;
    }

    public void Dispose()
    {
        _updateTimer?.Dispose();
        _monitor.OnMetricsUpdated -= OnMetricsUpdated;
    }
}