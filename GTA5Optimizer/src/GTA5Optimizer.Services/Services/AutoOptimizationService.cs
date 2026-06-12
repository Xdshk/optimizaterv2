using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Logging;
using GTA5Optimizer.Models.Optimization;
using GTA5Optimizer.Models.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GTA5Optimizer.Services.Services;

public sealed class AutoOptimizationService : BackgroundService
{
    private readonly Microsoft.Extensions.Logging.ILogger<AutoOptimizationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerService _loggerService;

    public AutoOptimizationService(
        Microsoft.Extensions.Logging.ILogger<AutoOptimizationService> logger,
        IServiceProvider serviceProvider,
        ILoggerService loggerService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _loggerService = loggerService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-optimization service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAutoOptimizationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Auto-optimization service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-optimization");
                await _loggerService.LogAsync(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = LogCategories.Optimization,
                    Message = "Auto-optimization error",
                    Details = ex.Message,
                    Exception = ex
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PerformAutoOptimizationAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();

        // Check if auto-optimization is enabled
        if (!settings.EnableAutoOptimization)
        {
            _logger.LogDebug("Auto-optimization is disabled in settings");
            return;
        }

        // CRITICAL: Check if game is running when AutoOptimizeOnlyInGame is set
        if (settings.AutoOptimizeOnlyInGame)
        {
            var gameDetector = scope.ServiceProvider.GetRequiredService<IGameDetector>();
            var isGameRunning = await gameDetector.IsGameRunningAsync();

            if (!isGameRunning)
            {
                _logger.LogDebug("Auto-optimization skipped: game is not running (AutoOptimizeOnlyInGame=true)");
                return;
            }
        }

        var processManager = scope.ServiceProvider.GetRequiredService<IProcessManager>();
        var memoryManager = scope.ServiceProvider.GetRequiredService<IMemoryManager>();
        var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitor>();

        var metrics = await performanceMonitor.GetCurrentMetricsAsync();

        // Memory cleanup if RAM usage is high
        if (metrics.RAMUsagePercent > 85)
        {
            _logger.LogInformation("High RAM usage detected: {RAMUsage:F1}%. Running memory cleanup...", metrics.RAMUsagePercent);
            var memResult = await memoryManager.OptimizeMemoryAsync();

            await _loggerService.LogAsync(new LogEntry
            {
                Level = memResult.Success ? LogLevel.Information : LogLevel.Warning,
                Category = LogCategories.Memory,
                Message = $"Auto memory cleanup: RAM was {metrics.RAMUsagePercent:F1}%",
                Details = memResult.Details,
                Properties = new Dictionary<string, object>
                {
                    ["RAM_Usage"] = metrics.RAMUsagePercent,
                    ["Memory_Freed_MB"] = memResult.MemoryFreedBytes / 1024.0 / 1024.0
                }
            });
        }

        // CPU optimization
        if (metrics.CPUUsage > 90)
        {
            _logger.LogInformation("High CPU usage detected: {CPUUsage:F1}%. Optimizing processes...", metrics.CPUUsage);
            await OptimizeCpuBoundProcesses(processManager);
        }

        // Background process optimization
        await OptimizeBackgroundProcessesAsync(processManager);

        // Temperature warning
        if (metrics.CPUTemperature > 85 || metrics.GPUTemperature > 83)
        {
            _logger.LogWarning("High temperature detected: CPU {CPUTemp:F0}°C, GPU {GPUTemp:F0}°C",
                metrics.CPUTemperature, metrics.GPUTemperature);
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = LogCategories.System,
                Message = $"High temperature: CPU {metrics.CPUTemperature:F0}°C, GPU {metrics.GPUTemperature:F0}°C"
            });
        }
    }

    private async Task OptimizeCpuBoundProcesses(IProcessManager processManager)
    {
        try
        {
            var processes = await processManager.GetRunningProcessesAsync();
            var cpuIntensive = processes.Where(p => p.CPUUsage > 50).ToList();

            foreach (var proc in cpuIntensive)
            {
                if (IsSystemProcess(proc.ProcessName))
                    continue;

                await processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Low);
                _logger.LogDebug("Set low priority for process {ProcessName} (PID {PID})", proc.ProcessName, proc.ProcessId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize CPU-bound processes");
        }
    }

    private async Task OptimizeBackgroundProcessesAsync(IProcessManager processManager)
    {
        var backgroundProcesses = new[]
        {
            "chrome", "msedge", "opera", "brave",
            "teams", "onenote", "onenotem",
            "steamwebhelper", "overwolf"
        };

        foreach (var procName in backgroundProcesses)
        {
            try
            {
                var procs = await processManager.GetProcessesByNameAsync(procName);
                foreach (var proc in procs)
                {
                    await processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Low);
                    _logger.LogDebug("Set low priority for background process {ProcessName}", procName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to optimize background process {ProcessName}", procName);
            }
        }
    }

    private static bool IsSystemProcess(string processName)
    {
        var systemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Idle", "System", "Registry", "smss", "csrss", "wininit",
            "services", "lsass", "winlogon", "dwm", "svchost",
            "fontdrvinit", "sihost", "taskhostw", "explorer",
            "GTA5", "GTA5Optimizer", "GTA5Optimizer.UI"
        };
        return systemProcesses.Contains(processName);
    }
}
