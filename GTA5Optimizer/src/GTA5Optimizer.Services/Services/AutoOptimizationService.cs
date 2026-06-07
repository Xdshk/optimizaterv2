using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Logging;
using GTA5Optimizer.Models.Optimization;
using GTA5Optimizer.Models.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Сервис автооптимизации каждые 30 секунд
/// </summary>
public class AutoOptimizationService : BackgroundService
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAutoOptimizationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автооптимизации");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PerformAutoOptimizationAsync()
    {
        using var scope = _serviceProvider.CreateScope();

        var processManager = scope.ServiceProvider.GetRequiredService<IProcessManager>();
        var memoryManager = scope.ServiceProvider.GetRequiredService<IMemoryManager>();
        var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitor>();

        var metrics = await performanceMonitor.GetCurrentMetricsAsync();

        if (metrics.RAMUsagePercent > 85)
        {
            _logger.LogInformation($"Высокое использование RAM: {metrics.RAMUsagePercent:F1}%. Запуск очистки...");
            await memoryManager.OptimizeMemoryAsync();
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5Optimizer.Models.Logging.LogLevel.Information,
                Category = LogCategories.Memory,
                Message = $"Автоочистка памяти: RAM {metrics.RAMUsagePercent:F1}% > 85%",
                Properties = new Dictionary<string, object> { ["RAM_Usage"] = metrics.RAMUsagePercent }
            });
        }

        if (metrics.CPUUsage > 90)
        {
            _logger.LogInformation($"Высокая нагрузка на CPU: {metrics.CPUUsage:F1}%");
            await OptimizeCpuBoundProcesses(processManager);
        }

        await OptimizeBackgroundProcessesAsync(processManager);

        if (metrics.CPUTemperature > 85 || metrics.GPUTemperature > 83)
        {
            _logger.LogWarning($"Высокая температура: CPU {metrics.CPUTemperature:F0}°C, GPU {metrics.GPUTemperature:F0}°C");
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5Optimizer.Models.Logging.LogLevel.Warning,
                Category = LogCategories.System,
                Message = $"Высокая температура: CPU {metrics.CPUTemperature:F0}°C, GPU {metrics.GPUTemperature:F0}°C"
            });
        }
    }

    private async Task OptimizeCpuBoundProcesses(IProcessManager processManager)
    {
        var processes = await processManager.GetRunningProcessesAsync();
        var cpuIntensive = processes.Where(p => p.CPUUsage > 50).ToList();

        foreach (var proc in cpuIntensive)
        {
            if (IsSystemProcess(proc.ProcessName))
                continue;

            await processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Low);
        }
    }

    private async Task OptimizeBackgroundProcessesAsync(IProcessManager processManager)
    {
        var backgroundProcesses = new[]
        {
            "chrome", "msedge", "opera", "brave",
            "discord", "teams", "onenote", "onenotem",
            "steam", "steamwebhelper", "overwolf"
        };

        foreach (var procName in backgroundProcesses)
        {
            var procs = await processManager.GetProcessesByNameAsync(procName);
            foreach (var proc in procs)
            {
                await processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Low);
            }
        }
    }

    private static bool IsSystemProcess(string processName)
    {
        var systemProcesses = new[] { "Idle", "System", "Registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon" };
        return systemProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }
}
