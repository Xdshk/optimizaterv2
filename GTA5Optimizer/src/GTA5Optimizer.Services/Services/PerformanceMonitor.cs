using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Monitoring;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Монитор производительности в реальном времени
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private Timer? _monitoringTimer;
    private readonly object _timerLock = new();

    public event Action<PerformanceMetrics>? OnMetricsUpdated;

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
    }

    public void StartMonitoring()
    {
        lock (_timerLock)
        {
            _monitoringTimer ??= new Timer(
                async _ => await UpdateMetricsAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(500));
        }
    }

    public void StopMonitoring()
    {
        lock (_timerLock)
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
        }
    }

    public Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        return UpdateMetricsAsync();
    }

    private async Task<PerformanceMetrics> UpdateMetricsAsync()
    {
        var metrics = new PerformanceMetrics();

        try
        {
            // CPU
            metrics.CPUUsage = GetCPUUsage();

            // RAM
            GetRAMInfo(out var totalRAM, out var availableRAM);
            metrics.TotalRAM = totalRAM;
            metrics.AvailableRAM = availableRAM;
            metrics.UsedRAM = totalRAM - availableRAM;
            metrics.StandbyMemory = GetStandbyMemory();

            // GPU
            metrics.GPUUsage = GetGPUUsage();
            metrics.GPUTemperature = GetGPUTemperature();

            // Disk
            metrics.DiskReadSpeedMBps = GetDiskReadSpeed();
            metrics.DiskWriteSpeedMBps = GetDiskWriteSpeed();

            // Network
            metrics.CurrentPing = await GetPingAsync();

            // Game specific
            var gtaProcess = Process.GetProcessesByName("GTA5").FirstOrDefault();
            if (gtaProcess != null)
            {
                metrics.GameWorkingSet = gtaProcess.WorkingSet64;
                metrics.GamePrivateBytes = gtaProcess.PrivateMemorySize64;
                metrics.CPUUsageGame = GetProcessCPUUsage(gtaProcess);
            }

            metrics.CurrentFPS = GetCurrentFPS();

            OnMetricsUpdated?.Invoke(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении метрик");
        }

        return metrics;
    }

    public Task<BottleneckAnalysis> AnalyzeBottlenecksAsync(PerformanceMetrics metrics)
    {
        var analysis = new BottleneckAnalysis();
        var bottlenecks = new List<BottleneckDetail>();

        if (metrics.CPUUsage > 90)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.CPU,
                Component = "CPU",
                CurrentValue = metrics.CPUUsage,
                ThresholdValue = 90,
                Severity = Math.Min(100, metrics.CPUUsage - 80),
                Description = $"Высокая нагрузка на CPU: {metrics.CPUUsage:F1}%",
                Recommendation = "Закройте фоновые приложения, уменьшите разрешение или частоту кадров"
            });
        }

        if (metrics.GPUUsage > 95)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.GPU,
                Component = "GPU",
                CurrentValue = metrics.GPUUsage,
                ThresholdValue = 95,
                Severity = Math.Min(100, metrics.GPUUsage - 90),
                Description = $"GPU перегружен: {metrics.GPUUsage:F1}%",
                Recommendation = "Уменьшите настройки графики, обновите драйвера"
            });
        }

        if (metrics.RAMUsagePercent > 85)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.RAM,
                Component = "RAM",
                CurrentValue = metrics.RAMUsagePercent,
                ThresholdValue = 85,
                Severity = Math.Min(100, metrics.RAMUsagePercent - 80),
                Description = $"Низкий уровень свободной памяти: {100 - metrics.RAMUsagePercent:F1}%",
                Recommendation = "Закройте приложения, увеличьте очистку standby памяти"
            });
        }

        if (metrics.DiskReadSpeedMBps < 100 && metrics.IsTextureStreamingBottleneck)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.Disk,
                Component = "Disk",
                CurrentValue = metrics.DiskReadSpeedMBps,
                ThresholdValue = 100,
                Severity = 90,
                Description = "Замедленная загрузка текстур из-за медленного диска",
                Recommendation = "Перенесите игру на SSD или уменьшите качество текстур"
            });
        }

        if (metrics.CPUTemperature > 85 || metrics.GPUTemperature > 83)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.Thermal,
                Component = "Thermal",
                CurrentValue = Math.Max(metrics.CPUTemperature, metrics.GPUTemperature),
                ThresholdValue = 85,
                Severity = 85,
                Description = $"Высокая температура: CPU {metrics.CPUTemperature:F0}°C, GPU {metrics.GPUTemperature:F0}°C",
                Recommendation = "Проверьте кулеры, очистите систему охлаждения"
            });
        }

        analysis.Details = bottlenecks;
        analysis.Recommendations = bottlenecks.Select(b => b.Recommendation).ToList();
        analysis.PrimaryBottleneck = bottlenecks.OrderByDescending(b => b.Severity).FirstOrDefault()?.Type ?? BottleneckType.None;
        analysis.SeverityScore = bottlenecks.Any() ? bottlenecks.Max(b => b.Severity) : 0;

        return Task.FromResult(analysis);
    }

    #region Helper methods
    private static float GetCPUUsage()
    {
        using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        cpuCounter.NextValue();
        Thread.Sleep(100);
        return cpuCounter.NextValue();
    }

    private static void GetRAMInfo(out long totalRAM, out long availableRAM)
    {
        totalRAM = 0;
        availableRAM = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject? mo in searcher.Get())
            {
                // Values are in KB
                totalRAM = Convert.ToInt64(mo["TotalVisibleMemorySize"]) * 1024;
                availableRAM = Convert.ToInt64(mo["FreePhysicalMemory"]) * 1024;
                break;
            }
        }
        catch { }
    }

    private static long GetStandbyMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT StandbyListSize FROM Win32_PerfFormattedData_PerfOS_Memory");
            foreach (ManagementObject? mo in searcher.Get())
            {
                return Convert.ToInt64(mo["StandbyListSize"]) * 1024;
            }
        }
        catch { }
        return 0;
    }

    private static float GetGPUTemperature()
    {
        // Requires third-party library (e.g., LibreHardwareMonitor) or NVIDIA/AMD SDK
        return 0f;
    }

    private static float GetGPUUsage()
    {
        // Requires third-party library (e.g., LibreHardwareMonitor) or NVIDIA/AMD SDK
        return 0f;
    }

    private static float GetDiskReadSpeed()
    {
        try
        {
            using var counter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            counter.NextValue();
            Thread.Sleep(100);
            return counter.NextValue() / 1024 / 1024;
        }
        catch { return 0; }
    }

    private static float GetDiskWriteSpeed()
    {
        try
        {
            using var counter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            counter.NextValue();
            Thread.Sleep(100);
            return counter.NextValue() / 1024 / 1024;
        }
        catch { return 0; }
    }

    private static async Task<float> GetPingAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : 0;
        }
        catch { return 0; }
    }

    private static float GetProcessCPUUsage(Process process)
    {
        try
        {
            var startTime = DateTime.Now;
            var startCpuUsage = process.TotalProcessorTime;
            Thread.Sleep(500);
            var endTime = DateTime.Now;
            var endCpuUsage = process.TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return (float)(cpuUsageTotal * 100);
        }
        catch { return 0; }
    }

    private static float GetCurrentFPS()
    {
        // FPS requires special hooking (e.g., RTSS, PresentMon, or in-game overlay API)
        return 0f;
    }
    #endregion
}
