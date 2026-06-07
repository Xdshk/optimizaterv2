using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;
using GTA5Optimizer.Models.System;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Системный оптимизатор для GTA V и Majestic RP
/// </summary>
public class SystemOptimizer : ISystemOptimizer
{
    private readonly ILogger<SystemOptimizer> _logger;
    private readonly ILoggerService _loggerService;
    private readonly IProcessManager _processManager;
    private readonly IMemoryManager _memoryManager;
    private readonly IRegistryManager _registryManager;
    private readonly IGameDetector _gameDetector;

    private Guid _originalPowerScheme = Guid.Empty;

    // High performance power plan GUID
    private static readonly Guid HighPerformanceGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerGetActiveScheme(
        IntPtr UserPowerKey, ref IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(
        IntPtr UserPowerKey, ref Guid ActivePolicyGuid);

    public SystemOptimizer(
        ILogger<SystemOptimizer> logger,
        ILoggerService loggerService,
        IProcessManager processManager,
        IMemoryManager memoryManager,
        IRegistryManager registryManager,
        IGameDetector gameDetector)
    {
        _logger = logger;
        _loggerService = loggerService;
        _processManager = processManager;
        _memoryManager = memoryManager;
        _registryManager = registryManager;
        _gameDetector = gameDetector;
    }

    public async Task<bool> ApplyOptimizationsAsync(OptimizationProfile profile)
    {
        try
        {
            _logger.LogInformation($"Применение оптимизаций для профиля: {profile}");

            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Information,
                Category = LogCategories.Optimization,
                Message = $"Начало оптимизации — профиль: {profile}",
                Properties = new Dictionary<string, object> { ["Profile"] = profile.ToString() }
            });

            // Создаем точку восстановления
            await _registryManager.CreateRestorePointAsync($"GTA5Optimizer - {profile}");
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Information,
                Category = LogCategories.System,
                Message = "Точка восстановления создана"
            });

            var config = ProfileConfig.GetDefaultProfile(profile);
            var results = new List<OptimizationResult>();

            if (config.SetHighPerformancePowerPlan)
            {
                var powerResult = await ApplyPowerPlanAsync();
                results.Add(powerResult);
                await _loggerService.LogAsync(new LogEntry
                {
                    Level = powerResult.Success ? LogLevel.Information : LogLevel.Warning,
                    Category = LogCategories.Optimization,
                    Message = powerResult.Message,
                    Details = powerResult.Details
                });
            }

            var processResult = await ApplyProcessOptimizationsAsync(config);
            results.Add(processResult);
            await _loggerService.LogAsync(new LogEntry
            {
                Level = processResult.Success ? LogLevel.Information : LogLevel.Warning,
                Category = LogCategories.Process,
                Message = processResult.Message,
                Details = processResult.Details
            });

            if (config.EnableMemoryCleanup)
            {
                var memoryResult = await _memoryManager.OptimizeMemoryAsync();
                var memOptResult = new OptimizationResult
                {
                    Success = memoryResult.Success,
                    Message = "Оптимизация памяти",
                    Details = memoryResult.Details,
                    Category = OptimizationCategory.MemoryCleanup
                };
                results.Add(memOptResult);
                await _loggerService.LogAsync(new LogEntry
                {
                    Level = memoryResult.Success ? LogLevel.Information : LogLevel.Warning,
                    Category = LogCategories.Memory,
                    Message = memoryResult.Success ? $"Память оптимизирована: {memoryResult.Details}" : "Ошибка оптимизации памяти",
                    Details = memoryResult.Details
                });
            }

            var windowsResult = await ApplyWindowsOptimizationsAsync(config);
            results.Add(windowsResult);
            await _loggerService.LogAsync(new LogEntry
            {
                Level = windowsResult.Success ? LogLevel.Information : LogLevel.Warning,
                Category = LogCategories.System,
                Message = windowsResult.Message,
                Details = windowsResult.Details
            });

            var diskResult = await ApplyDiskOptimizationsAsync(config);
            results.Add(diskResult);
            await _loggerService.LogAsync(new LogEntry
            {
                Level = diskResult.Success ? LogLevel.Information : LogLevel.Warning,
                Category = LogCategories.Disk,
                Message = diskResult.Message,
                Details = diskResult.Details
            });

            var allSuccess = results.All(r => r.Success);
            await _loggerService.LogAsync(new LogEntry
            {
                Level = allSuccess ? LogLevel.Information : LogLevel.Warning,
                Category = LogCategories.Optimization,
                Message = allSuccess ? "Оптимизация завершена успешно" : $"Оптимизация завершена с ошибками: {results.Count(r => !r.Success)} из {results.Count}",
                Properties = new Dictionary<string, object>
                {
                    ["Success"] = allSuccess,
                    ["StepsTotal"] = results.Count,
                    ["StepsFailed"] = results.Count(r => !r.Success)
                }
            });

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при применении оптимизаций");
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Error,
                Category = LogCategories.Optimization,
                Message = "Критическая ошибка оптимизации",
                Details = ex.Message,
                Exception = ex
            });
            return false;
        }
    }

    public async Task<bool> RestoreDefaultsAsync()
    {
        try
        {
            _logger.LogInformation("Восстановление настроек по умолчанию");

            await RestorePowerPlanAsync();
            await RestoreProcessPrioritiesAsync();
            await _registryManager.RestoreRegistryKeyAsync(@"SOFTWARE\GTA5Optimizer");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при восстановлении настроек");
            return false;
        }
    }

    public Task<bool> EnableHighPerformanceModeAsync()
    {
        return SetPowerPlanAsync(HighPerformanceGuid);
    }

    public Task<bool> DisableHighPerformanceModeAsync()
    {
        return SetPowerPlanAsync(BalancedGuid);
    }

    private async Task<OptimizationResult> ApplyPowerPlanAsync()
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.PowerPlan,
            Timestamp = DateTime.Now
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();

            _originalPowerScheme = GetActivePowerScheme();
            await SetPowerPlanAsync(HighPerformanceGuid);

            result.Success = true;
            result.Message = "Энергоплан изменен на High Performance";
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Exception = ex;
        }

        return result;
    }

    private async Task<OptimizationResult> ApplyProcessOptimizationsAsync(ProfileConfig config)
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.ProcessPriority,
            Timestamp = DateTime.Now
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var optimizedProcesses = new List<int>();

            // Оптимизируем GTA5.exe
            var gtaProcesses = await _processManager.GetProcessesByNameAsync("GTA5");
            foreach (var proc in gtaProcesses)
            {
                await _processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.High);
                optimizedProcesses.Add(proc.ProcessId);
            }

            // Оптимизируем Majestic RP
            var majesticProcesses = await _processManager.GetProcessesByNameAsync("MajesticRP");
            foreach (var proc in majesticProcesses)
            {
                await _processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.High);
                optimizedProcesses.Add(proc.ProcessId);
            }

            // Оптимизируем Rockstar сервисы
            var rockstarProcesses = await _processManager.GetProcessesByNameAsync("RockstarService");
            foreach (var proc in rockstarProcesses)
            {
                await _processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Normal);
                optimizedProcesses.Add(proc.ProcessId);
            }

            // Закрываем браузеры
            if (config.CloseBrowsers)
            {
                var browsers = new[] { "chrome", "msedge", "opera", "brave" };
                foreach (var browser in browsers)
                {
                    var procs = await _processManager.GetProcessesByNameAsync(browser);
                    foreach (var proc in procs)
                    {
                        await _processManager.KillProcessAsync(proc.ProcessId);
                    }
                }
            }

            // Discord: меняем приоритет вместо убийства
            if (config.CloseDiscordOverlay)
            {
                var discordProcs = await _processManager.GetProcessesByNameAsync("discord");
                foreach (var proc in discordProcs)
                {
                    await _processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Low);
                }
            }

            result.Success = true;
            result.Message = "Процессы оптимизированы";
            result.ProcessesOptimized = optimizedProcesses.Count;
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Exception = ex;
        }

        return result;
    }

    private async Task<OptimizationResult> ApplyWindowsOptimizationsAsync(ProfileConfig config)
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.WindowsServices,
            Timestamp = DateTime.Now,
            Message = "Оптимизация Windows"
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (config.DisableGameDVR)
            {
                await DisableGameDVRAsync();
            }

            if (config.DisableXboxServices)
            {
                await DisableXboxServicesAsync();
            }

            if (config.OptimizeNetworkStack)
            {
                await OptimizeNetworkStackAsync();
            }

            if (config.OptimizeTaskScheduler)
            {
                await OptimizeTaskSchedulerAsync();
            }

            result.Success = true;
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Exception = ex;
        }

        return result;
    }

    private async Task<OptimizationResult> ApplyDiskOptimizationsAsync(ProfileConfig config)
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.DiskOptimization,
            Timestamp = DateTime.Now
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var gameInfo = await _gameDetector.DetectGameAsync();
            if (gameInfo.IsOnHDD)
            {
                result.Message += "GTA V обнаружена на HDD. ";
                result.NeedsSSDWarning = true;
            }

            result.Success = true;
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            result.Exception = ex;
        }

        return result;
    }

    private static Guid GetActivePowerScheme()
    {
        try
        {
            IntPtr ptr = IntPtr.Zero;
            var result = PowerGetActiveScheme(IntPtr.Zero, ref ptr);
            if (result == 0)
            {
                var guid = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid))!;
                Marshal.FreeHGlobal(ptr);
                return guid;
            }
        }
        catch { }
        return Guid.Empty;
    }

    private Task<bool> SetPowerPlanAsync(Guid schemeGuid)
    {
        try
        {
            var result = PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            return Task.FromResult(result == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private async Task RestorePowerPlanAsync()
    {
        if (_originalPowerScheme != Guid.Empty)
        {
            await SetPowerPlanAsync(_originalPowerScheme);
        }
    }

    private async Task DisableGameDVRAsync()
    {
        await _registryManager.WriteRegistryValueAsync(
            @"SOFTWARE\Policies\Microsoft\Windows\GameDVR",
            "AllowGameDVR", 0);
    }

    private async Task DisableXboxServicesAsync()
    {
        var xboxServices = new[] { "XblAuthManager", "XblGameSave", "XboxNetApi", "XboxGIpSvc" };
        foreach (var service in xboxServices)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Service WHERE Name='{service}'");
                foreach (ManagementObject? svc in searcher.Get())
                {
                    svc?.InvokeMethod("ChangeStartMode", new object[] { "Disabled" });
                }
            }
            catch { }
        }
    }

    private async Task OptimizeNetworkStackAsync()
    {
        await _registryManager.WriteRegistryValueAsync(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
            "TcpAckFrequency", 1);

        await _registryManager.WriteRegistryValueAsync(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
            "TCPNoDelay", 1);
    }

    private async Task OptimizeTaskSchedulerAsync()
    {
        await _registryManager.WriteRegistryValueAsync(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule",
            "EnableWorkloadTriggers", 0);
    }

    private async Task RestoreProcessPrioritiesAsync()
    {
        var processes = await _processManager.GetRunningProcessesAsync();
        foreach (var proc in processes)
        {
            await _processManager.SetProcessPriorityAsync(proc.ProcessId, ProcessPriority.Normal);
        }
    }
}
