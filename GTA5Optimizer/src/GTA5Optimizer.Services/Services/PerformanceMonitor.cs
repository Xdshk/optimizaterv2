using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Monitoring;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GTA5Optimizer.Services.Services;

public sealed class PerformanceMonitor : IPerformanceMonitor, IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly Computer _computer;
    private readonly IScreenFpsCounter? _fpsCounter;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Timer? _monitoringTimer;
    private readonly object _timerLock = new();

    // Cached metrics — returned when update is in progress
    private PerformanceMetrics? _cachedMetrics;
    private readonly object _cacheLock = new();

    // FPS tracking from screen counter or external ReportFrame calls
    private readonly Queue<double> _fpsSamples = new();
    private DateTime _lastReportedFrameTime = DateTime.MinValue;
    private DateTime _lastValidFpsTime = DateTime.MinValue;
    private DateTime _lastWindowFpsTime = DateTime.MinValue;
    private long _lastWindowFramesPresented;
    private readonly object _windowFpsLock = new();
    private const int MaxFpsHistory = 600;

    // Heavy operation caching
    private DateTime _lastPingTime = DateTime.MinValue;
    private double _cachedPing;
    private DateTime _lastDiskTime = DateTime.MinValue;
    private double _cachedDiskRead;
    private double _cachedDiskWrite;
    private const int PingCacheMs = 5000;
    private const int DiskCacheMs = 2000;

    public event Action<PerformanceMetrics>? OnMetricsUpdated;

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger, IScreenFpsCounter? fpsCounter = null)
    {
        _logger = logger;
        _fpsCounter = fpsCounter;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();

    }

    public void StartMonitoring()
    {
        lock (_timerLock)
        {
            _monitoringTimer ??= new Timer(
                async _ => await UpdateMetricsAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(1000));
        }

        _fpsCounter?.StartCapture();
    }

    public void StopMonitoring()
    {
        lock (_timerLock)
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
        }

        _fpsCounter?.StopCapture();
    }

    public Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        return UpdateMetricsAsync();
    }

    private async Task<PerformanceMetrics> UpdateMetricsAsync()
    {
        if (!await _updateLock.WaitAsync(0))
        {
            // Return cached metrics instead of empty object
            lock (_cacheLock)
                return _cachedMetrics ?? new PerformanceMetrics();
        }

        try
        {
            var metrics = new PerformanceMetrics { Timestamp = DateTime.Now };

            // Update LibreHardwareMonitor
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
            }

            // CPU
            await PopulateCpuMetricsAsync(metrics);

            // GPU
            PopulateGpuMetrics(metrics);

            // RAM
            PopulateRamMetrics(metrics);

            // Disk (cached to avoid heavy PerfCounter every second)
            await PopulateDiskMetricsCachedAsync(metrics);

            // FPS — use RTSS/DWM when available, otherwise keep the last external frame report.
            var screenFps = _fpsCounter?.CurrentFPS ?? 0;
            if (screenFps > 0 && screenFps <= 1000)
            {
                metrics.CurrentFPS = screenFps;
                AddFpsSample(screenFps);
            }
            else
            {
                metrics.CurrentFPS = GetGtaWindowFps();
            }

            if (metrics.CurrentFPS <= 0)
                metrics.CurrentFPS = GetLastReportedFps();

            metrics.FrameTimeMs = metrics.CurrentFPS > 0 ? (int)(1000.0 / metrics.CurrentFPS) : 0;
            PopulateFpsHistory(metrics);

            // Game process
            var gtaProcess = Process.GetProcessesByName("GTA5").FirstOrDefault();
            if (gtaProcess != null)
            {
                try
                {
                    metrics.GameWorkingSet = gtaProcess.WorkingSet64;
                    metrics.GamePrivateBytes = gtaProcess.PrivateMemorySize64;
                    metrics.CPUUsageGame = await GetProcessCpuUsageAsync(gtaProcess);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not read GTA5 process metrics");
                }
            }

            // Network ping (cached)
            metrics.CurrentPing = await GetPingCachedAsync();

            // Cache the result
            lock (_cacheLock)
                _cachedMetrics = metrics;

            OnMetricsUpdated?.Invoke(metrics);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance metrics");
            lock (_cacheLock)
                return _cachedMetrics ?? new PerformanceMetrics();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private void AddFpsSample(double fps)
    {
        if (fps <= 0 || fps > 1000)
            return;

        lock (_fpsSamples)
        {
            _fpsSamples.Enqueue(fps);
            while (_fpsSamples.Count > MaxFpsHistory)
                _fpsSamples.Dequeue();
        }
    }

    private double GetLastReportedFps()
    {
        lock (_fpsSamples)
        {
            if (_fpsSamples.Count == 0)
                return 0;

            var latest = _fpsSamples.Last();
            var elapsedSeconds = (DateTime.UtcNow - _lastValidFpsTime).TotalSeconds;
            return elapsedSeconds > 5 ? Math.Max(0, latest - Math.Min(20, elapsedSeconds * 2)) : latest;
        }
    }

    private void PopulateFpsHistory(PerformanceMetrics metrics)
    {
        double[] samples;
        lock (_fpsSamples)
            samples = _fpsSamples.ToArray();

        if (samples.Length == 0)
            return;

        var frameTimes = samples
            .Where(fps => fps > 0 && fps <= 1000)
            .Select(fps => 1000.0 / fps)
            .ToArray();

        if (frameTimes.Length == 0)
            return;

        var sortedFrameTimes = frameTimes.OrderByDescending(t => t).ToArray();
        int onePercentIdx = Math.Max(1, (int)Math.Ceiling(sortedFrameTimes.Length * 0.01));
        int pointOnePercentIdx = Math.Max(1, (int)Math.Ceiling(sortedFrameTimes.Length * 0.001));

        metrics.OnePercentLow = Math.Min(1000.0 / sortedFrameTimes.Take(onePercentIdx).Max(), metrics.CurrentFPS);
        metrics.PointOnePercentLow = Math.Min(1000.0 / sortedFrameTimes.Take(pointOnePercentIdx).Max(), metrics.CurrentFPS);
        metrics.AverageFPS = samples.Average();
    }

    private double GetProcessFps()
    {
        var process = Process.GetProcessesByName("GTA5").FirstOrDefault();
        if (process == null)
            return 0;

        try
        {
            process.Refresh();
            var cpuUsage = process.TotalProcessorTime.TotalMilliseconds;
            var workingSet = process.WorkingSet64;
            var now = DateTime.UtcNow;

            lock (_fpsSamples)
            {
                if (_fpsSamples.Count == 0)
                    return 0;

                var latest = _fpsSamples.Last();
                var estimated = Math.Min(latest, 240);
                AddFpsSample(estimated);
                return estimated;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not estimate GTA5 process FPS");
            return 0;
        }
    }

    private double GetGtaWindowFps()
    {
        var process = Process.GetProcessesByName("GTA5").FirstOrDefault();
        if (process == null || process.MainWindowHandle == IntPtr.Zero)
            return 0;

        var hwnd = process.MainWindowHandle;
        var fps = TryReadRtssFpsForProcess(process.Id);
        if (fps > 0)
            return fps;

        return TryReadDwmWindowFps(hwnd);
    }

    private static double TryReadRtssFpsForProcess(int processId)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2", MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var signature = accessor.ReadUInt32(0);
            if (signature != 0x53535452)
                return 0;

            var version = accessor.ReadUInt32(4);
            if (version < 2)
                return 0;

            var entryCount = accessor.ReadInt32(8);
            var entrySize = accessor.ReadInt32(12);
            if (entryCount <= 0 || entrySize < 28)
                return 0;

            for (var i = 0; i < entryCount; i++)
            {
                var offset = 16L + i * entrySize;
                var pid = accessor.ReadInt32(offset);
                if (pid != processId)
                    continue;

                var frameTimeUs = accessor.ReadUInt32(offset + 20);
                if (frameTimeUs <= 0)
                    continue;

                var fps = 1_000_000.0 / frameTimeUs;
                if (fps is > 1 and < 1000)
                    return fps;
            }
        }
        catch { }

        return 0;
    }

    private static double TryReadDwmWindowFps(IntPtr hwnd)
    {
        try
        {
            var info = new DWM_TIMING_INFO { cbSize = Marshal.SizeOf<DWM_TIMING_INFO>() };
            if (DwmGetCompositionTimingInfo(hwnd, ref info) != 0)
                return 0;

            if (info.cFramesPresented > 0 && info.cRefreshFrameDelta > 0 &&
                info.rateRefresh.uiNumerator > 0 && info.rateRefresh.uiDenominator > 0)
            {
                var refreshRate = (double)info.rateRefresh.uiNumerator / info.rateRefresh.uiDenominator;
                var fps = (double)info.cFramesPresented / info.cRefreshFrameDelta * refreshRate;
                return fps is > 1 and < 1000 ? fps : 0;
            }

            if (info.cFrames > 0 && info.cRefreshFrameDelta > 0 &&
                info.rateRefresh.uiNumerator > 0 && info.rateRefresh.uiDenominator > 0)
            {
                var refreshRate = (double)info.rateRefresh.uiNumerator / info.rateRefresh.uiDenominator;
                var fps = (double)info.cFrames / info.cRefreshFrameDelta * refreshRate;
                return fps is > 1 and < 1000 ? fps : 0;
            }
        }
        catch { }

        return 0;
    }

    private async Task PopulateCpuMetricsAsync(PerformanceMetrics metrics)
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                        {
                            metrics.CPUUsage = sensor.Value ?? 0;
                        }
                        else if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("CPU Package"))
                        {
                            metrics.CPUTemperature = sensor.Value ?? 0;
                        }
                        else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("CPU Core #1"))
                        {
                            metrics.CPUClock = (sensor.Value ?? 0) * 1000; // MHz
                        }
                    }

                    // Per-core usage
                    var coreUsages = new List<double>();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                        {
                            coreUsages.Add(sensor.Value ?? 0);
                        }
                    }
                    metrics.PerCoreUsage = coreUsages.ToArray();
                    metrics.CPUThreadCount = coreUsages.Count;
                    break;
                }
            }

            // Fallback: PerformanceCounter for CPU usage if LHM didn't find it
            if (metrics.CPUUsage == 0)
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                await Task.Delay(200);
                metrics.CPUUsage = cpuCounter.NextValue();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read CPU metrics from LibreHardwareMonitor");
        }
    }

    private void PopulateGpuMetrics(PerformanceMetrics metrics)
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuAmd ||
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        switch (sensor.SensorType)
                        {
                            case SensorType.Load when sensor.Name.Contains("GPU Core"):
                                metrics.GPUUsage = sensor.Value ?? 0;
                                break;
                            case SensorType.Temperature when sensor.Name.Contains("GPU Hot Spot"):
                                metrics.GPUTemperature = sensor.Value ?? 0;
                                break;
                            case SensorType.Temperature when sensor.Name.Contains("GPU Core") && metrics.GPUTemperature == 0:
                                metrics.GPUTemperature = sensor.Value ?? 0;
                                break;
                            case SensorType.SmallData when sensor.Name.Contains("GPU Memory Used"):
                                metrics.GPUMemoryUsed = (long)(sensor.Value ?? 0) * 1024 * 1024;
                                break;
                            case SensorType.SmallData when sensor.Name.Contains("GPU Memory Total"):
                                metrics.GPUMemoryTotal = (long)(sensor.Value ?? 0) * 1024 * 1024;
                                break;
                            case SensorType.Clock when sensor.Name.Contains("GPU Core"):
                                metrics.GPUEngineClock = (int)(sensor.Value ?? 0);
                                break;
                            case SensorType.Clock when sensor.Name.Contains("GPU Memory"):
                                metrics.GPUMemoryClock = (int)(sensor.Value ?? 0);
                                break;
                            case SensorType.Power when sensor.Name.Contains("GPU Power"):
                                metrics.GPUPowerDraw = sensor.Value ?? 0;
                                break;
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read GPU metrics from LibreHardwareMonitor");
        }
    }

    private void PopulateRamMetrics(PerformanceMetrics metrics)
    {
        try
        {
            long totalRam = 0;
            long availableRam = 0;

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Memory)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Memory Used"))
                        {
                            totalRam += (long)(sensor.Value ?? 0) * 1024 * 1024;
                        }
                        if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Memory Available"))
                        {
                            availableRam += (long)(sensor.Value ?? 0) * 1024 * 1024;
                        }
                    }
                }
            }

            if (totalRam > 0)
            {
                metrics.TotalRAM = totalRam + availableRam;
                metrics.AvailableRAM = availableRam;
                metrics.UsedRAM = totalRam;
            }
            else
            {
                // Fallback to WMI
                GetRamInfo(out var wmiTotal, out var wmiAvailable);
                metrics.TotalRAM = wmiTotal;
                metrics.AvailableRAM = wmiAvailable;
                metrics.UsedRAM = wmiTotal - wmiAvailable;
            }

            // Standby memory
            metrics.StandbyMemory = GetStandbyMemory();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read RAM metrics");
        }
    }

    private async Task PopulateDiskMetricsCachedAsync(PerformanceMetrics metrics)
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Storage)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Activity"))
                        {
                            metrics.DiskActiveTimePercent = sensor.Value ?? 0;
                        }
                    }
                }
            }

            // Disk speeds are expensive (need 200ms delay) — cache them
            var now = DateTime.UtcNow;
            if ((now - _lastDiskTime).TotalMilliseconds < DiskCacheMs)
            {
                metrics.DiskReadSpeedMBps = _cachedDiskRead;
                metrics.DiskWriteSpeedMBps = _cachedDiskWrite;
            }
            else
            {
                using var readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                using var writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                readCounter.NextValue();
                writeCounter.NextValue();
                await Task.Delay(200);
                _cachedDiskRead = readCounter.NextValue() / 1024 / 1024;
                _cachedDiskWrite = writeCounter.NextValue() / 1024 / 1024;
                _lastDiskTime = now;
                metrics.DiskReadSpeedMBps = _cachedDiskRead;
                metrics.DiskWriteSpeedMBps = _cachedDiskWrite;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disk metrics");
        }
    }

    private async Task PopulateDiskMetricsAsync(PerformanceMetrics metrics)
    {
        await PopulateDiskMetricsCachedAsync(metrics);
    }

    public void ReportFrame()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastReportedFrameTime).TotalMilliseconds;
        _lastReportedFrameTime = now;
        _lastValidFpsTime = now;

        if (elapsed <= 0 || elapsed >= 1000)
            return;

        var fps = 1000.0 / elapsed;
        AddFpsSample(fps);
    }

    private static void GetRamInfo(out long totalRAM, out long availableRAM)
    {
        totalRAM = 0;
        availableRAM = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject? mo in searcher.Get())
            {
                totalRAM = Convert.ToInt64(mo["TotalVisibleMemorySize"]) * 1024;
                availableRAM = Convert.ToInt64(mo["FreePhysicalMemory"]) * 1024;
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get RAM info: {ex.Message}");
        }
    }

    private static long GetStandbyMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT StandbyCacheNormalPrioritySize, StandbyCacheReserveSize FROM Win32_PerfFormattedData_PerfOS_Memory");
            foreach (ManagementObject? mo in searcher.Get())
            {
                var normal = Convert.ToInt64(mo["StandbyCacheNormalPrioritySize"]);
                var reserve = Convert.ToInt64(mo["StandbyCacheReserveSize"]);
                return normal + reserve;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get standby memory: {ex.Message}");
        }
        return 0;
    }

    private async Task<float> GetPingCachedAsync()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPingTime).TotalMilliseconds < PingCacheMs)
            return (float)_cachedPing;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            _cachedPing = reply.Status == IPStatus.Success ? reply.RoundtripTime : 0;
            _lastPingTime = now;
            return (float)_cachedPing;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get ping: {ex.Message}");
            return (float)_cachedPing;
        }
    }

    private static async Task<float> GetPingAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get ping: {ex.Message}");
            return 0;
        }
    }

    private static async Task<float> GetProcessCpuUsageAsync(Process process)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpu = process.TotalProcessorTime;
            await Task.Delay(500);
            var endTime = DateTime.UtcNow;
            process.Refresh();
            var endCpu = process.TotalProcessorTime;
            var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
            var totalMs = (endTime - startTime).TotalMilliseconds;
            var cpuUsage = cpuUsedMs / (Environment.ProcessorCount * totalMs);
            return (float)(cpuUsage * 100);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get process CPU: {ex.Message}");
            return 0;
        }
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
                Description = $"High CPU load: {metrics.CPUUsage:F1}%",
                Recommendation = "Close background applications, reduce resolution or frame rate limit"
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
                Description = $"GPU overloaded: {metrics.GPUUsage:F1}%",
                Recommendation = "Lower graphics settings, update GPU drivers"
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
                Description = $"Low free memory: {100 - metrics.RAMUsagePercent:F1}%",
                Recommendation = "Close applications, run memory cleanup"
            });
        }

        if (metrics.GPUMemoryUsagePercent > 90)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.GPU,
                Component = "VRAM",
                CurrentValue = metrics.GPUMemoryUsagePercent,
                ThresholdValue = 90,
                Severity = Math.Min(100, (int)metrics.GPUMemoryUsagePercent - 85),
                Description = $"VRAM nearly full: {metrics.GPUMemoryUsagePercent:F1}%",
                Recommendation = "Lower texture quality, reduce resolution"
            });
        }

        if (metrics.DiskReadSpeedMBps < 50 && metrics.DiskActiveTimePercent > 80)
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.Disk,
                Component = "Disk",
                CurrentValue = metrics.DiskReadSpeedMBps,
                ThresholdValue = 50,
                Severity = 80,
                Description = "Slow disk causing texture streaming issues",
                Recommendation = "Move game to SSD, reduce texture quality"
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
                Description = $"High temperature: CPU {metrics.CPUTemperature:F0}°C, GPU {metrics.GPUTemperature:F0}°C",
                Recommendation = "Check cooling, clean dust from fans"
            });
        }

        if (metrics.FrameTimeMs > 33 && metrics.CurrentFPS > 0) // < 30 FPS sustained
        {
            bottlenecks.Add(new BottleneckDetail
            {
                Type = BottleneckType.GameEngine,
                Component = "Game",
                CurrentValue = metrics.FrameTimeMs,
                ThresholdValue = 33,
                Severity = Math.Min(100, metrics.FrameTimeMs * 2),
                Description = $"Low framerate: {metrics.CurrentFPS:F0} FPS ({metrics.FrameTimeMs}ms)",
                Recommendation = "Run optimization or lower game settings"
            });
        }

        analysis.Details = bottlenecks;
        analysis.Recommendations = bottlenecks.Select(b => b.Recommendation).ToList();
        analysis.PrimaryBottleneck = bottlenecks.OrderByDescending(b => b.Severity).FirstOrDefault()?.Type ?? BottleneckType.None;
        analysis.SeverityScore = bottlenecks.Any() ? bottlenecks.Max(b => b.Severity) : 0;

        return Task.FromResult(analysis);
    }

    public void Dispose()
    {
        StopMonitoring();
        _cts.Cancel();
        _monitoringTimer?.Dispose();
        _updateLock.Dispose();
        _cts.Dispose();
        _computer.Close();
        _fpsCounter?.StopCapture();
    }
}
