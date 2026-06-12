using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Monitoring;
using Microsoft.Extensions.Logging;

namespace GTA5Optimizer.Services.Services;

public sealed class BenchmarkService : IBenchmarkService
{
    private readonly ILogger<BenchmarkService> _logger;
    private readonly IPerformanceMonitor _performanceMonitor;

    public BenchmarkService(
        ILogger<BenchmarkService> logger,
        IPerformanceMonitor performanceMonitor)
    {
        _logger = logger;
        _performanceMonitor = performanceMonitor;
    }

    public async Task<BenchmarkResult> RunBenchmarkAsync(TimeSpan duration, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting benchmark for {Duration}s...", duration.TotalSeconds);
        var result = new BenchmarkResult { StartTime = DateTime.Now };
        var fpsValues = new List<double>();
        var frameTimes = new List<double>();
        var cpuUsages = new List<double>();
        var gpuUsages = new List<double>();
        var ramUsages = new List<double>();
        var cpuTemps = new List<double>();
        var gpuTemps = new List<double>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sampleInterval = TimeSpan.FromMilliseconds(500);
        double prevFps = 0;

        while (sw.Elapsed < duration && !ct.IsCancellationRequested)
        {
            try
            {
                var metrics = await _performanceMonitor.GetCurrentMetricsAsync();
                result.RawData.Add(metrics);

                fpsValues.Add(metrics.CurrentFPS);
                frameTimes.Add(metrics.FrameTimeMs);
                cpuUsages.Add(metrics.CPUUsage);
                gpuUsages.Add(metrics.GPUUsage);
                ramUsages.Add(metrics.RAMUsagePercent);
                cpuTemps.Add(metrics.CPUTemperature);
                gpuTemps.Add(metrics.GPUTemperature);

                // Detect stutters (sudden FPS drops)
                if (prevFps > 0 && metrics.CurrentFPS > 0)
                {
                    var drop = (prevFps - metrics.CurrentFPS) / prevFps;
                    if (drop > 0.5) // >50% sudden drop
                    {
                        result.StutterCount++;
                        result.StutterSeverity += drop;
                    }
                }
                prevFps = metrics.CurrentFPS;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Benchmark sample error");
            }

            await Task.Delay(sampleInterval, ct).ConfigureAwait(false);
        }

        sw.Stop();
        result.EndTime = DateTime.Now;

        if (fpsValues.Count > 0)
        {
            result.AverageFPS = fpsValues.Average();
            result.MinFPS = fpsValues.Min();
            result.MaxFPS = fpsValues.Max();

            var sortedFps = fpsValues.OrderBy(f => f).ToList();
            int onePctIdx = Math.Max(1, sortedFps.Count / 100);
            int pointOnePctIdx = Math.Max(1, sortedFps.Count / 1000);
            result.OnePercentLowFPS = sortedFps.Take(onePctIdx).First();
            result.PointOnePercentLowFPS = sortedFps.Take(pointOnePctIdx).First();
        }

        if (frameTimes.Count > 0)
        {
            result.AverageFrameTimeMs = frameTimes.Average();
            result.MaxFrameTimeMs = frameTimes.Max();
        }

        if (cpuUsages.Count > 0) result.AverageCPUUsage = cpuUsages.Average();
        if (gpuUsages.Count > 0) result.AverageGPUUsage = gpuUsages.Average();
        if (ramUsages.Count > 0) result.AverageRAMUsagePercent = ramUsages.Average();
        if (cpuTemps.Count > 0) result.PeakCPUTemperature = cpuTemps.Max();
        if (gpuTemps.Count > 0) result.PeakGPUTemperature = gpuTemps.Max();

        _logger.LogInformation("Benchmark complete: Avg FPS={AvgFPS:F1}, 1% Low={OnePct:F1}, Stutters={Stutters}",
            result.AverageFPS, result.OnePercentLowFPS, result.StutterCount);

        return result;
    }

    public async Task<BenchmarkSnapshot> CaptureSnapshotAsync(CancellationToken ct = default)
    {
        var metrics = await _performanceMonitor.GetCurrentMetricsAsync();
        return new BenchmarkSnapshot
        {
            Timestamp = DateTime.Now,
            Metrics = metrics
        };
    }

    public BenchmarkComparison CompareSnapshots(BenchmarkSnapshot before, BenchmarkSnapshot after)
    {
        return new BenchmarkComparison
        {
            Before = before,
            After = after
        };
    }
}
