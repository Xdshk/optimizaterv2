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
    private readonly System.Windows.Threading.DispatcherTimer _analysisTimer;

    [ObservableProperty]
    private PerformanceMetrics _metrics = new();

    [ObservableProperty]
    private BottleneckAnalysis _bottleneck = new();

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
        });
    }

    private async Task UpdateBottleneckAsync()
    {
        try
        {
            // Use cached metrics — don't trigger a fresh heavy update
            PerformanceMetrics snapshot;
            lock (this)
            {
                snapshot = Metrics;
            }

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